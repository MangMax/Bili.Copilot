param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference='Stop'
if($AppPid -le 0 -or $AppPid -eq 9608){throw 'Not an owned PID'}
$env:WINAPP_UI_WORKFLOW_ID='engine49-c9d5'
if(Test-Path $ArtifactDirectory){throw 'Use a fresh evidence directory'}
New-Item -ItemType Directory $ArtifactDirectory | Out-Null
$ArtifactDirectory=(Resolve-Path $ArtifactDirectory).Path
function Ui([string[]]$Arguments){$o=& winapp ui @Arguments --on local -a $AppPid --json;if($LASTEXITCODE -ne 0){throw ($o -join "`n")};$o}
function Act([string]$Name){Ui @('invoke',$Name)|Out-Null;Ui @('wait-for','Status','--value',"$Name ready",'--contains','-t','5000')|Out-Null}
function Capture([string]$Name){Act 'Sample';Ui @('screenshot','--output',"$ArtifactDirectory/$Name.png")|Out-Null}
try {
 foreach($engine in 'DirectX','Win2D'){
  Act $engine;Act 'Pause';Act 'SeekForward';Capture "$engine-pause-before-seek"
  Act 'Switch';Capture "$engine-paused-switch"
  # Reset to the named engine for the remaining cases.
  Act $engine;Act 'Exact';Act 'SeekForward';Act 'Pause';Capture "$engine-exact-pause-after-seek"
  Act 'Motion';Capture "$engine-motion-1";Capture "$engine-motion-2"
  Act 'FreezeMotion';Capture "$engine-frozen-1";Capture "$engine-frozen-2"
 }
} finally {
 Copy-Item "$env:LOCALAPPDATA/Packages/BiliCopilot.Acceptance49C9D5_y1mf40g4r9srg/LocalState/events.jsonl" "$ArtifactDirectory/events.jsonl"
}
