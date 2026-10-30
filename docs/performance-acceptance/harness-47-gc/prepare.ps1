param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Destination,[ValidateSet('current','historical')][string]$Mode)
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../harness-46-47/prepare.ps1" -SourceRoot $SourceRoot -Destination $Destination
# Copy ALL Markdown sources into this owned fixture; only historical Clean receives its original one-line call.
$copy="$Destination/source/src/Desktop/BiliCopilot.UI/Controls/Markdown"
New-Item -ItemType Directory -Force $copy | Out-Null
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/Markdown/*" $copy -Recurse
$target="$copy/MarkdownTextBlock.xaml.cs"
if($Mode -eq 'historical') {
 $s=[IO.File]::ReadAllText($target)
 $needle='        _renderer?.Clear();'
 if(([regex]::Matches($s,[regex]::Escape($needle))).Count -ne 1){throw 'Historical patch anchor not unique'}
 [IO.File]::WriteAllText($target,$s.Replace($needle,$needle+"`r`n        GC.Collect();"),[Text.UTF8Encoding]::new($true))
}
$proj=Get-Content "$Destination/Harness.csproj" -Raw
$proj=$proj.Replace('$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/Markdown/**/*.cs','source/src/Desktop/BiliCopilot.UI/Controls/Markdown/**/*.cs')
# Explicit linked Include plus default glob would duplicate the source copy.
$proj=$proj.Replace('<Compile Include="source/', '<Compile Remove="source/**/*.cs" /><Compile Include="source/')
$proj=$proj.Replace('<Content Include="Assets/**" />','<Compile Remove="App.xaml.cs;Diagnostics.cs;Image52.cs;Http52.cs" /><Content Include="Assets/**" />')
Set-Content "$Destination/Harness.csproj" $proj
Copy-Item "$PSScriptRoot/Experiment.cs" $Destination
(Get-Content "$Destination/Package.appxmanifest" -Raw).Replace('BiliCopilot.Acceptance4647','BiliCopilot.GC47D0E5') | Set-Content "$Destination/Package.appxmanifest"
Get-ChildItem $copy -Recurse -File | Get-FileHash | ConvertTo-Json | Set-Content "$Destination/copied-source-hashes.json"
