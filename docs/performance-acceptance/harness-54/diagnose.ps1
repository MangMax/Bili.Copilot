param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
if($AppPid -eq 9608){throw 'Protected process'}
if(Test-Path $Evidence){throw 'Use new evidence directory'}
New-Item -ItemType Directory $Evidence|Out-Null
$env:WINAPP_UI_WORKFLOW_ID='acceptance54-recheck-1d19'
$package=Get-AppxPackage -Name BiliCopilot.Acceptance54.R1D19
$p=Get-Process -Id $AppPid
if(!$package -or !$p.Path.StartsWith($package.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned process mismatch'}
$local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
try {
    & winapp ui invoke Diagnose -a $AppPid --json | Set-Content "$Evidence/invoke.json"
    if($LASTEXITCODE){throw 'Invoke failed'}
    & winapp ui wait-for Status --value 'Diagnostic complete' -t 15000 -a $AppPid --json | Set-Content "$Evidence/wait.json"
    if($LASTEXITCODE){throw 'Diagnostic timeout'}
    Copy-Item "$local/*.json","$local/*.jsonl" $Evidence
    $rows=@(Get-Content "$Evidence/results.json" -Raw|ConvertFrom-Json)
    if($rows.Count -ne 3 -or @($rows|Where-Object status -ne PASS).Count){throw 'Diagnostic red; inspect synchronous GettingFocus and deferred GotFocus stacks'}
} finally {& winapp ui yield --on local --json|Set-Content "$Evidence/yield.json"}
