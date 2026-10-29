param([Parameter(Mandatory)][int]$AppPid)
$ErrorActionPreference = 'Stop'
$env:WINAPP_UI_WORKFLOW_ID = 'engine49-trace-9d66'
$out = [IO.Path]::GetFullPath('artifacts/engine49-trace/replay-original')
if (Test-Path $out) { throw 'Use a fresh directory' }
if ($AppPid -eq 9608 -or (Get-Process -Id $AppPid).Path -notlike '*t3-9d663a29*engine49-trace*') { throw 'Not owned' }
New-Item -ItemType Directory $out | Out-Null
$captures = [Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments) {
    $text = & winapp ui @Arguments --on local -a $AppPid --json
    if ($LASTEXITCODE -ne 0) { throw ($text -join "`n") }
    $text
}
function Act([string]$Name) {
    Ui @('invoke',$Name) | Out-Null
    Ui @('wait-for','Status','--value',"$Name ready",'--contains','-t','5000') | Out-Null
}
function Capture([string]$Name) {
    Act 'Sample'
    $begin = [DateTimeOffset]::UtcNow
    Ui @('screenshot','--output',"$out/$Name.png") | Out-Null
    $captures.Add(@{name=$Name;beginUtc=$begin;endUtc=[DateTimeOffset]::UtcNow;pid=$AppPid})
}
try {
    # Exact Win2D subsequence from candidate visual.ps1, including Sample actions.
    Act 'Win2D'; Act 'Exact'; Act 'SeekForward'; Act 'Pause'
    Capture 'Win2D-exact-pause-after-seek'
    Act 'Motion'; Capture 'Win2D-motion-1'; Capture 'Win2D-motion-2'
    Act 'FreezeMotion'; Capture 'Win2D-frozen-1'; Capture 'Win2D-frozen-2'
    Act 'UnloadQueued'
    Ui @('invoke','ExportTrace') | Out-Null
    Ui @('wait-for','Status','--value','Exported 1','--contains','-t','10000') | Out-Null
} finally {
    $captures | ConvertTo-Json -Depth 5 | Set-Content "$out/captures.json"
    $package = Get-AppxPackage -Name BiliCopilot.Acceptance49Trace9D66
    if ($package) { Copy-Item "$env:LOCALAPPDATA/Packages/$($package.PackageFamilyName)/LocalState/*.json*" $out }
    & winapp ui yield --on local --json | Set-Content "$out/yield.json"
}
