param([Parameter(Mandatory)][int]$AppPid)
$ErrorActionPreference = 'Stop'
$env:WINAPP_UI_WORKFLOW_ID = 'engine49-trace-9d66'
$out = [IO.Path]::GetFullPath('artifacts/engine49-trace/run')
if (Test-Path $out) { throw 'Use a fresh evidence directory' }
$process = Get-Process -Id $AppPid
if ($AppPid -eq 9608 -or $process.Path -notlike '*t3-9d663a29*engine49-trace*') { throw 'Not owned' }
New-Item -ItemType Directory $out | Out-Null
$captures = [Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments) {
    $text = & winapp ui @Arguments --on local -a $AppPid --json
    if ($LASTEXITCODE -ne 0) { throw ($text -join "`n") }
    $text
}
function Act([string]$Name) {
    Ui @('invoke', $Name) | Out-Null
    Ui @('wait-for', 'Status', '--value', "$Name ready", '--contains', '-t', '5000') | Out-Null
}
function Capture([string]$Name) {
    $begin = [DateTimeOffset]::UtcNow
    Ui @('screenshot', '--output', "$out/$Name.png") | Out-Null
    $captures.Add(@{name=$Name; beginUtc=$begin; endUtc=[DateTimeOffset]::UtcNow; pid=$AppPid})
}
function Export([int]$Index) {
    Ui @('invoke','ExportTrace') | Out-Null
    Ui @('wait-for','Status','--value',"Exported $Index",'--contains','-t','10000') | Out-Null
}
try {
    Ui @('inspect','--interactive') | Set-Content "$out/initial-tree.json"
    Act 'Win2D'
    Act 'SingleRolling'; Capture 'single-rolling-before'; Act 'Pause'
    Capture 'single-rolling-paused-1'
    # Observation window, not a completion fence.
    Start-Sleep -Milliseconds 3500
    Capture 'single-rolling-paused-2'; Export 1
    Act 'SingleTop'; Capture 'single-top-before'; Act 'Pause'
    Capture 'single-top-paused-1'
    Start-Sleep -Milliseconds 3500
    Capture 'single-top-paused-2'; Export 2
    # Restore default Scroll used by the original realtime command.
    Act 'SingleRolling'
    Act 'Win2D'; Act 'Exact'; Act 'SeekForward'; Act 'Pause'
    Capture 'original-exact-pause-after-seek'
    Act 'Motion'; Capture 'original-motion-1'; Capture 'original-motion-2'
    Act 'FreezeMotion'; Capture 'original-frozen-1'; Capture 'original-frozen-2'; Export 3
    Act 'UnloadQueued'; Export 4
} finally {
    $captures | ConvertTo-Json -Depth 5 | Set-Content "$out/captures.json"
    $package = Get-AppxPackage -Name BiliCopilot.Acceptance49Trace9D66
    if ($package) {
        Copy-Item "$env:LOCALAPPDATA/Packages/$($package.PackageFamilyName)/LocalState/*.json*" $out
    }
    & winapp ui yield --on local --json | Set-Content "$out/yield.json"
}
