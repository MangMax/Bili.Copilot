param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
if(Test-Path $Destination){throw 'Use a fresh destination'}
$bin="$root/src/_build/x64/Release/BiliCopilot.UI/bin"
# Copy application binaries only, never any package LocalState or account data.
# Exclude Visor to avoid discovering/attaching the other task's shared service.
robocopy $bin $Destination /E /XF BiliCopilot.Visor.exe *.appxrecipe /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
if($LASTEXITCODE -gt 7){throw 'Binary copy failed'}
$manifest=Get-Content "$Destination/AppxManifest.xml" -Raw
$manifest=$manifest.Replace('Name="Mang.BiliCopilot"','Name="Mang.BiliCopilot.Integration2743.Normal"').Replace('Publisher="CN=Mang"','Publisher="CN=BiliCopilotDev"')
[xml]$xml=$manifest
if($xml.Package.Identity.Name -ne 'Mang.BiliCopilot.Integration2743.Normal'){throw 'Identity isolation failed'}
$manifest | Set-Content "$Destination/AppxManifest.xml"
if(Test-Path "$Destination/Assets/visor/x64/BiliCopilot.Visor.exe"){throw 'Visor must be absent from this smoke layout'}
