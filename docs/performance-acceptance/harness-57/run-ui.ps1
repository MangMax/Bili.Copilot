param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
if ($AppPid -eq 9608) { throw 'Protected process.' }
$env:WINAPP_UI_WORKFLOW_ID = 'bili-spike57-a35e'
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments --on local -a $AppPid --json
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return $output
}
function Capture([string]$Name, [datetime]$Start) {
    $path = Join-Path $ArtifactDirectory "$Name.png"
    if (Test-Path $path) { throw 'Never overwrite evidence.' }
    $before = [datetime]::UtcNow
    Ui @('screenshot', '--capture-screen', '--output', $path) | Out-Null
    $after = [datetime]::UtcNow
    if (!(Test-Path $path) -or (Get-Item $path).Length -eq 0) { throw 'Capture missing.' }
    $results.Add(@{ name=$Name; commandStartUtc=$Start; captureStartUtc=$before; captureEndUtc=$after; visibleUpperBoundMs=($after-$Start).TotalMilliseconds; screenshot=$path; visualReview='PENDING' })
}
try {
    Ui @('inspect') | Set-Content (Join-Path $ArtifactDirectory 'initial-tree.json')
    foreach ($engine in @('DirectX','Win2D')) {
        Ui @('invoke',$engine) | Out-Null
        foreach ($mode in @('Window','Full','Dense')) {
            $start = [datetime]::UtcNow
            Ui @('invoke',$mode) | Out-Null
            Ui @('wait-for','Status','--value','ready-capture','--contains','-t','15000') | Out-Null
            Capture "$engine-$mode" $start
        }
        Ui @('invoke','Window') | Out-Null
        Ui @('wait-for','Status','--value','ready-capture','--contains','-t','15000') | Out-Null
        foreach ($step in @('Append','Replace','Seek','Pause')) {
            $start = [datetime]::UtcNow
            Ui @('invoke',$step) | Out-Null
            $expected = @{ Append='append'; Replace='replace-no-seek'; Seek='seek-pause-restored'; Pause='pause' }[$step]
            Ui @('wait-for','Status','--value',$expected,'--contains','-t','10000') | Out-Null
            Capture "$engine-$step" $start
        }
    }
} finally {
    $results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ArtifactDirectory 'captures.json')
    & winapp ui yield --on local | Out-Null
}
