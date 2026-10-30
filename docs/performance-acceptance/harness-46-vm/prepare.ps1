param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$SourceRoot=(Resolve-Path -LiteralPath $SourceRoot).Path
if(Test-Path -LiteralPath $Destination){throw 'Choose a fresh destination'}
New-Item -ItemType Directory $Destination,"$Destination/Assets","$Destination/Themes" | Out-Null
Copy-Item "$PSScriptRoot/*" $Destination
$ui="$SourceRoot/src/Desktop/BiliCopilot.UI"
foreach($name in 'AIPanel','AIBody','AIFooter','AIHeader'){Copy-Item "$ui/Controls/AI/$name.xaml" $Destination}
Copy-Item "$ui/Controls/Markdown/MarkdownTextBlock.xaml" "$Destination/Themes/Generic.xaml"
Copy-Item "$ui/app.manifest" $Destination
Copy-Item "$ui/Assets/Logo/Square150x150Logo.scale-100.png" "$Destination/Assets/Logo.png"
# Source-derived resource enum and exact formatting helper; no generation logic is rewritten.
$inputs=Get-ChildItem "$ui/ViewModels/Core/AIViewModel/*.cs","$ui/Controls/AI/*","$ui/Controls/Markdown/TextElements/MyCodeBlock.cs"
$names=[regex]::Matches(($inputs | Get-Content -Raw)-join "`n",'(?:StringNames\.|Locale Name=)(\w+)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique
"namespace BiliCopilot.UI.Models.Constants { public enum StringNames { $($names -join ',') } }" | Set-Content "$Destination/Strings.cs"
$app=Get-Content "$ui/Toolkits/AppToolkit.cs" -Raw
$method=[regex]::Match($app,'public static string FormatDuration\(TimeSpan duration\)\s*=>[^;]+;').Value
"namespace BiliCopilot.UI.Toolkits { static class AppToolkit { $method } }" | Set-Content "$Destination/Duration.cs"
# Extract exact styles used by the product controls, retaining their original setters/templates.
[xml]$styles=Get-Content "$ui/Styles/Overrides.xaml"
foreach($node in @($styles.DocumentElement.ChildNodes)){if($node -isnot [System.Xml.XmlElement] -or $node.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml') -notin @('IconButtonStyle','SmallIconButtonStyle','PageScrollViewStyle','PageProgressRingStyle','AppItemContainerStyle')){[void]$styles.DocumentElement.RemoveChild($node)}}
$styles.Save("$Destination/Styles.xaml")
$template=Get-Content "$SourceRoot/docs/performance-acceptance/harness-50-53/prepare.ps1" -Raw
$manifest=[regex]::Match($template,"(?s)<Package xmlns=.*?</Package>").Value.Replace('BiliCopilot.Acceptance5053D26C','BiliCopilot.Acceptance46VM8B8F').Replace('Offline Popular acceptance 50 53','Offline AIViewModel acceptance')
$manifest | Set-Content "$Destination/Package.appxmanifest"
"<Project><PropertyGroup><SourceRoot>$([Security.SecurityElement]::Escape($SourceRoot))</SourceRoot></PropertyGroup></Project>" | Set-Content "$Destination/Directory.Build.props"
$inputs + (Get-Item "$ui/Toolkits/AppToolkit.cs","$ui/Styles/Overrides.xaml") | Get-FileHash | ConvertTo-Json | Set-Content "$Destination/source-hashes.json"
