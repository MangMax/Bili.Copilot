param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Evidence,[switch]$KeepPackage)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='acceptance54-recheck-1d19'
try {
    if($AppPid -eq 9608){throw 'Protected PID'}
    $package=Get-AppxPackage -Name BiliCopilot.Acceptance54.R1D19
    $owned=Get-Process -Id $AppPid -ErrorAction SilentlyContinue
    if($owned){
        if(!$package -or !$owned.Path.StartsWith($package.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned executable mismatch'}
        $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
        $controller=[string](Get-Content "$local/identity.json" -Raw|ConvertFrom-Json).controllerHwnd
        & winapp ui invoke Exit -w $controller --on local --json | Set-Content "$Evidence/exit.json"
        if($LASTEXITCODE -ne 0 -or !$owned.WaitForExit(10000)){throw 'Owned exit failed'}
        Copy-Item "$local/*.json","$local/*.jsonl" $Evidence
    }
    if($package -and !$KeepPackage){Remove-AppxPackage $package.PackageFullName}
    @{ownedProcessGone=($null -eq (Get-Process -Id $AppPid -ErrorAction SilentlyContinue));ownedPackageGone=($null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance54.R1D19));packageRetained=[bool]$KeepPackage}|ConvertTo-Json|Set-Content "$Evidence/cleanup.json"
} finally {& winapp ui yield --on local --json|Set-Content "$Evidence/cleanup-yield.json"}
