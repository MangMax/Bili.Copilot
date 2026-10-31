param([Parameter(Mandatory)][string]$Stage,[ValidateSet('ResizeChecks','HotResizeChecks')][string]$Action='ResizeChecks')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
$hostDir="$root/artifacts/resize-diagnosis/host"
$out="$root/artifacts/resize-diagnosis/$Stage"
if(Test-Path $out){throw 'Fresh stage required'}
New-Item -ItemType Directory $out | Out-Null
if(Test-Path "$hostDir/probe-provenance.json"){Copy-Item "$hostDir/probe-provenance.json" $out}
Copy-Item "$hostDir/RecommendationCoverImage.cs" "$out/instrumented-cover.cs"
$identity='BiliCopilot.Resize5a5e.Owned'
$layout="$out/layout"
if(Get-AppxPackage -Name $identity){throw 'Owned identity already registered'}
$env:WINAPP_UI_WORKFLOW_ID='resize-5a5e'
$appPid=0
$started=Get-Date
Get-Process -Name BiliCopilot.UI -ErrorAction SilentlyContinue | Select-Object Id,Path,CPU,WorkingSet64 | ConvertTo-Json | Set-Content "$out/other-processes.json"
try {
    $launch=& winapp run "$hostDir/Harness.csproj" -c Release --arch x64 -p Platform=x64 --no-build --output-appx-directory $layout --detach --json
    $launch | Set-Content "$out/launch.json"
    if($LASTEXITCODE -ne 0){throw 'Launch failed'}
    $info=$launch -join "`n" | ConvertFrom-Json
    $appPid=[int]$info.ProcessId
    $owned=Get-Process -Id $appPid
    if([IO.Path]::GetFullPath($owned.Path) -ne [IO.Path]::GetFullPath("$layout/Harness.exe")){throw 'Path mismatch'}
    $ownedPath=$owned.Path
    @{pid=$appPid;path=$ownedPath;startedUtc=$started.ToUniversalTime().ToString('O')} | ConvertTo-Json | Set-Content "$out/identity.json"
    $package=Get-AppxPackage -Name $identity
    $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    if(Test-Path "$local/resize-finished.txt"){throw 'Stale result'}
    & winapp ui wait-for ResizeChecks -a $appPid -t 15000 --json | Set-Content "$out/ready.json"
    if($LASTEXITCODE -ne 0){throw 'UI readiness failed'}
    & winapp ui invoke $Action -a $appPid --json | Set-Content "$out/invoke.json"
    if($LASTEXITCODE -ne 0){throw 'Invoke failed'}
    $samples=[Collections.Generic.List[object]]::new()
    $deadline=(Get-Date).AddMinutes(5)
    while(!(Test-Path "$local/resize-finished.txt") -and (Get-Date) -lt $deadline){
        $p=Get-Process -Id $appPid -ErrorAction Stop
        $samples.Add(@{utc=(Get-Date).ToUniversalTime().ToString('O');cpu=$p.CPU;workingSet=$p.WorkingSet64;private=$p.PrivateMemorySize64;threads=$p.Threads.Count})
        Start-Sleep -Milliseconds 500
    }
    $samples | ConvertTo-Json | Set-Content "$out/process-samples.json"
    Copy-Item "$local/resize-*" $out
    if(!(Test-Path "$out/resize-finished.txt")){throw 'Timeout'}
    & winapp ui screenshot -a $appPid --output "$out/owned.png" --json | Set-Content "$out/screenshot.json"
    & winapp ui invoke Close -a $appPid --json | Set-Content "$out/close.json"
    $closed=Get-Date
    Start-Sleep -Seconds 32
    $still=Get-Process -Id $appPid -ErrorAction SilentlyContinue
    @{pid=$appPid;path=$ownedPath;closeUtc=$closed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$closed).TotalSeconds;absent=($null -eq $still)} | ConvertTo-Json | Set-Content "$out/exit.json"
    $wer=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 1000,1001 -and $_.Message -like "*$layout*" } | Select-Object TimeCreated,Id,Message)
    ConvertTo-Json -InputObject $wer | Set-Content "$out/wer.json"
} finally {
    if($appPid){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){Stop-Process -Id $appPid}}
    $package=Get-AppxPackage -Name $identity
    if($package -and [IO.Path]::GetFullPath($package.InstallLocation) -eq [IO.Path]::GetFullPath($layout)){Remove-AppxPackage -Package $package.PackageFullName}
    & winapp ui yield --json | Set-Content "$out/yield.json"
}
if(Test-Path "$out/resize-error.txt"){throw (Get-Content "$out/resize-error.txt" -Raw)}
$rows=Get-Content "$out/resize-results.json" -Raw | ConvertFrom-Json
if(@($rows).Count -ne 3 -or @($rows | Where-Object { !$_.pass }).Count){throw 'Resize responsiveness gate failed; raw measurements retained'}
