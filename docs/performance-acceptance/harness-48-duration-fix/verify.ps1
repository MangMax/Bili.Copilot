param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$cases=@(Get-Content "$Evidence/results.json" -Raw | ConvertFrom-Json)
$names=@('VideoPartDurationBounds','VideoSwitchLateSuccessAndError','PgcSwitchLateSuccessAndError','RepeatedReloadLateSuccessAndError','DirectDisposeInFlight','WindowDestroyInFlight','VmCancellationReachesHttpHandler','EmptyBodyRetriesThreeTimes','FiveByteEmptyFrameParsesOnce','VideoPartZeroDoesNotFallbackToArticle','VideoPart360RequestsOneSegment','VideoPart361RequestsTwoSegments')
if($cases.Count -ne 12 -or @(Compare-Object ($cases.name | Sort-Object) ($names | Sort-Object)).Count -or @($cases | Where-Object status -ne PASS).Count){throw 'Incorrect case set or failure'}
$events=@(Get-Content "$Evidence/events.jsonl" | ForEach-Object {$_ | ConvertFrom-Json})
$d=@($events | Where-Object kind -eq duration)
if($d.Count -ne 1 -or $d[0].data.article -ne 7200 -or $d[0].data.part -ne 120 -or ($d[0].data.indices -join ',') -ne '1'){throw 'Duration regression mismatch'}
foreach($b in @(@{part=0;indices=''},@{part=360;indices='1'},@{part=361;indices='1,2'})){
 $row=@($events | Where-Object {$_.kind -eq 'duration-boundary' -and $_.data.part -eq $b.part})
 if($row.Count -ne 1 -or $row[0].data.article -ne 7200 -or ($row[0].data.initialIndices -join ',') -ne $b.indices -or ($row[0].data.reloadIndices -join ',') -ne $b.indices -or $row[0].data.IsDanmakuLoading){throw 'Boundary mismatch'}
}
$t=@($events | Where-Object kind -eq transport-cancel)
if($t.Count -ne 1 -or $t[0].data.Attempts -ne 6 -or $t[0].data.Cancellations -ne 6 -or @($t[0].data.errors).Count){throw 'Transport mismatch'}
$bodies=@($events | Where-Object kind -eq body)
if($bodies.Count -ne 2){throw 'Missing body cases'}
foreach($b in $bodies){if($b.data.frame){if($b.data.bytes -ne 5 -or $b.data.Attempts -ne 1 -or $b.data.errorCount -ne 0){throw 'Frame mismatch'}}else{if($b.data.bytes -ne 0 -or $b.data.Attempts -ne 3 -or $b.data.errorCount -ne 1){throw 'Body mismatch'}}}
foreach($name in 'duration','video-switch','pgc-switch','reload-command','before-dispose','before-destroy','boundary-0','boundary-360','boundary-361','results'){
 if(!(Test-Path "$Evidence/$name.png") -or (Get-Item "$Evidence/$name.png").Length -eq 0){throw "Missing screenshot $name"}
}
$p=Get-Content "$Evidence/provenance.json" -Raw | ConvertFrom-Json
if(!$p.buildLayoutLoadedEqual -or $p.productionCopiedFiles -lt 800){throw 'Missing provenance'}
$events | Where-Object kind -in 'duration','duration-boundary','current','switch','closed','transport-cancel','body','case' | ConvertTo-Json -Depth 12 | Set-Content "$Evidence/observations.json"
@{Passed=12;Failed=0;Skipped=0;originalMatrixPassed=9;addedBoundariesPassed=3;runnerExitCode=0;actualCaseCount=$cases.Count;sourceFilesCompared=$p.productionCopiedFiles} | ConvertTo-Json | Set-Content "$Evidence/verification.json"
'Passed: 12, Failed: 0, Skipped: 0. Original nine plus three boundaries.'
