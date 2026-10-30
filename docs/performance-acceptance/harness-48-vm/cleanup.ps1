param([Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$Layout=[IO.Path]::GetFullPath($Layout).TrimEnd('\')
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Force $Evidence | Out-Null
$env:WINAPP_UI_WORKFLOW_ID='offline48-c6355575b'
$package=Get-AppxPackage -Name Mang.BiliCopilot.Offline48.C6355575b
if($package -and $package.InstallLocation -ne $Layout){throw 'Owned package location mismatch; refusing cleanup'}
$owned=@(Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue | Where-Object Path -eq "$Layout\BiliCopilot.UI.exe")
if(@($owned | Where-Object Id -eq 9608).Count){throw 'Protected PID'}
foreach($p in $owned){Stop-Process -Id $p.Id}
if($package){Remove-AppxPackage -Package $package.PackageFullName}
& winapp ui yield --on local --json | Set-Content "$Evidence/final-yield.json"
$yieldCode=$LASTEXITCODE
$retained=Get-AppxPackage -Name Mang.BiliCopilot.PerfAcceptance.Caa6a7be
$result=@{ownedProcessIds=@($owned.Id);ownedProcessAbsent=(@(Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue | Where-Object Path -eq "$Layout\BiliCopilot.UI.exe").Count -eq 0);ownedPackageAbsent=!(Get-AppxPackage -Name Mang.BiliCopilot.Offline48.C6355575b);retainedPackagePresent=!!$retained;retainedLocation=$retained.InstallLocation;yieldExitCode=$yieldCode}
$result | ConvertTo-Json | Set-Content "$Evidence/cleanup.json"
if(!$result.ownedProcessAbsent -or !$result.ownedPackageAbsent -or !$result.retainedPackagePresent -or $yieldCode -ne 0){throw 'Cleanup verification failed'}
