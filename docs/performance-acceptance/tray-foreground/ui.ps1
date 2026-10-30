param([string]$Run='run1')
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='tray-ee493'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..");$out="$root/artifacts/tray-foreground/$Run"
$r=Get-Content "$out/ready.json" -Raw|ConvertFrom-Json
$target=Get-Process -Id $r.pid
if($target.Path -ne $r.exe -or $target.StartTime.ToUniversalTime().ToFileTimeUtc() -ne $r.creationFileTime -or $r.pid -eq 9608){throw 'Identity mismatch'}
if($Run.StartsWith('logged')){$W=[string](Get-Content "$out/main-window.json" -Raw|ConvertFrom-Json).hwnd}else{$ready=Get-Content "$($r.state)/tray-ready.json" -Raw|ConvertFrom-Json;$W=[string]$ready.hwnd}
if(-not ('ProbeNative' -as [type])){Add-Type @'
using System;using System.Runtime.InteropServices;
public static class ProbeNative {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
}
'@}
function State([string]$label){
 $fg=[ProbeNative]::GetForegroundWindow();[uint32]$fp=0;[void][ProbeNative]::GetWindowThreadProcessId($fg,[ref]$fp)
 $s=@{utc=[DateTime]::UtcNow.ToString('o');label=$label;mainHwnd=[long]$W;visible=[ProbeNative]::IsWindowVisible([IntPtr][long]$W);minimized=[ProbeNative]::IsIconic([IntPtr][long]$W);foreground=$fg.ToInt64();foregroundPid=$fp;targetForeground=($fg.ToInt64() -eq [long]$W)}
 $s|ConvertTo-Json -Compress|Add-Content "$out/states.jsonl";$s
}
function U([string[]]$a,[string]$hwnd=$W){
 $before=State ('before-'+$a[0]);$start=[DateTime]::UtcNow.ToString('o')
 $raw=@(& winapp ui @a --on local -w $hwnd --json);$code=$LASTEXITCODE
 $after=State ('after-'+$a[0]);$safeOutput=if($Run.StartsWith('logged') -and $a[0] -eq 'inspect'){@('private tree withheld')}else{$raw};@{startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');args=$a;hwnd=$hwnd;exit=$code;before=$before;after=$after;output=$safeOutput}|ConvertTo-Json -Depth 8 -Compress|Add-Content "$out/actions.jsonl"
 if($code -ne 0){throw ($raw -join "`n")};($raw -join "`n")|ConvertFrom-Json
}
function Nodes($nodes){foreach($n in $nodes){$n;Nodes $n.children}}
if(-not ('VerifiedMouse' -as [type])){Add-Type -Path "$PSScriptRoot/Mouse.cs"}
function Mouse([string]$hwnd,[string]$selector,[bool]$right=$false){
 $raw=winapp ui inspect --depth 15 --on local -w $hwnd --json
 if($LASTEXITCODE -ne 0){throw 'Mouse inspection failed'}
 $tree=$raw|ConvertFrom-Json;$nodes=@(Nodes $tree.windows.elements|Where-Object {$_.selector -eq $selector})
 if($nodes.Count -ne 1 -or $nodes[0].isOffscreen -or !$nodes[0].isEnabled){throw 'Mouse selector not uniquely ready'}
 $node=$nodes[0];$x=[int]($node.x+$node.width/2);$y=[int]($node.y+$node.height/2)
 $before=State 'before-verified-mouse';$start=[DateTime]::UtcNow.ToString('o')
 [VerifiedMouse]::Click([long]$hwnd,$x,$y,$right)
 $after=State 'after-verified-mouse'
 @{startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');args=@('verified-mouse',$selector);right=$right;hwnd=$hwnd;x=$x;y=$y;before=$before;after=$after;exit=0}|ConvertTo-Json -Depth 6 -Compress|Add-Content "$out/actions.jsonl"
}
function OpenTray([bool]$invokeOpen=$false) {
 $shell=[ProbeNative]::FindWindow('Shell_TrayWnd',$null).ToInt64()
 $wins=winapp ui list-windows --on local --json|ConvertFrom-Json
 $ov=@($wins|Where-Object className -eq TopLevelWindowForOverflowXamlIsland)
 if(!$ov.Count){
  $shellTree=winapp ui inspect --depth 16 --on local -w $shell --json|ConvertFrom-Json
  $overflowButton=@(Nodes $shellTree.windows.elements|Where-Object name -eq '显示隐藏的图标')
  if($overflowButton.Count -ne 1){throw 'No unique overflow button'}
  if($invokeOpen){U @('invoke',$overflowButton[0].selector) ([string]$shell)|Out-Null}else{Mouse ([string]$shell) $overflowButton[0].selector}
  $deadline=[DateTime]::UtcNow.AddSeconds(3)
  do{$wins=winapp ui list-windows --on local --json|ConvertFrom-Json;$ov=@($wins|Where-Object className -eq TopLevelWindowForOverflowXamlIsland);if(!$ov.Count){Start-Sleep -Milliseconds 100}}while(!$ov.Count -and [DateTime]::UtcNow -lt $deadline)
 }
 if($ov.Count -ne 1){throw 'No unique overflow'}
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{
  $tree=winapp ui inspect --depth 12 --on local -w $ov[0].hwnd --json|ConvertFrom-Json
  $icons=@(Nodes $tree.windows.elements|Where-Object {$_.name -eq '哔哩助理' -and !$_.isOffscreen})
  if(!$icons.Count){Start-Sleep -Milliseconds 100}
 }while(!$icons.Count -and [DateTime]::UtcNow -lt $deadline)
 if($icons.Count -ne 1){throw 'No unique offline app icon'}
 @{hwnd=[string]$ov[0].hwnd;node=$icons[0]}
}
