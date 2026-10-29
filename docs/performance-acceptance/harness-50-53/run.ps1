param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='bili5053-d26c'
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Force $Evidence|Out-Null
$trace="$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance5053D26C_y1mf40g4r9srg/LocalState/trace.jsonl"
$results=[Collections.Generic.List[object]]::new()
$captures=[Collections.Generic.List[string]]::new()
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments -a $AppPid --json; if($LASTEXITCODE -ne 0){throw "UI command failed: $Arguments $o"}; $o}
function Events { $all=@(Get-Content $trace | ForEach-Object{$_|ConvertFrom-Json}); $last=0;for($i=0;$i -lt $all.Count;$i++){if($all[$i].kind -eq "launch"){$last=$i}};@($all[$last..($all.Count-1)]) }
function State { (Events | Where-Object kind -eq state | Select-Object -Last 1).data }
function Command([string]$Name){Ui @('invoke',$Name)|Out-Null}
function Check($Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Await([scriptblock]$Test,[string]$Message,[int]$Timeout=10000){$sw=[Diagnostics.Stopwatch]::StartNew();while(-not (&$Test)){if($sw.ElapsedMilliseconds -gt $Timeout){throw "Timeout: $Message"};Start-Sleep -Milliseconds 100}}
function Snap {Command 'Snapshot';Start-Sleep -Milliseconds 150;State}
function Capture([string]$Name){$path=Join-Path $Evidence "$Name.png";Ui @('screenshot','--output',$path)|Out-Null;Ui @('inspect','--depth','12')|Set-Content "$Evidence/$Name.uia.json";$captures.Add($path)}
function Fence {
 $n=@(Events|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'fence-end'}).Count
 Command 'Fence'
 Await {@(Events|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'fence-end'}).Count -gt $n} '4500ms fence'
 $events=Events;$mid=($events|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'fence-mid'}|Select-Object -Last 1);$end=($events|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'fence-end'}|Select-Object -Last 1)
 Check ($end.ms-$mid.ms -ge 2950) 'Quiet window shorter than 3 seconds'
 Check ($mid.data.requests -eq $end.data.requests) 'Requests continue during stable quiet window'
 Check ($mid.data.updates -eq $end.data.updates) 'List notifications continue during stable quiet window'
 Check ($mid.data.views -eq $end.data.views -and $mid.data.sizes -eq $end.data.sizes) 'Geometry did not settle'
 return $end.data
}
function Case([string]$Name,[scriptblock]$Test){
 try{&$Test;$results.Add(@{name=$Name;passed=$true})}
 catch{$results.Add(@{name=$Name;passed=$false;error="$_"});try{Capture "$Name-failure"}catch{$results.Add(@{name="$Name capture";passed=$false;error="$_"})}}
 $results|ConvertTo-Json -Depth 6|Set-Content "$Evidence/results.json"
}
try{
 $owned=Get-Process -Id $AppPid
 Check ($AppPid -ne 9608 -and $owned.Path.StartsWith('C:\Users\hp\AppData\Local\Temp\bili5053-d26c-layout\',[StringComparison]::OrdinalIgnoreCase)) 'Not owned process'
 foreach($mode in @('Recommend','Hot')){
  Case "Slow$mode" {
   Command $mode
   Await {@(Events|Where-Object {$_.kind -eq 'state' -and $_.data.scenario -eq $mode -and $_.data.condition -eq 'first-card-render'}).Count -gt 0} 'first card before directory'
   $s=Snap;Check ($s.partitionLoading -and $s.cards -gt 0) 'Directory did not remain pending with visible cards'
   Capture "slow-$mode";Command 'ReleaseDirectory'
   $s=Fence;Check ($s.items.Count -eq 3) 'Short pages did not continue to three cards'
   $ev=Events;Check (@($ev|Where-Object {$_.kind -eq 'response' -and $_.data.Scenario -eq $mode -and $_.data.count -eq 0}).Count -gt 0) 'No actual empty page reached'
  }
 }
 foreach($mode in @('Saved','Missing','DirFail')){
  Case "$mode restore" {
   Command $mode;Start-Sleep -Milliseconds 700
   $s=Snap;Check ($s.requests -eq 1 -and $s.items.Count -eq 0) 'Saved partition must await directory'
   Capture "$mode-pending";Command 'ReleaseDirectory';$s=Fence
   $expected=if($mode -eq 'Saved'){'13'}else{'Recommend'}
   Check ($s.selected -eq $expected -and $s.cards -gt 0) 'Wrong restored/fallback section or absent cards'
   Capture "$mode-restored"
  }
 }
 Case 'EmptyThenRefresh' {
  Command 'Recommend';Command 'ReleaseDirectory';$s=Fence
  Check ($s.items.Count -eq 3) 'Missing short list'
  $before=$s.requests;Command 'Refresh';$s=Fence
  Check ($s.requests -gt $before -and $s.items.Count -eq 3) 'Refresh failed to resume after empty page'
  Capture 'empty-refresh'
 }
 Case 'FailureThenRefresh' {
  Command 'Failure';$s=Fence
  Check (@(Events|Where-Object {$_.kind -eq 'response-error' -and $_.data.Scenario -eq 'Failure'}).Count -gt 0) 'Actual failure not reached'
  $before=$s.requests;Command 'Refresh';$s=Fence
  Check ($s.requests -gt $before -and $s.items.Count -eq 3) 'Refresh failed to recover content'
  Capture 'failure-refresh'
 }
 foreach($mode in @('Rank','Partition')){
  Case "$mode idle" {
   Command $mode;$s=Fence;Check ($s.items.Count -eq 1 -and $s.requests -eq 2 -and $s.updates -eq 1) 'Populated ranking repeats requests/notifications'
   Capture "$mode-idle"
  }
 }
 Case 'StaleSection' {
  Command 'Stale';Start-Sleep -Milliseconds 400;Command 'SwitchHot';Start-Sleep -Milliseconds 700
  Command 'ReleaseOld';Command 'ReleaseDirectory';$s=Fence
  Check ($s.selected -eq 'Hot' -and @($s.items|Where-Object {$_ -like '*STALE*'}).Count -eq 0 -and $s.items.Count -gt 0) 'Old page contaminated new section'
  Capture 'stale-rejected'
 }
 Case 'BurstGeometry' {
  Command 'Long';Start-Sleep -Milliseconds 500;Command 'Burst';Start-Sleep -Milliseconds 1200;$s=Fence
  Check ($s.items.Count -eq 60 -and $s.views -gt 0 -and $s.sizes -gt 1) 'Burst did not exercise real geometry events'
  Capture 'long-burst'
  Get-Process -Id $AppPid|Select-Object Id,CPU,WorkingSet64,PrivateMemorySize64,HandleCount|ConvertTo-Json|Set-Content "$Evidence/process-sample.json"
 }
 Case 'UnloadDelay' {
  Command 'Recommend';Start-Sleep -Milliseconds 200;Command 'DelayDetach';$s=Fence
  Check (-not $s.loaded -and -not $s.active -and $s.mainSubscriptions -eq 0 -and $s.sideSubscriptions -eq 0) 'Unloaded control remains active'
  Capture 'detached'
 }
 Case 'ReattachMainSide' {
  Command 'Attach';$s=Fence
  Capture 'reattached'
  Check ($s.loaded -and $s.scrollLoaded -and $s.active -and $s.mainSubscriptions -eq 1 -and $s.sideSubscriptions -eq 1 -and $s.sideHasSource) 'Reattached control lost lifecycle subscriptions or active state'
 }
 Case 'RemountRefresh' {
  Command 'Refresh';$s=Fence;Capture 'remount-refresh'
  Check ($s.items.Count -eq 3) 'Reattached short list no longer automatically fills'
 }
}finally{
 Copy-Item $trace "$Evidence/events.jsonl"
 $results|ConvertTo-Json -Depth 6|Set-Content "$Evidence/results.json"
 $captures|ConvertTo-Json|Set-Content "$Evidence/screenshots.json"
 winapp ui yield --json|Set-Content "$Evidence/yield.json"
}
$failed=@($results|Where-Object {-not $_.passed}).Count
"Passed $($results.Count-$failed), failed $failed"
if($failed){exit 1}
