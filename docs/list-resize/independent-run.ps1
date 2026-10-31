param([Parameter(Mandatory)][string]$Stage,[ValidateSet('MatrixRed','MatrixGreen','ExtraRed','ExtraGreen','ImageChecks','Focus')][string]$Action='MatrixRed')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
$hostDir="$root/artifacts/list-review/host"
$out="$root/artifacts/list-review/$Stage"
if(Test-Path $out){throw 'Fresh stage required'}
New-Item -ItemType Directory $out | Out-Null
Get-ChildItem $hostDir -File | Where-Object Extension -in '.cs','.xaml','.csproj' | Get-FileHash | ConvertTo-Json | Set-Content "$out/source-hashes.json"
New-Item -ItemType Directory "$out/sources" | Out-Null
Get-ChildItem $hostDir -File | Where-Object Extension -in '.cs','.xaml','.csproj' | Copy-Item -Destination "$out/sources"
if(Test-Path "$hostDir/probe-provenance.json"){Copy-Item "$hostDir/probe-provenance.json" $out}
Copy-Item "$hostDir/RecommendationCoverImage.cs" "$out/instrumented-cover.cs"
$identity='BiliCopilot.ListReview3301.Owned'
$layout="$out/layout"
if(Get-AppxPackage -Name $identity){throw 'Owned identity already registered'}
$env:WINAPP_UI_WORKFLOW_ID='list-review-3301'
$appPid=0
$local=$null
$samples=$null
$ownedPath=$null
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
    if(Test-Path "$local/matrix-finished.txt"){throw 'Stale result'}
    & winapp ui wait-for MatrixRed -a $appPid -t 15000 --json | Set-Content "$out/ready.json"
    if($LASTEXITCODE -ne 0){throw 'UI readiness failed'}
    & winapp ui invoke $Action -a $appPid --json | Set-Content "$out/invoke.json"
    if($LASTEXITCODE -ne 0){throw 'Invoke failed'}
    $samples=[Collections.Generic.List[object]]::new()
    $deadline=(Get-Date).AddSeconds(45)
    while(!(Test-Path "$local/matrix-finished.txt") -and (Get-Date) -lt $deadline){
        if((Test-Path "$local/trace.jsonl") -and (Select-String -LiteralPath "$local/trace.jsonl" -Pattern '"kind":"unhandled"' -Quiet)){throw 'Unhandled exception; partial evidence retained'}
        $p=Get-Process -Id $appPid -ErrorAction Stop
        $samples.Add(@{utc=(Get-Date).ToUniversalTime().ToString('O');cpu=$p.CPU;workingSet=$p.WorkingSet64;private=$p.PrivateMemorySize64;threads=$p.Threads.Count})
        Start-Sleep -Milliseconds 500
    }
    $samples | ConvertTo-Json | Set-Content "$out/process-samples.json"
    Copy-Item "$local/matrix-*" $out
    if(!(Test-Path "$out/matrix-finished.txt")){throw 'Timeout'}
    & winapp ui screenshot -a $appPid --output "$out/owned.png" --json | Set-Content "$out/screenshot.json"
    & winapp ui invoke Close -a $appPid --json | Set-Content "$out/close.json"
    $closed=Get-Date
    Start-Sleep -Seconds 32
    $still=Get-Process -Id $appPid -ErrorAction SilentlyContinue
    @{pid=$appPid;path=$ownedPath;closeUtc=$closed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$closed).TotalSeconds;absent=($null -eq $still)} | ConvertTo-Json | Set-Content "$out/exit.json"
    $wer=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 1000,1001 -and $_.Message -like "*$layout*" } | Select-Object TimeCreated,Id,Message)
    ConvertTo-Json -InputObject $wer | Set-Content "$out/wer.json"
} finally {
    # Preserve partial evidence even if the app exits before the completion marker.
    if($local -and (Test-Path $local)){
        Get-ChildItem $local -File | Where-Object { $_.Name -like 'matrix-*' -or $_.Name -eq 'trace.jsonl' } | Copy-Item -Destination $out
    }
    if($samples){$samples | ConvertTo-Json | Set-Content "$out/process-samples.json"}
    if($appPid -and !(Test-Path "$out/exit.json")){
        $p=Get-Process -Id $appPid -ErrorAction SilentlyContinue
        if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){[void]$p.CloseMainWindow()}
        $observed=Get-Date
        Start-Sleep -Seconds 32
        $still=Get-Process -Id $appPid -ErrorAction SilentlyContinue
        @{pid=$appPid;path=$ownedPath;observationStart=$observed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$observed).TotalSeconds;absent=($null -eq $still);incomplete=$true} | ConvertTo-Json | Set-Content "$out/exit.json"
    }
    if($appPid){
        $wer=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 1000,1001 -and $_.Message -like "*$layout*" } | Select-Object TimeCreated,Id,Message)
        ConvertTo-Json -InputObject $wer | Set-Content "$out/wer.json"
    }
    if($appPid){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){Stop-Process -Id $appPid}}
    $package=Get-AppxPackage -Name $identity
    if($package -and [IO.Path]::GetFullPath($package.InstallLocation) -eq [IO.Path]::GetFullPath($layout)){Remove-AppxPackage -Package $package.PackageFullName}
    & winapp ui yield --json | Set-Content "$out/yield.json"
}
if(Test-Path "$out/matrix-focus.json"){Get-Content "$out/matrix-focus.json"}
