param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$OutputFile)
$ErrorActionPreference='Stop'
if($AppPid -le 0 -or $AppPid -eq 9608){throw 'Not an owned PID'}
if(Test-Path $OutputFile){throw 'Output must be new'}
$env:WINAPP_UI_WORKFLOW_ID='engine49-c9d5'
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments --on local -a $AppPid --json;if($LASTEXITCODE -ne 0){throw ($o -join "`n")};$o}
Ui @('wait-for','EngineTests','-t','10000') | Out-Null
$start=Get-Date
Ui @('invoke','EngineTests') | Out-Null
Ui @('wait-for','Status','--value','EngineTests done','--contains','-t','30000') | Out-Null
$log="$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance49C9D5_y1mf40g4r9srg/LocalState/engine-cases.json"
if((Get-Item $log).LastWriteTime -lt $start){throw 'Stale result file'}
Copy-Item $log $OutputFile
$results=Get-Content $OutputFile -Raw | ConvertFrom-Json
if($results.Count -ne 26 -or @($results.name|Sort-Object -Unique).Count -ne 26){throw 'Incomplete/duplicate test results'}
$failed=@($results|Where-Object {!$_.passed}).Count
Write-Output "Passed: $($results.Count-$failed), Failed: $failed, Skipped: 0"
if($failed){exit 1}
