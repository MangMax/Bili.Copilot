param([Parameter(Mandatory)][string]$Evidence,[switch]$RequireFocus)
$ErrorActionPreference='Stop'
$rows=@(Get-Content "$Evidence/results.json" -Raw|ConvertFrom-Json)
$events=@(Get-Content "$Evidence/events.jsonl"|ForEach-Object {$_|ConvertFrom-Json})
$names=@('RapidFinalNormal','RestoreBothModesAndSavedGeometry','CloseDuring500ms','CloseBeforeQueuedFocusAndTitle','CloseDuringFirstTitleDelay','CloseDuringSecondTitleDelay')
$expected=@(foreach($round in 1..3){foreach($name in $names){"${name}_R$round"}})
@{cases=$rows.Count;passed=@($rows|Where-Object status -eq PASS).Count;failed=@($rows|Where-Object status -ne PASS).Count;
    closedObservations=@($events|Where-Object kind -eq 'closed-observation').Count;liveFocusEvents=@($events|Where-Object {$_.kind -eq 'focus' -and !$_.data.IsClosed -and !$_.data.IsDisposed}).Count;
    lateFocusEvents=@($events|Where-Object {$_.kind -eq 'focus' -and ($_.data.IsClosed -or $_.data.IsDisposed)}).Count;
    events=$events.Count;visualReview='See ui-54.md; JSON is not visual acceptance'}|ConvertTo-Json|Set-Content "$Evidence/observed-summary.json"
if($rows.Count -ne 18 -or @($rows|Where-Object status -ne PASS).Count -or (Compare-Object $expected @($rows.name))){throw 'Missing, duplicate or failing cases'}
if(@($events|Where-Object {$_.kind -in @('first-chance','unhandled','command-failed','case-failed')}).Count){throw 'Unexpected exceptions'}
$closed=@($events|Where-Object kind -eq 'closed-observation')
if($closed.Count -ne 12 -or @($closed|Where-Object {$_.data.lateFocus -ne 0 -or $_.data.AfterDestroyCalls -ne 0 -or $_.data.IsPresenterChanging -or !$_.data.IsClosed -or !$_.data.IsDisposed}).Count){throw 'Invalid closed observations'}
$late=@($events|Where-Object {$_.kind -in @('focus','activation-callback') -and ($_.data.IsClosed -or $_.data.IsDisposed)})
if($late.Count){throw 'Late focus/activation callback'}
$settled=@($events|Where-Object {$_.kind -eq 'snapshot' -and $_.data.name -eq 'rapid-settled'})
if($settled.Count -ne 3 -or @($settled|Where-Object {$_.data.mode -ne 'Overlapped' -or $_.data.IsPresenterChanging -or $_.data.top -or $_.data.x -ne 500 -or $_.data.y -ne 140 -or $_.data.width -ne 840 -or $_.data.height -ne 570}).Count){throw 'Invalid final geometry/presenter/topmost'}
if($RequireFocus -and @($events|Where-Object kind -eq focus).Count -lt 3){throw 'Missing live focus positive control'}
foreach($name in @('initial','Top','Full','Compact','Normal','complete')){
    if(!(Test-Path "$Evidence/$name.png") -or (Get-Item "$Evidence/$name.png").Length -eq 0){throw 'Missing screenshot'}
}
@{cases=$rows.Count;passed=18;failed=0;closedObservations=$closed.Count;liveFocusEvents=@($events|Where-Object kind -eq focus).Count;
    lateFocusOrActivation=$late.Count;unexpectedExceptions=0;events=$events.Count;source='results.json + events.jsonl';visualReview='See ui-54.md; not inferred from JSON'} |
    ConvertTo-Json|Set-Content "$Evidence/verification.json"
Get-Content "$Evidence/verification.json"
