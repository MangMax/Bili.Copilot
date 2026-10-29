param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
function Check($ok,$message){if(!$ok){throw $message}}
$expected=@('SuccessOpen','SuccessDisabled','NonzeroExit','StartFailure','CopyOnlyNoProcess','CloseWindowHostAlive')
$rows=@(Get-Content "$Evidence/run3/results.json" -Raw|ConvertFrom-Json)
Check ($rows.Count -eq 6) 'Expected six normal cases'
foreach($name in $expected){Check (@($rows|Where-Object {$_.name -eq $name -and $_.status -eq 'PASS'}).Count -eq 1) "Missing $name"}
$last=Get-Content "$Evidence/last2/results.json" -Raw|ConvertFrom-Json
Check ($last.name -eq 'CloseLastWindowChildSurvives' -and $last.status -eq 'PASS') 'Last-window case missing'
foreach($name in $expected){
 $events=@(Get-Content "$Evidence/run3/localstate/r3-$name/events.jsonl"|ForEach-Object{$_|ConvertFrom-Json})
 Check (@($events|Where-Object schemaVersion -ne 1).Count -eq 0) 'Wrong event schema'
 $folders=@($events|Where-Object kind -eq 'folder-adapter').Count
 $desired=if($name -in @('SuccessOpen','CloseWindowHostAlive')){1}else{0}
 Check ($folders -eq $desired) "Wrong final folder count $name"
 $disposed=@($events|Where-Object kind -eq 'process-disposed')
 if($name -notin @('StartFailure','CopyOnlyNoProcess')){Check ($disposed.Count -eq 1 -and $disposed[0].data.handleClosed -and $disposed[0].data.handleCaptured) "No closed handle $name"}
 if($name -eq 'CopyOnlyNoProcess'){Check (@($events|Where-Object kind -eq 'process-created').Count -eq 0) 'Copy created process';Check (@($events|Where-Object kind -eq 'clipboard-cleared').Count -eq 1) 'Clipboard not cleared'}
}
$external=Get-Content "$Evidence/last2/last-window-external-observation.json" -Raw|ConvertFrom-Json
Check ($external.hostExited -and $external.childAlive -and $external.folderCallbacks -eq 0 -and $external.disposals -eq 0) 'Exit observation invalid'
$child=Get-Content "$Evidence/last2/localstate/last2-CloseLastWindowChildSurvives/child-exit.json" -Raw|ConvertFrom-Json
Check ($child.pid -eq $external.childPid -and $child.code -eq 0) 'Child did not complete normally'
foreach($phase in @('run3','last2')){
 $calls=@(Get-Content "$Evidence/$phase/uia-calls.json" -Raw|ConvertFrom-Json)
 Check ($calls.Count -gt 10 -and @($calls|Where-Object exit -ne 0).Count -eq 0) "Failed UIA calls $phase"
}
'PASS: 7 exact behavior cases; final events, real child termination, UIA command exits and owned clipboard cleanup verified.'
