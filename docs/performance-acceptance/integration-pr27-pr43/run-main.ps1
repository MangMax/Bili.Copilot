param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='integration2743'
$Layout=[IO.Path]::GetFullPath($Layout)
if($AppPid -eq 9608 -or (Get-Process -Id $AppPid).Path -ne "$Layout\BiliCopilot.UI.exe"){throw 'Not owned process'}
$package=Get-AppxPackage -Name Mang.BiliCopilot.Integration2743.Player
if(!$package -or $package.InstallLocation -ne $Layout){throw 'Not owned registration'}
$state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
if(Test-Path "$state/main-results.json"){throw 'Stale main results; use a fresh owned registration'}
New-Item -ItemType Directory -Force $Evidence | Out-Null
$sourceRoot=(Resolve-Path "$PSScriptRoot/../../..").Path
git -C $sourceRoot rev-parse HEAD | Set-Content "$Layout/source-provenance.txt"
git -C $sourceRoot diff -- src | Add-Content "$Layout/source-provenance.txt"
try {
    & winapp ui invoke MainChecks --on local -a $AppPid --json
    if($LASTEXITCODE -ne 0){throw 'MainChecks invoke failed'}
    $deadline=[DateTime]::UtcNow.AddMinutes(5)
    while(!(Test-Path "$state/main-ready.json") -and [DateTime]::UtcNow -lt $deadline){
        if(!(Get-Process -Id $AppPid -ErrorAction SilentlyContinue)){throw 'Owned process exited before baseline finished'}
        Start-Sleep -Seconds 1
    }
    if(!(Test-Path "$state/main-ready.json")){throw 'Main baseline did not finish'}
    $ready=Get-Content "$state/main-ready.json" -Raw | ConvertFrom-Json
    $baseline=Get-Content "$state/idle-baseline.json" -Raw | ConvertFrom-Json
    if(@($baseline.rows).Count -ne 183){throw 'Incomplete baseline samples'}
    $results=Get-Content "$state/main-results.json" -Raw | ConvertFrom-Json
    if(@($results).Count -ne 4 -or @($results | Where-Object status -ne PASS).Count){throw 'Main checks failed'}
    & winapp ui screenshot -w $ready.mainHwnd --on local -a $AppPid --output "$Evidence/settings.png" --json
    & winapp ui invoke ExitMain -w $ready.controllerHwnd --on local -a $AppPid --json
    $exitDeadline=[DateTime]::UtcNow.AddSeconds(15)
    while((Get-Process -Id $AppPid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $exitDeadline){Start-Sleep -Milliseconds 200}
    $exited=!(Get-Process -Id $AppPid -ErrorAction SilentlyContinue)
    $unhandled=@(Get-Content "$state/events.jsonl" | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object { $_.kind -eq 'logger' -and $_.data.message -eq 'Unhandled exception occurred.' })
    @{pid=$AppPid;explicitExit=$exited;unhandledCount=$unhandled.Count;baselineRows=$baseline.rows.Count} | ConvertTo-Json | Set-Content "$Evidence/exit.json"
    if(!$exited){throw 'Explicit exit left owned process alive'}
    if($unhandled.Count){throw 'Unhandled exception during native main/exit checks'}
} finally {
    foreach($name in 'main-results.json','main-ready.json','idle-baseline.json','events.jsonl'){
        if(Test-Path "$state/$name"){Copy-Item "$state/$name" $Evidence -Force}
    }
    & winapp ui yield --on local --json | Set-Content "$Evidence/yield.json"
}
