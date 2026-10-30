param([string]$WorkRoot = 'artifacts/win2d-spike')
$ErrorActionPreference = 'Stop'
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
if(Get-AppxPackage -Name 'BiliCopilot.Win2DCapability702c*') { throw 'Experiment identity already registered; refuse to replace another run.' }
$evidence = Join-Path $WorkRoot 'run'
if(Test-Path $evidence) { throw 'Use a fresh run directory; preserve previous evidence.' }
New-Item -ItemType Directory $evidence | Out-Null
$layout = Join-Path $WorkRoot 'layout'
$records = [Collections.Generic.List[object]]::new()
$ownedPid = 0
$previous = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = 'win2d-spike-702c'
function Invoke-Checked([string[]]$Arguments) {
    $output = @(& winapp @Arguments)
    $code = $LASTEXITCODE
    $records.Add(@{ args=$Arguments; exit=$code; output=($output -join "`n"); utc=[DateTime]::UtcNow.ToString('o') })
    if($code -ne 0) { throw "winapp failed ($code): $($Arguments -join ' ')" }
    $output -join "`n"
}
try {
    $launch = Invoke-Checked @('run', "$WorkRoot/harness/Spike.csproj", '-c','Release','--no-build','--no-restore','--on','local','--output-appx-directory',$layout,'--detach','--json') | ConvertFrom-Json
    if(!$launch.ProcessId -or $launch.ProcessScope -eq 'sandbox') { throw 'Invalid owned local PID' }
    $ownedPid = [int]$launch.ProcessId
    if($ownedPid -eq 9608) { throw 'Protected PID; no operations permitted' }
    $process = Get-Process -Id $ownedPid
    if(!$process.Path.StartsWith($WorkRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'PID not in owned workspace' }
    Invoke-Checked @('ui','wait-for','RunSuite','--on','local','-a',"$ownedPid",'-t','55000','--json') | Out-Null
    $windows = Invoke-Checked @('ui','list-windows','--on','local','-a',"$ownedPid",'--json') | ConvertFrom-Json
    $hwnd = @($windows | Where-Object title -Like '*EXPERIMENT Win2D*')[0].hwnd
    if(!$hwnd) { throw 'Owned window missing' }
    Invoke-Checked @('ui','invoke','RunSuite','--on','local','-w',"$hwnd",'--json') | Out-Null
    Invoke-Checked @('ui','wait-for','Status','--value','queue/normal visual-ready','--on','local','-w',"$hwnd",'-t','5000','--json') | Out-Null
    Invoke-Checked @('ui','screenshot','--on','local','-w',"$hwnd",'--output',"$evidence/01-real-control.png",'--json') | Out-Null
    Invoke-Checked @('ui','wait-for','Status','--value','DONE','--contains','--on','local','-w',"$hwnd",'-t','55000','--json') | Out-Null
    Invoke-Checked @('ui','screenshot','--on','local','-w',"$hwnd",'--output',"$evidence/02-complete.png",'--json') | Out-Null
} finally {
    if($ownedPid -gt 0 -and $ownedPid -ne 9608) {
        $p = Get-Process -Id $ownedPid -ErrorAction SilentlyContinue
        if($p -and $p.Path.StartsWith($WorkRoot,[StringComparison]::OrdinalIgnoreCase)) {
            $close = $p.CloseMainWindow()
            $exited = $p.WaitForExit(3000)
            if(!$exited) { Stop-Process -Id $ownedPid; $p.WaitForExit(3000) | Out-Null }
            $records.Add(@{cleanup='owned process';pid=$ownedPid;closeMainWindow=$close;graceful=$exited;remaining=[bool](Get-Process -Id $ownedPid -ErrorAction SilentlyContinue)})
        }
    }
    Get-ChildItem $WorkRoot -Filter events.jsonl -Recurse | Where-Object FullName -Like '*layout*' | ForEach-Object { Copy-Item $_.FullName "$evidence/events.jsonl" }
    $packages = @(Get-AppxPackage -Name 'BiliCopilot.Win2DCapability702c*')
    foreach($p in $packages) {
        if(!$p.InstallLocation.StartsWith($WorkRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Refuse removing package outside owned root' }
        $events = Join-Path $env:LOCALAPPDATA "Packages/$($p.PackageFamilyName)/LocalState/events.jsonl"
        if(Test-Path $events) { Copy-Item $events "$evidence/events.jsonl" }
        Remove-AppxPackage -Package $p.PackageFullName
        $records.Add(@{cleanup='owned package';package=$p.PackageFullName;remaining=@(Get-AppxPackage -Name $p.Name).Count})
    }
    try { Invoke-Checked @('ui','yield','--on','local','--json') | Out-Null }
    finally {
        $records | ConvertTo-Json -Depth 12 | Set-Content "$evidence/commands.json"
        $env:WINAPP_UI_WORKFLOW_ID = $previous
    }
}
