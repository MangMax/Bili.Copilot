# Fresh owned host: require the original late UserInput ordering, not normal cancellation.
param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-late-751'
$Evidence=[IO.Path]::GetFullPath($Evidence);New-Item -ItemType Directory $Evidence|Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run.ps1",[ref]$null,[ref]$null)
foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($function.Extent.Text))}
try {
    $owned=Get-Process -Id $AppPid;if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned guard failed'}
    Ui @('wait-for','SearchInput','-t','10000')|Out-Null
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-late-751',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'LateUserInputAfterUnloadStartsNoWork' {
        Ui @('focus','SearchInput')|Out-Null;Request '<recommend>'
        Keys 'vk=0x10';Command 'ArmLateInput';Keys 'q'
        Await {@(Events|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q' -and $_.data.reason -eq 'UserInput'}).Count -eq 1} 'real q UserInput'
        Fence 'late-input-fenced';Capture 'late-input-fenced'
        $ev=Events
        $keyword=$ev|Where-Object {$_.kind -eq 'keyword' -and $_.data.keyword -eq 'q'}|Select-Object -First 1
        $unload=$ev|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'unloaded'}|Select-Object -First 1
        $input=$ev|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}|Select-Object -First 1
        Check ($keyword -and $keyword.data.loaded -and $unload -and $input -and $keyword.ms -lt $unload.ms -and $unload.ms -lt $input.ms -and -not $input.data.loaded) 'Required Keyword -> Unloaded -> real UserInput ordering missing'
        Check (@($ev|Where-Object {$_.kind -eq 'request' -and $_.ms -gt $unload.ms}).Count -eq 0) 'New request after unload'
        Check (@($ev|Where-Object {$_.kind -eq 'state' -and $_.ms -gt $unload.ms -and $_.data.items.Count -gt 0}).Count -eq 0) 'Transient unloaded publication'
        Command 'ReleaseOldest';Fence 'late-input-old-response'
        Check (-not (State).loaded -and (State).items.Count -eq 0 -and -not (State).open) 'Late response revived unloaded control'
        @{keyword=$keyword;unloaded=$unload;input=$input;final=(State)}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/ordering.json"
    }
    Case 'RealInputAndEmptyFocusAfterRemount' {
        Command 'Attach';Await {(State).loaded} 'remount'
        Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a';Keys 'resumed';Request 'resumed'
        Command 'ReleaseNewest';Fence 'resumed';Capture 'resumed'
        Check ((State).open -and (State).items -contains 'resumed result') 'Remount input blocked'
        Keys 'ctrl+a delete';Fence 'clear'
        Ui @('focus','ReleaseNewest')|Out-Null
        $before=@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq '<recommend>'}).Count
        Ui @('focus','SearchInput')|Out-Null
        Await {@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq '<recommend>'}).Count -eq $before+1} 'fresh empty focus'
        Command 'ReleaseNewest';Fence 'empty-focus';Capture 'empty-focus'
        Check ((State).open -and (State).items -contains 'fixture recommendation') 'Empty focus blocked'
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
