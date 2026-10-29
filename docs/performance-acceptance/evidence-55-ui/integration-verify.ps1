param([string]$Evidence = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
function Check($value, $message) { if (!$value) { throw $message } }
function Json($path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
$repo = (Resolve-Path "$PSScriptRoot/../../..").Path
& "$repo/docs/performance-acceptance/harness-55/verify.ps1" -Evidence $Evidence
$names = @('SuccessOpen','SuccessDisabled','NonzeroExit','StartFailure','CopyOnlyNoProcess','CloseWindowHostAlive','CloseLastWindowChildSurvives')
foreach ($name in $names) {
    $last = $name -eq 'CloseLastWindowChildSurvives'
    $phase = if ($last) { 'last2' } else { 'run3' }
    $prefix = if ($last) { 'last2' } else { 'r3' }
    $root = "$Evidence/$phase/localstate/$prefix-$name"
    $events = @(Get-Content "$root/events.jsonl" | ForEach-Object { $_ | ConvertFrom-Json })
    $created = @($events | Where-Object kind -eq 'process-created')
    $disposed = @($events | Where-Object kind -eq 'process-disposed')
    $folders = @($events | Where-Object kind -eq 'folder-adapter')
    $tips = @($events | Where-Object kind -eq 'tip')
    $logs = @($events | Where-Object kind -eq 'production-log')
    $copy = $name -eq 'CopyOnlyNoProcess'
    Check ($created.Count -eq $(if ($copy) { 0 } else { 1 })) "$name process creation"
    Check ($tips.Count -eq $(if ($copy) { 1 } else { 0 })) "$name unexpected tip"
    $failure = $name -in @('NonzeroExit','StartFailure')
    Check ($logs.Count -eq $(if ($failure) { 1 } else { 0 })) "$name log count"
    if ($failure) {
        $level = if ($name -eq 'NonzeroExit') { 'Warning' } else { 'Error' }
        $message = if ($name -eq 'NonzeroExit') { 'Download process exited with code 7.' } else { 'Download process failed (Win32Exception).' }
        Check ($logs[0].data.level -eq $level -and $logs[0].data.message -ceq $message) "$name exact failure log"
    }
    if ($copy -or $name -eq 'StartFailure') {
        Check (!(Test-Path "$root/child.json")) "$name unexpected child"
        if ($copy) {
            Check ($disposed.Count -eq 0) 'Copy disposed a process'
            $clip = @($events | Where-Object kind -eq 'clipboard-owned')
            Check ($clip.Count -eq 1 -and $clip[0].data.matches -and $tips[0].data.value -eq '(Copied, Success)') 'Copy clipboard/tip'
        } else {
            Check ($disposed.Count -eq 1 -and !$disposed[0].data.handleCaptured -and $null -eq $disposed[0].data.handleClosed) 'Startup failure disposal'
        }
    } else {
        $child = Json "$root/child.json"
        $end = Json "$root/child-exit.json"
        $code = if ($name -eq 'NonzeroExit') { 7 } else { 0 }
        Check ($child.pid -eq $end.pid -and $end.code -eq $code -and (Test-Path "$root/release")) "$name child termination"
        if ($last) {
            Check ($disposed.Count -eq 0 -and $folders.Count -eq 0) 'Exited host continuation'
            $external = Json "$Evidence/last2/last-window-external-observation.json"
            Check ($external.hostExited -and $external.childAlive -and $external.childPid -eq $child.pid -and $external.hostPid -eq $events[0].pid -and $null -eq $external.hostExitCode) 'External exit observation'
        } else {
            Check ([DateTimeOffset]$end.exitingUtc -le [DateTimeOffset]$disposed[0].utc) "$name child/disposal order"
            if ($folders.Count) { Check ([array]::IndexOf($events,$disposed[0]) -lt [array]::IndexOf($events,$folders[0])) "$name folder/disposal order" }
        }
        if ($name -eq 'CloseWindowHostAlive') {
            $external = Json "$Evidence/run3/close-window-external-observation.json"
            $closed = @($events | Where-Object kind -eq 'main-closed')
            Check ($external.hostAlive -and $external.childAlive -and $external.mainClosed -and $external.childPid -eq $child.pid -and $external.hostPid -eq $events[0].pid) 'External host-alive observation'
            Check ($closed.Count -eq 1 -and [DateTimeOffset]$closed[0].utc -lt [DateTimeOffset]$disposed[0].utc) 'Close-before-disposal order'
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath $root -File) {
        $final = "$Evidence/final-localstate/$prefix-$name/$($file.Name)"
        Check ((Get-FileHash $file.FullName).Hash -eq (Get-FileHash $final).Hash) "$name final snapshot mismatch"
    }
    "PASS raw cross-check: $name"
}
foreach ($item in (Json "$Evidence/production-hashes.json")) {
    Check ((Get-FileHash "$repo/$($item.path)" -Algorithm SHA256).Hash -eq $item.sha256) "Source hash mismatch: $($item.path)"
}
'PASS production source SHA256: 8/8'
$cleanup = Json "$Evidence/cleanup.json"
Check ($cleanup.ownedPackageGone -and $cleanup.noKillUsed -and @($cleanup.ownedProcessesAlive).Count -eq 0) 'Historical cleanup record'
Check ((Json "$Evidence/run3/yield.json").released -and (Json "$Evidence/last2/yield.json").released) 'Phase yield record'
'PASS recorded phase yield and owned cleanup; cleanup-yield itself reports released=false'
'Scope: offline record validation only; no fresh UI, shell, child launch or lifecycle experiment.'
