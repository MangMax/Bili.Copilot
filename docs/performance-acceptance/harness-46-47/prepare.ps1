param([Parameter(Mandatory)][string]$SourceRoot, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
if (Test-Path -LiteralPath $Destination) { throw 'Choose a new empty temporary destination.' }
New-Item -ItemType Directory -Path $Destination | Out-Null
Copy-Item "$PSScriptRoot/*" $Destination
New-Item -ItemType Directory "$Destination/Themes", "$Destination/Assets" | Out-Null
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/Markdown/MarkdownTextBlock.xaml" "$Destination/Themes/Generic.xaml"
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/app.manifest" $Destination
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/Assets/Logo/Square150x150Logo.scale-100.png" "$Destination/Assets/Logo.png"
# Reuse the exact production streaming TextBlock, removing only its ViewModel binding.
[xml]$body = Get-Content "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/AI/AIBody.xaml" -Raw
$node = $body.SelectSingleNode('//*[@AutomationProperties.AutomationId="AIStreamingResponse"]')
if (-not $node) { throw 'Production streaming TextBlock missing.' }
$node.SetAttribute('Text', '')
$node.OuterXml | Set-Content "$Destination/Streaming.xaml" -Encoding utf8
@'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
  <Identity Name="BiliCopilot.Acceptance4647" Publisher="CN=BiliCopilotDev" Version="1.0.0.0" />
  <Properties><DisplayName>Offline acceptance 46 47</DisplayName><PublisherDisplayName>Acceptance</PublisherDisplayName><Logo>Assets\Logo.png</Logo></Properties>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" /></Dependencies>
  <Resources><Resource Language="en-us" /></Resources>
  <Applications><Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$"><uap:VisualElements DisplayName="Offline acceptance 46 47" Description="Isolated offline harness" BackgroundColor="transparent" Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" /></Application></Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
'@ | Set-Content "$Destination/Package.appxmanifest" -Encoding utf8
@"
<Project><PropertyGroup><SourceRoot>$([System.Security.SecurityElement]::Escape($SourceRoot))</SourceRoot></PropertyGroup></Project>
"@ | Set-Content "$Destination/Directory.Build.props" -Encoding utf8
# Production code and manifests are never edited. Restore verification belongs in runner finally.
Get-FileHash "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/Markdown/MarkdownTextBlock.xaml.cs", "$SourceRoot/src/Desktop/BiliCopilot.UI/Toolkits/Pure/StreamingResponseSession.cs", "$SourceRoot/src/Desktop/BiliCopilot.UI/Package.Dev.appxmanifest", "$SourceRoot/src/Desktop/BiliCopilot.UI/Package.appxmanifest" | ConvertTo-Json | Set-Content "$Destination/source-hashes.json"
