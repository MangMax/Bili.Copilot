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
function ScrollTarget {
 $j=Ui @('inspect','--depth','12')|ConvertFrom-Json
 function Nodes($a){foreach($n in $a){$n;Nodes $n.children}}
 $p=@(Nodes $j.windows.elements|Where-Object {$_.automationId -eq 'PART_ScrollPresenter' -and $_.width -gt 500})
 Check ($p.Count -eq 1) 'Expected unique main scroll presenter'
 $p[0].selector
}
try {
 $owned=Get-Process -Id $AppPid
 Check ($AppPid -ne 9608 -and $owned.Path.StartsWith('C:\Users\hp\AppData\Local\Temp\bili5053-d26c-layout\',[StringComparison]::OrdinalIgnoreCase)) 'Not owned'
 Case 'ActualPresenterScrollResizeIdle' {
  Ui @('send-keys','ctrl+home','--target',(ScrollTarget),'--via','send-input')|Out-Null
  Start-Sleep -Milliseconds 800
  Command 'Burst';Start-Sleep -Milliseconds 1200
  Ui @('send-keys','ctrl+end','--target',(ScrollTarget),'--via','send-input')|Out-Null
  $s=Fence
  Capture 'actual-scroll-bottom'
  Get-Process -Id $AppPid|Select-Object Id,CPU,WorkingSet64,PrivateMemorySize64,HandleCount|ConvertTo-Json|Set-Content "$Evidence/process-sample.json"
  Check ($s.items.Count -eq 60 -and $s.views -gt 0 -and $s.sizes -gt 1 -and $s.VerticalOffset -gt 0) 'No real scroll/size activity'
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
