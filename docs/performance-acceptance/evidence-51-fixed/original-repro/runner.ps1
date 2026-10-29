# Minimal fresh-process reproduction. Expected to fail until production lifecycle handling is repaired.
param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-fixed-d19'
$Evidence=[IO.Path]::GetFullPath($Evidence);New-Item -ItemType Directory -Force $Evidence|Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run.ps1",[ref]$null,[ref]$null)
foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($function.Extent.Text))}
try{
    $owned=Get-Process -Id $AppPid;if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned guard failed'}
    Ui @('wait-for','SearchInput','-t','10000')|Out-Null
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-fixed-d19',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'FirstUnloadCancelsFocusRequest' {
        Ui @('focus','SearchInput')|Out-Null;Request '<recommend>'
        Command 'Detach';Await {-not (State).loaded} 'first unload';Fence 'first-unload'
        Check (@(Events|Where-Object {$_.kind -eq 'cancel' -and $_.data.Id -eq 1}).Count -eq 1) 'First unload did not cancel request'
        Command 'ReleaseOldest';Command 'Attach';Await {(State).loaded} 'first remount'
    }
    Case 'SecondUnloadCancelsDebounce' {
        Ui @('focus','SearchInput')|Out-Null;Keys 'vk=0x10';Keys 'q'
        Await {@(Events|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}).Count -eq 1} 'q UserInput'
        Command 'Detach';Await {-not (State).loaded} 'second unload';Fence 'second-unload';Capture 'second-unload'
        $ev=Events;$input=$ev|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'q'}|Select-Object -Last 1
        $unload=$ev|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'unloaded'}|Select-Object -Last 1
        Check ($unload.ms-$input.ms -lt 250) 'Harness missed the pre-dispatch unload window'
        Check (@($ev|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'q'}).Count -eq 0) 'PRODUCTION: request q dispatched while unloaded'
    }
    Case 'SecondUnloadRejectsLateResponse' {
        Check (-not (State).loaded) 'Precondition: actually unloaded'
        Check (@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'q'}).Count -eq 1) 'Precondition: request q observed'
        Command 'ReleaseNewest';Fence 'second-late';Capture 'second-late'
        Check ((State).items.Count -eq 0) 'PRODUCTION: q result published while unloaded'
    }
    Command 'Attach';Await {(State).loaded} 'second remount';Fence 'second-remount';Capture 'second-remount'
}catch{$results.Add(@{name='Runner';passed=$false;error="$_"})}
finally{
    Copy-Item "$Layout/events.jsonl" "$Evidence/events.jsonl"
    if($writer){$writer.Dispose()};if($pipe){$pipe.Dispose()}
    & winapp ui yield --json|Set-Content "$Evidence/yield.json"
    @{checks=$results.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
}
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
if(@($results|Where-Object {-not $_.passed}).Count){exit 1}

