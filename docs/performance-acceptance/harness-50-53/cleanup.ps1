param([int]$AppPid=47000,[string]$Evidence="$PSScriptRoot/../evidence-50-53")
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='bili5053-d26c'
$process=Get-Process -Id $AppPid -ErrorAction SilentlyContinue
if($process){
 if($AppPid -eq 9608 -or -not $process.Path.StartsWith('C:\Users\hp\AppData\Local\Temp\bili5053-d26c-layout\',[StringComparison]::OrdinalIgnoreCase)){throw 'Not owned'}
 winapp ui invoke Close -a $AppPid --json | Set-Content "$Evidence/close.json"
 if($LASTEXITCODE -ne 0){throw 'Close failed'}
 $process.WaitForExit(5000)|Out-Null
 if(Get-Process -Id $AppPid -ErrorAction SilentlyContinue){throw 'Owned process remains'}
}
$package=Get-AppxPackage -Name BiliCopilot.Acceptance5053D26C
if($package){
 if($package.InstallLocation -ne 'C:\Users\hp\AppData\Local\Temp\bili5053-d26c-layout'){throw 'Unexpected package layout'}
 $trace="$env:LOCALAPPDATA/Packages/$($package.PackageFamilyName)/LocalState/trace.jsonl"
 Copy-Item $trace "$Evidence/final-events.jsonl"
 $package|Select-Object Name,PackageFullName,InstallLocation|ConvertTo-Json|Set-Content "$Evidence/owned-package.json"
 Remove-AppxPackage -Package $package.PackageFullName
}
winapp ui yield --json|Set-Content "$Evidence/final-yield.json"
if($LASTEXITCODE -ne 0){throw 'Yield failed'}
@{utc=[DateTimeOffset]::UtcNow;ownedProcessRemaining=[bool](Get-Process -Id $AppPid -ErrorAction SilentlyContinue);ownedPackageRemaining=[bool](Get-AppxPackage -Name BiliCopilot.Acceptance5053D26C);desktopReleased=$true}|ConvertTo-Json|Set-Content "$Evidence/cleanup.json"
