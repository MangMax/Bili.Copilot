param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-fixed-d19'
try {
    if($AppPid -eq 9608){throw 'Protected PID'}
    $owned=Get-Process -Id $AppPid -ErrorAction SilentlyContinue
    if($owned){
        if(-not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned executable mismatch'}
        & winapp ui invoke Close -a $AppPid --json
        if($LASTEXITCODE -ne 0 -or -not $owned.WaitForExit(10000)){throw 'Owned close failed'}
    }
    $package=Get-AppxPackage -Name BiliCopilot.Acceptance51FixedD19
    if($package){if($package.InstallLocation -ne $Layout){throw 'Owned package mismatch'};Remove-AppxPackage $package.PackageFullName}
    @{ownedProcessGone=($null -eq (Get-Process -Id $AppPid -ErrorAction SilentlyContinue));ownedPackageGone=($null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance51FixedD19))}|ConvertTo-Json|Set-Content "$Evidence/cleanup.json"
} finally {& winapp ui yield --json|Set-Content "$Evidence/cleanup-yield.json"}

