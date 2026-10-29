param([int]$AppPid,[string]$ArtifactDirectory)
$ErrorActionPreference='Stop';$env:WINAPP_UI_WORKFLOW_ID='bili-acceptance49-c4d3';New-Item -ItemType Directory -Force $ArtifactDirectory|Out-Null
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments --on local -a $AppPid --json;if($LASTEXITCODE -ne 0){throw ($o -join "`n")};return $o}
function Act([string]$Name){Ui @('invoke',$Name)|Out-Null;Ui @('wait-for','Status','--value',"$Name ready",'--contains','-t','5000')|Out-Null}
function Shot([string]$Name){Ui @('inspect')|Set-Content "$ArtifactDirectory/$Name.uia.json";Ui @('screenshot','--capture-screen','--output',"$ArtifactDirectory/$Name.png")|Out-Null}
foreach($engine in @('DirectX','Win2D')){
 Act $engine;Act 'Realtime';Shot "$engine-realtime"
 Act 'Pause';Shot "$engine-paused-a";Act 'Sample';Shot "$engine-paused-b"
 Act 'SeekForward';Act 'Sample';Shot "$engine-seek-paused"
 Act 'Switch';Shot "$engine-switch-paused";Act 'Sample'
 Act 'Resume';Act 'Realtime';Shot "$engine-switch-resumed"
 Act 'UnloadQueued';Act 'Remount';Act 'Sample';Shot "$engine-remount-settled"
 Act 'Redraw';Act 'Sample';Shot "$engine-remount-public-redraw"
}
Copy-Item "$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance49C4D3_y1mf40g4r9srg/LocalState/events.jsonl" "$ArtifactDirectory/events.jsonl"
