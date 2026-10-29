param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference='Stop'
if($AppPid -eq 9608 -or $AppPid -le 0){throw 'Invalid owned PID'}
$env:WINAPP_UI_WORKFLOW_ID='bili-acceptance49-31f2'
if(Test-Path $ArtifactDirectory){throw 'Evidence directory must be new'}
New-Item -ItemType Directory $ArtifactDirectory | Out-Null
$ArtifactDirectory=(Resolve-Path $ArtifactDirectory).Path
$log="$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance4931F2_y1mf40g4r9srg/LocalState/events.jsonl"
$results=[Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments --on local -a $AppPid --json;if($LASTEXITCODE -ne 0){throw ($o -join "`n")};return $o}
function Act([string]$Name){Ui @('invoke',$Name)|Out-Null;Ui @('wait-for','Status','--value',"$Name ready",'--contains','-t','5000')|Out-Null}
function State { (Get-Content $log | ForEach-Object {ConvertFrom-Json $_} | Where-Object kind -eq snapshot | Select-Object -Last 1).data }
function Check([string]$Name,[bool]$Passed,[string]$Scope='structure'){$results.Add(@{name=$Name;passed=$Passed;scope=$Scope;state=(State)})}
function Capture([string]$Name){Ui @('inspect')|Set-Content "$ArtifactDirectory/$Name.uia.json";Ui @('screenshot','--capture-screen','--output',"$ArtifactDirectory/$Name.png")|Out-Null}
try {
 Ui @('wait-for','DirectX','-t','10000')|Out-Null
 foreach($engine in 'DirectX','Win2D'){
  Act $engine
  Act 'Pause';Act 'SeekForward';Act 'Sample'
  $s=State;Check "$engine-PausedSeek" ($s.paused -and $s.engineTree._render._isPaused) 'engine pause state; known blocker'
  Capture "$engine-paused-seek"
  Act 'Switch';Act 'Sample'
  $s=State;Check "$engine-PausedSwitch" ($s.paused -and $s.engineTree._render._isPaused) 'engine pause state; known blocker'
  for($cycle=1;$cycle -le 3;$cycle++){
   Act 'UnloadQueued';Act 'DetachedUpdates'
   $s=State;Check "$engine-Unload-$cycle" (!$s.loaded -and $s.visuals -eq 0 -and !$s.dx -and !$s.legacy)
   Act 'Remount';Act 'Redraw';Act 'Sample'
   $s=State;Check "$engine-Remount-$cycle" ($s.loaded -and $s.visuals -eq 1 -and ($s.dx -xor $s.legacy) -and $s.lastProgress -eq 20 -and $s.items.Count -eq 5 -and @($s.items|Where-Object {$_.Text -notlike 'DETACHED*'}).Count -eq 0)
   Capture "$engine-remount-$cycle"
  }
  Act 'Resume';Act 'Realtime';Act 'Sample';Capture "$engine-remount-visible"
  Act 'Hide';$s=State;Check "$engine-RemountHideBinding" (!$s.show -and $s.visibility -eq 'Collapsed')
  Act 'Show';$s=State;Check "$engine-RemountShowBinding" ($s.show -and $s.visibility -eq 'Visible')
  Act 'ReplaceVM';Act 'Sample'
  $s=State;Check "$engine-ReplaceVMRejectsOldEvents" ($s.position -eq 2 -and $s.lastProgress -eq 2 -and !$s.paused -and $s.items.Count -eq 5 -and @($s.items|Where-Object {$_.Text -notlike 'NEWVM*'}).Count -eq 0)
  Act 'UnloadQueued';Act 'Sample'
  $s=State;Check "$engine-FinalUnload" (!$s.loaded -and $s.visuals -eq 0 -and !$s.dx -and !$s.legacy)
 }
} finally {
 Copy-Item $log "$ArtifactDirectory/events.jsonl"
 $results|ConvertTo-Json -Depth 16|Set-Content "$ArtifactDirectory/results.json"
}
$results | Select-Object name,passed,scope | Format-Table
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
