param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='acceptance-51-b48a'
$Evidence=[IO.Path]::GetFullPath($Evidence);New-Item -ItemType Directory -Force $Evidence|Out-Null
$results=[Collections.Generic.List[object]]::new();$captures=[Collections.Generic.List[string]]::new()
# Share only runner helpers, without re-running the first suite.
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run.ps1",[ref]$null,[ref]$null)
foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($function.Extent.Text))}
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class Input51 {
 [StructLayout(LayoutKind.Sequential)] struct INPUT {public uint type;public UNION u;}
 [StructLayout(LayoutKind.Explicit)] struct UNION {[FieldOffset(0)] public KEY k;[FieldOffset(0)] public MOUSE m;}
 [StructLayout(LayoutKind.Sequential)] struct KEY {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct MOUSE {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [DllImport("user32.dll")] static extern uint SendInput(uint n,INPUT[] inputs,int size);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 public static void Key(int hwnd,ushort key){if(GetForegroundWindow()!=new IntPtr(hwnd))throw new Exception("Owned HWND is not foreground");var a=new INPUT[2];a[0].type=a[1].type=1;a[0].u.k.vk=a[1].u.k.vk=key;a[1].u.k.flags=2;if(SendInput(2,a,Marshal.SizeOf<INPUT>())!=2)throw new Exception("SendInput failed");}
}
'@
function NativeLetter([char]$Letter,[string]$Expected){[Input51]::Key($script:hwnd,[ushort][char]([string]$Letter).ToUpperInvariant());Await {@(Events|Where-Object {$_.kind -eq 'text' -and $_.data.Text -eq $Expected}).Count -gt 0} "native key $Expected"}
try {
    $owned=Get-Process -Id $AppPid;if($AppPid -eq 9608 -or -not $owned.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned guard failed'}
    Ui @('wait-for','SearchInput','-t','10000')|Out-Null
    $script:hwnd=(Ui @('list-windows')|ConvertFrom-Json|Where-Object title -like 'Offline search acceptance 51*'|Select-Object -First 1).hwnd
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','bili51-b48a',[IO.Pipes.PipeDirection]::Out);$pipe.Connect(3000);$writer=[IO.StreamWriter]::new($pipe)
    Case 'FocusLateAcrossRemount' {
        Ui @('focus','SearchInput')|Out-Null;Request '<recommend>'
        Command 'Detach';Await {-not (State).loaded} 'detached';Command 'Attach';Await {(State).loaded} 'attached'
        Command 'ReleaseOldest';Fence 'focus-late-settled';Capture 'focus-late'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Stale focus completion reopened remounted popup'
    }
    Case 'MultipleInputEventsWithinDebounce' {
        Ui @('focus','SearchInput')|Out-Null;Keys 'vk=0x10'
        NativeLetter 'r' 'r';NativeLetter 'a' 'ra';NativeLetter 'p' 'rap';NativeLetter 'i' 'rapi';NativeLetter 'd' 'rapid'
        Request 'rapid';Command 'ReleaseNewest';Fence 'rapid-settled';Capture 'rapid'
        $ev=Events;$inputs=@($ev|Where-Object {$_.kind -eq 'text' -and $_.data.Text -in @('r','ra','rap','rapi','rapid')})
        Check ($inputs.Count -eq 5) 'Expected five distinct real UserInput events'
        for($i=1;$i -lt $inputs.Count;$i++){Check ($inputs[$i].ms-$inputs[$i-1].ms -lt 250) 'Input gap exceeded debounce window'}
        $requests=@($ev|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -in @('r','ra','rap','rapi','rapid')})
        Check ($requests.Count -eq 1 -and $requests[0].data.keyword -eq 'rapid') 'Intermediate input dispatched a request'
        Check ($requests[0].ms-$inputs[-1].ms -ge 240) 'Final request before quiet window'
        Check ((State).items -contains 'rapid result' -and (State).open) 'Final popup absent'
    }
    Case 'ClearDuringDebounce' {
        Keys 'ctrl+a delete';NativeLetter 'x' 'x'
        # Backspace is injected immediately after observed UserInput; no CLI startup in the 250ms interval.
        [Input51]::Key($script:hwnd,8)
        Await {(State).text -eq ''} 'clear';Fence 'debounce-clear';Capture 'debounce-clear'
        Check (@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'x'}).Count -eq 0) 'Cleared debounce dispatched x'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Clear reopened popup'
    }
    Case 'UnloadDuringDebounce' {
        NativeLetter 'z' 'z';Command 'Detach';Await {-not (State).loaded} 'unload before delay';Fence 'debounce-unload'
        Command 'Attach';Await {(State).loaded} 'reattach';Capture 'debounce-unload'
        Check (@(Events|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'z'}).Count -eq 0) 'Unloaded debounce dispatched z'
        Check ((State).items.Count -eq 0 -and -not (State).open) 'Unloaded debounce reopened popup'
    }
    Case 'RegionChoicesAndAdapterRouting' {
        Command 'Region';Ui @('focus','SearchInput')|Out-Null;Keys 'ctrl+a';Keys 'region';Fence 'region-options';Capture 'region-options'
        Check ((State).items.Count -eq 2 -and (State).open) 'Missing global and regional options'
        Keys 'down down enter';Fence 'region-selected';Capture 'region-selected'
        Check (@(Events|Where-Object {$_.kind -eq 'navigation-adapter' -and $_.data.route -eq 'region' -and $_.data.keyword -eq 'region'}).Count -eq 1) 'Wrong regional adapter route or keyword'
        Check (-not (State).open) 'Regional selection reopened popup'
        Command 'Global'
    }
    Case 'ImeMouseSelectionDoesNotRequery' {
        Keys 'ctrl+a delete';Keys 'vk=0x10';Keys 'vk=0x4E vk=0x49 vk=0x48 vk=0x41 vk=0x4F'
        Await {@(Events|Where-Object kind -eq composition-start).Count -gt 0} 'real IME composition';Capture 'ime-candidate'
        Keys 'space';Request '你好';Command 'ReleaseNewest';Fence 'ime-ready';Capture 'ime-popup'
        $before=@(Events|Where-Object kind -eq request).Count
        ClickSuggestion;Fence 'ime-selected';Capture 'ime-selected'
        Check (@(Events|Where-Object {$_.kind -eq 'submit' -and $_.data.chosen}).Count -ge 2) 'No IME suggestion mouse submit'
        Check (@(Events|Where-Object kind -eq request).Count -eq $before) 'IME selection re-queried'
        Check ((State).text -eq '你好 result' -and -not (State).open) 'IME selection text or popup incorrect'
    }
}catch{$results.Add(@{name='Runner';passed=$false;error="$_"})}
finally {
    Copy-Item "$Layout/events.jsonl" "$Evidence/events.jsonl"
    if($writer){$writer.Dispose()};if($pipe){$pipe.Dispose()}
    & winapp ui yield --json|Set-Content "$Evidence/yield.json"
    @{checks=$results.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'}|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
}
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
if(@($results|Where-Object {-not $_.passed}).Count){exit 1}
