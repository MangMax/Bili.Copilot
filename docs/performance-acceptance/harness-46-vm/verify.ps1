param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$events=Get-Content "$Evidence/events.jsonl" | ConvertFrom-Json
$asserts=@($events | Where-Object kind -eq 'assert' | ForEach-Object value)
$cases=@($events | Where-Object kind -eq 'case' | ForEach-Object value)
if($cases.Count -ne 10 -or $asserts.Count -ne 38 -or !($events | Where-Object kind -eq 'done')){throw 'Incomplete execution; cannot report expected matrix'}
foreach($video in 'False','True'){foreach($branch in 0..2){foreach($phase in 'Streaming','Final','Preparation'){if(@($asserts|Where-Object name -eq "$video-$branch-$phase").Count -ne 1){throw 'Missing branch observation'}}}}
$copy=@($asserts|Where-Object name -eq 'RealCopyExactUnicodeMarkdown')
if($copy.Count -ne 1 -or !$copy[0].pass){throw 'No verified real copy'}
$latencies=@(($events|Where-Object kind -eq 'dispatcher-proxy-ms').value | Sort-Object)
if($latencies.Count -ne 40){throw 'Missing latency sample'}
$cleanup=Get-Content "$Evidence/cleanup.json" | ConvertFrom-Json
if(!$cleanup.processAbsent -or !$cleanup.packageAbsent){throw 'Cleanup not confirmed'}
$summary=[ordered]@{
 cases=$cases.Count; casesPassed=@($cases|Where-Object pass).Count; casesFailed=@($cases|Where-Object {!$_.pass}).Count
 assertions=$asserts.Count; assertionsPassed=@($asserts|Where-Object pass).Count; assertionsFailed=@($asserts|Where-Object {!$_.pass}).Count
 failures=@($asserts|Where-Object {!$_.pass})
 dispatcherProxyMs=@{count=40;minimum=$latencies[0];median=($latencies[19]+$latencies[20])/2;p95NearestRank=$latencies[37];maximum=$latencies[39]}
 cleanup=$cleanup; yield=(Get-Content "$Evidence/yield.json"|ConvertFrom-Json)
}
$summary|ConvertTo-Json -Depth 8|Set-Content "$Evidence/summary.json"
$summary|ConvertTo-Json -Depth 8
if($summary.assertionsFailed -or $summary.casesFailed){exit 1}
exit 0
