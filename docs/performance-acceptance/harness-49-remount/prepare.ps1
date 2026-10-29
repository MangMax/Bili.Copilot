param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
# Preserve the original acceptance fixture and production XAML verbatim.
& "$PSScriptRoot/../harness-49/prepare.ps1" -SourceRoot $SourceRoot -Destination $Destination
if(!$?){throw 'Base harness preparation failed'}
$manifest=Join-Path $Destination 'Package.appxmanifest'
(Get-Content $manifest -Raw).Replace('Acceptance49C4D3','Acceptance4931F2') | Set-Content $manifest
$app=Join-Path $Destination 'App.xaml.cs'
(Get-Content $app -Raw).Replace('C4D3','31F2') | Set-Content $app
