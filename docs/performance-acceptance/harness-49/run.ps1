param([int]$AppPid,[string]$ArtifactDirectory,[string]$Engine)
$ErrorActionPreference='Stop'
if($AppPid -eq 9608 -or $AppPid -le 0){throw 'Invalid owned PID'}
$env:WINAPP_UI_WORKFLOW_ID='bili-acceptance49-c4d3'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$rows=[Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments --on local -a $AppPid --json;if($LASTEXITCODE -ne 0){throw ($o -join "`n")};return $o}
function Act([string]$Name){Ui @('invoke',$Name)|Out-Null;Ui @('wait-for','Status','--value',"$Name ready",'--contains','-t','5000')|Out-Null}
function Capture([string]$Name){
 $start=[datetime]::UtcNow
 Ui @('inspect')|Set-Content "$ArtifactDirectory/$Name.uia.json"
 Ui @('screenshot','--capture-screen','--output',"$ArtifactDirectory/$Name.png")|Out-Null
 $rows.Add(@{name=$Name;startUtc=$start;endUtc=[datetime]::UtcNow})
}
try {
 Act $Engine;Capture '01-start'
 Act 'Burst';Capture '02-burst'
 Act 'Content';Capture '03-content'
 Act 'Style';Capture '04-style'
 Act $Engine;Act 'Pause';Capture '05-pause-a';Act 'Sample';Capture '06-pause-b'
 Act 'SeekForward';Capture '07-paused-seek-a';Act 'Sample';Capture '08-paused-seek-b'
 Act 'SeekBack';Capture '09-paused-back'
 Act 'Redraw';Capture '10-paused-redraw'
 Act 'Resume';Act 'SeekForward';Capture '11-playing-seek'
 Act 'Hide';Capture '12-hidden';Act 'Show';Capture '13-shown'
 Act 'Clear';Capture '14-clear';Act 'Reinitialize';Capture '15-reinitialize'
 Act 'ReplaceVM';Capture '16-replace-vm'
 Act 'UnloadQueued';Capture '17-unload-queued'
 Act 'DetachedUpdates';Act 'Remount';Capture '18-remount'
 Act 'UnloadQueued';Act 'Remount';Capture '19-remount2'
 Act 'UnloadQueued';Act 'Remount';Capture '20-remount3'
} finally {
 $rows|ConvertTo-Json -Depth 5|Set-Content "$ArtifactDirectory/captures.json"
 Copy-Item "$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance49C4D3_y1mf40g4r9srg/LocalState/events.jsonl" "$ArtifactDirectory/events.jsonl"
}
