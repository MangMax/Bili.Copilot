# Continue the terminal state of boundaries.ps1 without repeating passing scenarios.
param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-b48a'
$Evidence=[IO.Path]::GetFullPath($Evidence);New-Item -ItemType Directory -Force $Evidence|Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run.ps1",[ref]$null,[ref]$null)
foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($function.Extent.Text))}
try{
    $owned=Get-Process -Id $AppPid;if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned guard failed'}
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-b48a',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'ImeMouseSelectionExactNode' {
        Check ((State).text -eq '你好' -and (State).open) 'Expected existing IME result state'
        $before=@(Events|Where-Object kind -eq request).Count
        ClickSuggestion;Fence 'ime-mouse-exact';Capture 'ime-mouse-exact'
        Check ((State).text -eq '你好 result' -and -not (State).open) 'IME selection did not write text and close popup'
        Check (@(Events|Where-Object kind -eq request).Count -eq $before) 'IME selection dispatched another request'
        Check (@(Events|Where-Object {$_.kind -eq 'navigation-adapter' -and $_.data.keyword -eq '你好'}).Count -eq 1) 'Wrong selected IME keyword'
    }
    Case 'RepeatedUnloadPreventsDebounceDispatch' {
        Ui @('focus','SearchInput')|Out-Null
        Keys 'ctrl+a delete';Keys 'vk=0x10';Keys 'q'
        Await {@(Events|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}).Count -eq 1} 'q input'
        Command 'Detach';Await {-not (State).loaded} 'repeat unload';Fence 'repeat-unload';Capture 'repeat-unload'
        $events=Events
        # Keep the expected behavior assertion; the known production failure must remain red.
        Check (@($events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'q'}).Count -eq 0) 'PRODUCTION: q request dispatched after repeated unload'
    }
    Case 'RepeatedUnloadRejectsInFlightCompletion' {
        Check (-not (State).loaded) 'Precondition: control must be unloaded'
        Check (@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'q'}).Count -eq 1) 'Precondition: q request must exist'
        Command 'ReleaseNewest';Fence 'repeat-late';Capture 'repeat-late'
        Check ((State).items.Count -eq 0) 'PRODUCTION: response published to unloaded control after repeated unload'
    }
    Command 'Attach';Fence 'repeat-return';Capture 'repeat-return'
}catch{$results.Add(@{name='Runner';passed=$false;error="$_"})}
finally{
    Copy-Item "$Layout/events.jsonl" "$Evidence/events.jsonl"
    if($writer){$writer.Dispose()};if($pipe){$pipe.Dispose()}
    & winapp ui yield --json|Set-Content "$Evidence/yield.json"
    @{checks=$results.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
}
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
if(@($results|Where-Object {-not $_.passed}).Count){exit 1}
