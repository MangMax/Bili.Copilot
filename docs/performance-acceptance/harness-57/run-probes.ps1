param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
if ($AppPid -eq 9608) { throw 'Protected process.' }
$env:WINAPP_UI_WORKFLOW_ID = 'bili-spike57-a35e'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
function Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments --on local -a $AppPid --json
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return $output
}
$records = [Collections.Generic.List[object]]::new()
function Capture([string]$Name) {
    $path = Join-Path $ArtifactDirectory "$Name.png"
    if (Test-Path $path) { throw 'Evidence exists.' }
    $begin = [datetime]::UtcNow
    Ui @('screenshot','--capture-screen','--output',$path) | Out-Null
    if (!(Test-Path $path)) { throw 'Missing capture.' }
    $records.Add(@{ name=$Name; captureStartUtc=$begin; captureEndUtc=[datetime]::UtcNow; path=$path; visualReview='PENDING' })
}
try {
    foreach ($engine in @('DirectX','Win2D')) {
        Ui @('invoke',$engine) | Out-Null
        Ui @('invoke','Primary') | Out-Null
        Ui @('wait-for','Status','--value','primary-visible-probe','--contains','-t','10000') | Out-Null
        Capture "$engine-primary"
        Ui @('wait-for','Status','--value','neighbor-completed','--contains','-t','10000') | Out-Null
        Ui @('invoke','Rolling') | Out-Null
        Ui @('wait-for','Status','--value','rolling-paused','--contains','-t','10000') | Out-Null
        Capture "$engine-paused-a"
        Start-Sleep -Seconds 2 # Deliberate animation observation interval, not a latency estimate.
        Capture "$engine-paused-b"
        Ui @('invoke','SeekPaused') | Out-Null
        Ui @('wait-for','Status','--value','seek-without-pause','--contains','-t','10000') | Out-Null
        Capture "$engine-seek-a"
        Start-Sleep -Seconds 2
        Capture "$engine-seek-b"
        Ui @('invoke','Pause') | Out-Null
    }
} finally {
    $records | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ArtifactDirectory 'captures.json')
    & winapp ui yield --on local | Out-Null
}
