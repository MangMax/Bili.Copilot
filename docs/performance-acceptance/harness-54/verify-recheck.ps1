param([Parameter(Mandatory)][string]$Evidence,[switch]$RequireFocus)
$ErrorActionPreference='Stop'
$rows=@(Get-Content "$Evidence/results.json" -Raw|ConvertFrom-Json)
$events=@(Get-Content "$Evidence/events.jsonl"|ForEach-Object {$_|ConvertFrom-Json})
$names=@('RapidFinalNormal','RestoreBothModesAndSavedGeometry','CloseDuring500ms','CloseBeforeQueuedFocusAndTitle','CloseDuringFirstTitleDelay','CloseDuringSecondTitleDelay')
$expected=@(foreach($round in 1..3){foreach($name in $names){"${name}_R$round"}})
@{cases=$rows.Count;passed=@($rows|Where-Object status -eq PASS).Count;failed=@($rows|Where-Object status -ne PASS).Count;
    closedObservations=@($events|Where-Object kind -eq 'closed-observation').Count;liveFocusEvents=@($events|Where-Object {$_.kind -eq 'focus' -and !$_.data.IsClosed -and !$_.data.IsDisposed}).Count;
    lateFocusEvents=@($events|Where-Object {$_.kind -eq 'focus' -and ($_.data.IsClosed -or $_.data.IsDisposed)}).Count;
    events=$events.Count;visualReview='See ui-54-recheck.md; JSON is not visual acceptance'}|ConvertTo-Json|Set-Content "$Evidence/observed-summary.json"
if($rows.Count -ne 18 -or @($rows|Where-Object status -ne PASS).Count -or (Compare-Object $expected @($rows.name))){throw 'Missing, duplicate or failing cases'}
if(@($events|Where-Object {$_.kind -in @('first-chance','unhandled','command-failed','case-failed')}).Count){throw 'Unexpected exceptions'}
$closed=@($events|Where-Object kind -eq 'closed-observation')
if($closed.Count -ne 12 -or @($closed|Where-Object {$_.data.nativeLate -ne 0 -or $_.data.gettingLate -ne 0 -or $_.data.activationLate -ne 0 -or $_.data.LateHeaderReads -ne 0 -or !$_.data.sessionClosed -or $_.data.hwndAlive -or $_.data.AfterDestroyCalls -ne 0 -or $_.data.IsPresenterChanging -or !$_.data.IsClosed -or !$_.data.IsDisposed}).Count){throw 'Invalid closed observations'}
$late=@($events|Where-Object {$_.kind -in @('getting-focus','activation-callback') -and ($_.data.IsClosed -or $_.data.IsDisposed)})
if($late.Count){throw 'Late focus request/activation callback'}
$nativeLate=@($events|Where-Object {$_.kind -eq 'native-request' -and $_.data.closed})
$headerLate=@($events|Where-Object {$_.kind -eq 'header-read' -and $_.data.closed})
if($nativeLate.Count -or $headerLate.Count){throw 'Post-close native request/header read'}
$positive=@($events|Where-Object kind -eq 'live-boundaries')
if($positive.Count -ne 3 -or @($positive|Where-Object {$_.data.nativeResetLive -le 0 -or $_.data.activationLive -le 0 -or $_.data.LiveHeaderReads -le 0 -or $_.data.liveFocus -le 0}).Count){throw 'Missing live boundary controls'}
$notifications=@(foreach($e in @($events|Where-Object {$_.kind -eq 'focus' -and ($_.data.IsClosed -or $_.data.IsDisposed)})){
    $priorGetting=@($events|Where-Object {$_.kind -eq 'getting-focus' -and $_.data.id -eq $e.data.id -and $_.ms -lt $e.ms -and !$_.data.IsClosed -and !$_.data.IsDisposed})
    $priorNative=@($events|Where-Object {$_.kind -eq 'native-request' -and $_.data.id -eq $e.data.id -and $_.data.api -eq 'SetFocus' -and $_.ms -lt $e.ms -and !$_.data.closed})
    if(!$priorGetting.Count -or !$priorNative.Count -or $e.data.hwndAlive -or $e.data.stack -match 'PlayerWindow.*(ResetFocus|Show)'){throw 'Unclassified post-close focus notification'}
    @{id=$e.data.id;ms=$e.ms;classification='deferred notification; prior live request observed';priorGettingMs=$priorGetting[-1].ms;priorNativeRequestMs=$priorNative[-1].ms;hwndAlive=$e.data.hwndAlive}
})
$notifications|ConvertTo-Json -Depth 5|Set-Content "$Evidence/notification-classification.json"
$settled=@($events|Where-Object {$_.kind -eq 'snapshot' -and $_.data.name -eq 'rapid-settled'})
if($settled.Count -ne 3 -or @($settled|Where-Object {$_.data.mode -ne 'Overlapped' -or $_.data.IsPresenterChanging -or $_.data.top -or $_.data.x -ne 500 -or $_.data.y -ne 140 -or $_.data.width -ne 840 -or $_.data.height -ne 570}).Count){throw 'Invalid final geometry/presenter/topmost'}
if($RequireFocus -and @($events|Where-Object kind -eq focus).Count -lt 3){throw 'Missing live focus positive control'}
foreach($name in @('initial','Top','Full','Compact','Normal','complete')){
    if(!(Test-Path "$Evidence/$name.png") -or (Get-Item "$Evidence/$name.png").Length -eq 0){throw 'Missing screenshot'}
}
@{cases=$rows.Count;passed=18;failed=0;closedObservations=$closed.Count;liveFocusEvents=@($events|Where-Object {$_.kind -eq 'focus' -and !$_.data.IsClosed -and !$_.data.IsDisposed}).Count;
    lateGettingFocusOrActivation=$late.Count;lateNativeRequests=$nativeLate.Count;lateHeaderReads=$headerLate.Count;lateGotFocusNotifications=@($events|Where-Object {$_.kind -eq 'focus' -and ($_.data.IsClosed -or $_.data.IsDisposed)}).Count;unexpectedExceptions=0;events=$events.Count;source='results.json + events.jsonl';visualReview='See ui-54-recheck.md; not inferred from JSON'} |
    ConvertTo-Json|Set-Content "$Evidence/verification.json"
Get-Content "$Evidence/verification.json"
