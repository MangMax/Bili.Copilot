param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
if($AppPid -eq 9608){throw 'Protected process'}
if(Test-Path $Evidence){throw 'Use a fresh evidence directory'}
New-Item -ItemType Directory $Evidence | Out-Null
$env:WINAPP_UI_WORKFLOW_ID='acceptance54-recheck-1d19'
$package=Get-AppxPackage -Name BiliCopilot.Acceptance54.R1D19
$process=Get-Process -Id $AppPid
if(!$package -or !$process.Path.StartsWith($package.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned process/package mismatch'}
$local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
function Ui([string[]]$Arguments){
    $output=& winapp ui @Arguments --on local --json
    if($LASTEXITCODE){throw "winapp failed: $($output -join ' ')"}
    $output
}
try {
    $identity=Get-Content "$local/identity.json" -Raw|ConvertFrom-Json
    $controller=[string]$identity.controllerHwnd
    Ui @('inspect','-w',$controller) | Set-Content "$Evidence/controller-tree.json"
    # Existing initial window is closed by Create; all subsequent states belong to this runner.
    Ui @('invoke','Create','-w',$controller)|Set-Content "$Evidence/create.json"
    Ui @('wait-for','Status','--value','Created','-t','3000','-w',$controller)|Out-Null
    $player=[string](Get-Content "$local/player.json" -Raw|ConvertFrom-Json).hwnd
    foreach($step in @(@('initial','Created'),@('Top','Topmost'),@('Full','FullScreen'),@('Compact','CompactOverlay'),@('Normal','Overlapped'))){
        if($step[0] -ne 'initial'){
            Ui @('invoke',$step[0],'-w',$controller)|Set-Content "$Evidence/$($step[0])-invoke.json"
            Ui @('wait-for','Status','--value',$step[1],'-t','3000','-w',$controller)|Out-Null
        }
        Ui @('invoke','VisualProbe','-w',$controller)|Set-Content "$Evidence/$($step[0])-probe.json"
        Copy-Item "$local/visual-tree.json" "$Evidence/$($step[0])-visual-tree.json"
        Ui @('screenshot','-w',$player,'--capture-screen','--output',"$Evidence/$($step[0])-screen.png")|Set-Content "$Evidence/$($step[0])-screen-capture.json"
        Ui @('inspect','-w',$player)|Set-Content "$Evidence/$($step[0])-tree.json"
        Ui @('screenshot','-w',$player,'--output',"$Evidence/$($step[0]).png")|Set-Content "$Evidence/$($step[0])-capture.json"
        if(!(Test-Path "$Evidence/$($step[0]).png") -or (Get-Item "$Evidence/$($step[0]).png").Length -eq 0){throw 'Missing screenshot'}
    }
    Ui @('invoke','RunTests','-w',$controller)|Set-Content "$Evidence/run-invoke.json"
    Ui @('wait-for','Status','--value','Suite complete','-t','60000','-w',$controller)|Set-Content "$Evidence/suite-wait.json"
    Ui @('screenshot','-w',$controller,'--output',"$Evidence/complete.png")|Set-Content "$Evidence/complete-capture.json"
    Copy-Item "$local/*.json","$local/*.jsonl" $Evidence
    $rows=@(Get-Content "$Evidence/results.json" -Raw|ConvertFrom-Json)
    $failed=@($rows|Where-Object status -ne 'PASS')
    if($rows.Count -ne 18 -or $failed.Count){throw "Suite: $($rows.Count) cases; $($failed.Count) failed"}
} finally {
    Copy-Item "$local/*.json","$local/*.jsonl" $Evidence -ErrorAction Continue
    & winapp ui yield --on local --json | Set-Content "$Evidence/yield.json"
}
