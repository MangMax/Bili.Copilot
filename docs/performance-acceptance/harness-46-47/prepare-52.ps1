param([Parameter(Mandatory)][string]$SourceRoot, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/prepare.ps1" -SourceRoot $SourceRoot -Destination $Destination
$manifest = Join-Path $Destination 'Package.appxmanifest'
(Get-Content $manifest -Raw).Replace('BiliCopilot.Acceptance4647','BiliCopilot.Acceptance52T384527').Replace('Offline acceptance 46 47','HTTP image acceptance 52') | Set-Content $manifest -Encoding utf8
$app = Join-Path $Destination 'App.xaml.cs'
(Get-Content $app -Raw).Replace('window.Activate();', 'Install52(toolbar); window.Activate();') | Set-Content $app -Encoding utf8
