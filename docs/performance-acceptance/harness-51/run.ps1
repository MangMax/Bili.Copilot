param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-b48a'
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Force $Evidence | Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
function Ui([string[]]$Arguments){$out=& winapp ui @Arguments -a $AppPid --json; if($LASTEXITCODE -ne 0){throw "UI command failed: $Arguments $out"}; $out}
function Events { @(Get-Content "$Layout/events.jsonl" | ForEach-Object {$_|ConvertFrom-Json}) }
function Await([scriptblock]$Condition,[string]$Description){
    $deadline=[Diagnostics.Stopwatch]::StartNew()
    do {if(& $Condition){return};[Threading.Thread]::Sleep(20)}while($deadline.ElapsedMilliseconds -lt 5000)
    throw "Timed out: $Description"
}
function Command([string]$Value){$writer.WriteLine($Value);$writer.Flush();Await { @(Events | Where-Object {$_.kind -eq 'pipe' -and $_.data -eq $Value}).Count -gt 0 } "command $Value"}
function Marker([string]$Name){Command $Name;Await {@(Events | Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq $Name}).Count -gt 0} $Name}
function State { (Events | Where-Object kind -eq state | Select-Object -Last 1).data }
function Check([bool]$Satisfied,[string]$Message){if(-not $Satisfied){throw $Message}}
function Request([string]$Keyword){Await {@(Events | Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq $Keyword}).Count -gt 0} "request $Keyword"}
function Keys([string]$Value){Ui @('send-keys',$Value,'--via','send-input')|Out-Null}
function Nodes($Root){foreach($node in @($Root)){if($null -ne $node){$node;Nodes $node.children;Nodes $node.elements}}}
function ClickSuggestion {
    $tree=Ui @('inspect','--depth','12')|ConvertFrom-Json
    $item=@(Nodes $tree.windows|Where-Object {$_.className -eq 'ListViewItem' -and -not $_.isOffscreen}|Select-Object -ExpandProperty selector -Unique)
    Check ($item.Count -eq 1) 'Expected one visible suggestion item for mouse selection'
    Ui @('click',$item[0])|Out-Null
}
function Capture([string]$Name){
    $path=Join-Path $Evidence "$Name.png";if(Test-Path $path){throw 'Evidence already exists'}
    Ui @('screenshot','-w',"$script:hwnd",'--capture-screen','--output',$path)|Out-Null
    Ui @('inspect','--depth','12')|Set-Content "$Evidence/$Name.uia.json"
    $captures.Add($path)
}
function Fence([string]$Name){$before=@(Events | Where-Object {$_.kind -eq 'state' -and $_.data.condition -like 'fence*'}).Count;Command 'Fence';Await {@(Events | Where-Object {$_.kind -eq 'state' -and $_.data.condition -like 'fence*'}).Count -gt $before} $Name;Marker $Name}
function Case([string]$Name,[scriptblock]$Body){try{Marker "begin-$Name";&$Body;$results.Add(@{name=$Name;passed=$true})}catch{$results.Add(@{name=$Name;passed=$false;error="$_"});Capture "$Name-failure"}}
try {
    $owned=Get-Process -Id $AppPid
    if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned process guard failed'}
    Ui @('wait-for','SearchInput','-t','10000')|Out-Null
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-b48a',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'BurstFinalPopup' {
        Ui @('focus','SearchInput')|Out-Null
        Keys 'vk=0x10' # Installed IME: switch this focused input from Chinese to English.
        Keys 'bili';Request 'bili';Command 'ReleaseNewest'
        Await {(State).items -contains 'bili result'} 'final suggestion'
        Fence 'burst-settled';Capture 'burst-final'
        Check ((State).open) 'Final suggestion exists but real popup is closed'
        $ev=Events;$req=@($ev|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'bili'})
        $input=$ev|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq 'bili'}|Select-Object -Last 1
        Check ($req.Count -eq 1 -and $req[0].ms-$input.ms -ge 240) 'Expected one final request after 250ms (10ms observer tolerance)'
    }
    Case 'SelectionDoesNotRequery' {
        $before=@(Events|Where-Object kind -eq request).Count
        Keys 'down enter';Fence 'selection-settled';Capture 'selection'
        Check (@(Events|Where-Object kind -eq submit|Where-Object {$_.data.chosen}).Count -gt 0) 'No chosen-suggestion submission'
        Check (@(Events|Where-Object kind -eq request).Count -eq $before) 'Selection triggered another request'
        Check (-not (State).open) 'Selection popup reopened'
    }
    Case 'OutOfOrderOldNeverPublishes' {
        Keys 'ctrl+a';Keys 'old';Request 'old'
        Keys 'ctrl+a';Keys 'new';Request 'new';Command 'ReleaseNewest'
        Await {(State).items -contains 'new result'} 'new result';Capture 'new-result'
        # Oldest is the cancelled initial focus recommendation, then old keyword.
        Command 'ReleaseOldest';Command 'ReleaseOldest';Fence 'old-settled';Capture 'old-rejected'
        Check ((State).items -contains 'new result') 'Old completion replaced newest suggestion'
        Check (@(Events|Where-Object {$_.kind -eq 'state' -and $_.data.items -contains 'old result'}).Count -eq 0) 'Old suggestion was published transiently'
        Check (@(Events|Where-Object {$_.kind -eq 'cancel' -and $_.data.keyword -eq 'old'}).Count -eq 1) 'Old request not cancelled'
    }
    Case 'ClearRejectsLateResult' {
        Keys 'ctrl+a';Keys 'clearflight';Request 'clearflight';Keys 'ctrl+a delete'
        Command 'ReleaseNewest';Fence 'clear-settled';Capture 'clear'
        Check ((State).text -eq '' -and (State).items.Count -eq 0 -and -not (State).open) 'Clear allowed late suggestion or popup'
    }
    Case 'EmptyFocusRecommendations' {
        Ui @('focus','ReleaseNewest')|Out-Null;Ui @('focus','SearchInput')|Out-Null
        Await {@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq '<recommend>'}).Count -ge 2} 'new recommendation request'
        Command 'ReleaseNewest';Fence 'recommend-settled';Capture 'recommend'
        Check ((State).items -contains 'fixture recommendation' -and (State).open) 'Empty focus recommendation popup missing'
    }
    Case 'UnloadRemountRejectsLateResult' {
        Keys 'pending';Request 'pending';Command 'Detach'
        Await {-not (State).loaded} 'unload';Command 'Attach';Await {(State).loaded} 'reload'
        Command 'ReleaseNewest';Fence 'remount-settled';Capture 'remount'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Late result reopened remounted control'
        Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a';Keys 'resumed';Request 'resumed';Command 'ReleaseNewest'
        Fence 'resumed-settled';Capture 'resumed'
        Check ((State).items -contains 'resumed result' -and (State).open) 'Remounted control did not resume'
    }
    Case 'ChineseImeComposition' {
        Keys 'ctrl+a delete'
        Keys 'vk=0x10' # Restore Chinese mode for actual composition.
        # Real virtual keys pass through the installed IME. No UIA SetValue or Unicode injection qualifies.
        Keys 'vk=0x4E vk=0x49 vk=0x48 vk=0x41 vk=0x4F'
        Marker 'ime-composing';Capture 'ime-composing'
        Keys 'space';Marker 'ime-committed';Capture 'ime-committed'
        $ev=Events
        Check (@($ev|Where-Object kind -eq composition-start).Count -gt 0) 'BLOCKED: installed IME did not emit real composition-start from virtual-key input'
        Check (@($ev|Where-Object kind -eq composition-end).Count -gt 0) 'BLOCKED: no real composition-end'
        Check ((State).text -match '[\p{IsCJKUnifiedIdeographs}]') 'IME did not commit Chinese text'
        Request (State).text;Command 'ReleaseNewest';Fence 'ime-result';Capture 'ime-result'
        Check ((State).open) 'Committed IME suggestion popup missing'
    }
} catch {$results.Add(@{name='Runner';passed=$false;error="$_"})}
finally {
    Copy-Item "$Layout/events.jsonl" "$Evidence/events.jsonl"
    if($writer){$writer.Dispose()};if($pipe){$pipe.Dispose()}
    & winapp ui yield --json | Set-Content "$Evidence/yield.json"
    @{checks=$results.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
}
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
if(@($results|Where-Object {-not $_.passed}).Count){exit 1}
