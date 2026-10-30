param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$cases=@(Get-Content "$Evidence/results.json" -Raw | ConvertFrom-Json)
$names=@('VideoPartDurationBounds','VideoSwitchLateSuccessAndError','PgcSwitchLateSuccessAndError','RepeatedReloadLateSuccessAndError','DirectDisposeInFlight','WindowDestroyInFlight','VmCancellationReachesHttpHandler','EmptyBodyRetriesThreeTimes','FiveByteEmptyFrameParsesOnce')
if($cases.Count -ne 9 -or @(Compare-Object ($cases.name | Sort-Object) ($names | Sort-Object)).Count){throw 'Incorrect discovered case set'}
if(@($cases | Where-Object {$_.name -eq 'VideoPartDurationBounds' -and $_.status -eq 'FAIL'}).Count -ne 1){throw 'Expected baseline duration red missing'}
if(@($cases | Where-Object status -eq PASS).Count -ne 8){throw 'Independent cancellation/transport cases did not all pass'}
$events=@(Get-Content "$Evidence/events.jsonl" | ForEach-Object {$_ | ConvertFrom-Json})
$duration=@($events | Where-Object kind -eq duration)
if($duration.Count -ne 1 -or $duration[0].data.article -ne 7200 -or $duration[0].data.part -ne 120 -or ($duration[0].data.indices -join ',') -ne ((1..20) -join ',')){throw 'Duration evidence missing'}
$transport=@($events | Where-Object kind -eq transport-cancel)
if($transport.Count -ne 1 -or $transport[0].data.Attempts -ne 6 -or $transport[0].data.Cancellations -ne 6 -or @($transport[0].data.errors).Count -ne 0){throw 'Transport evidence mismatch'}
foreach($name in 'duration','video-switch','pgc-switch','reload-command','before-dispose','before-destroy','results'){
 if(!(Test-Path "$Evidence/$name.png") -or (Get-Item "$Evidence/$name.png").Length -eq 0){throw "Missing screenshot $name"}
}
$provenance=Get-Content "$Evidence/provenance.json" -Raw | ConvertFrom-Json
if(!$provenance.buildLayoutLoadedEqual -or $provenance.productionCopiedFiles -lt 800){throw 'Missing source/binary provenance'}
@{Passed=8;Failed=1;Skipped=0;expectedBaselineRed='VideoPartDurationBounds';acceptanceStatus='FAIL / production defect retained';evidenceConsistency='PASS';runnerExitCode=1;actualCaseCount=$cases.Count;sourceFilesCompared=$provenance.productionCopiedFiles} | ConvertTo-Json | Set-Content "$Evidence/verification.json"
Write-Output 'Evidence consistency PASS. Acceptance remains FAIL. Passed: 8, Failed: 1, Skipped: 0.'
