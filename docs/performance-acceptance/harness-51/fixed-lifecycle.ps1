# Fresh host. Second unload deliberately has a real in-flight request, unlike the original defect-dependent repro.
param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-fixed-d19'
$Evidence=[IO.Path]::GetFullPath($Evidence);New-Item -ItemType Directory $Evidence|Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run.ps1",[ref]$null,[ref]$null)
foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($function.Extent.Text))}
try {
    $owned=Get-Process -Id $AppPid;if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned guard failed'}
    Ui @('wait-for','SearchInput','-t','10000')|Out-Null
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-fixed-d19',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'FirstUnloadCancelsFocusRequest' {
        Ui @('focus','SearchInput')|Out-Null;Request '<recommend>'
        Command 'Detach';Await {-not (State).loaded} 'first unload'
        Command 'ReleaseOldest';Fence 'first-focus-late'
        Check (@(Events|Where-Object {$_.kind -eq 'cancel' -and $_.data.Id -eq 1}).Count -eq 1) 'Focus cancellation not exactly once'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Focus late completion revived suggestions'
        Command 'Attach';Await {(State).loaded} 'first remount'
    }
    Case 'SecondUnloadRejectsLateResponse' {
        Ui @('focus','SearchInput')|Out-Null;Keys 'vk=0x10';Keys 'inflight';Request 'inflight'
        Command 'Detach';Await {-not (State).loaded} 'second unload'
        Check (@(Events|Where-Object {$_.kind -eq 'cancel' -and $_.data.keyword -eq 'inflight'}).Count -eq 1) 'In-flight request not cancelled exactly once'
        Command 'ReleaseNewest';Fence 'second-inflight-late';Capture 'second-inflight-late'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Late result published while unloaded'
        Check (@(Events|Where-Object {$_.kind -eq 'state' -and $_.data.items -contains 'inflight result'}).Count -eq 0) 'Transient stale publication'
        Command 'Attach';Await {(State).loaded} 'second remount';Fence 'second-remount'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Remount revived stale result'
    }
    Case 'ThirdUnloadCancelsDebounceAndResumes' {
        Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a';Keys 'q'
        # Separately preserved lifecycle/runner.ps1 covers input whose delayed TextChanged arrives AFTER unload.
        Await {@(Events|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}).Count -eq 1} 'q UserInput before unload'
        Command 'Detach';Await {-not (State).loaded} 'third unload';Fence 'third-unload';Capture 'third-unload'
        $ev=Events;$input=$ev|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}|Select-Object -Last 1
        $unload=$ev|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'unloaded'}|Select-Object -Last 1
        Check ($input -and $unload.ms-$input.ms -ge 0 -and $unload.ms-$input.ms -lt 250) 'Missed debounce precondition'
        Check (@($ev|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'q'}).Count -eq 0) 'Third unload dispatched debounce'
        Command 'Attach';Await {(State).loaded} 'third remount'
        Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a';Keys 'resumed';Request 'resumed';Command 'ReleaseNewest';Fence 'third-resumed';Capture 'third-resumed'
        Check ((State).items -contains 'resumed result' -and (State).open) 'Normal input failed after third remount'
    }
    Case 'SelectionTimerUnloadAndLateFocusStayCleared' {
        Check ((State).loaded -and (State).open -and (State).items -contains 'resumed result') 'Selection precondition missing'
        Keys 'down enter';Command 'Detach';Await {-not (State).loaded} 'selection unload'
        Fence 'selection-timer-unloaded';Check ((State).items.Count -eq 0 -and -not (State).open) 'Selection timer revived result'
        Command 'Attach';Await {(State).loaded} 'selection remount'
        Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a delete'
        Ui @('focus','ReleaseNewest')|Out-Null;Ui @('focus','SearchInput')|Out-Null
        $req=Events|Where-Object kind -eq request|Select-Object -Last 1
        Check ($req.data.keyword -eq '<recommend>') 'Missing late focus precondition'
        Command 'Detach';Await {-not (State).loaded} 'late focus unload';Command 'ReleaseNewest';Fence 'late-focus';Capture 'late-focus'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Late focus revived result'
        Command 'Attach';Await {(State).loaded} 'final remount';Fence 'final-remount';Capture 'final-remount'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Final remount revived result'
    }
} catch {$results.Add(@{name='Runner';passed=$false;error="$_"})}
finally {
    Copy-Item "$Layout/events.jsonl" "$Evidence/events.jsonl"
    if($writer){$writer.Dispose()};if($pipe){$pipe.Dispose()}
    & winapp ui yield --json|Set-Content "$Evidence/yield.json"
    @{checks=$results.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
}
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
if(@($results|Where-Object {-not $_.passed}).Count){exit 1}
