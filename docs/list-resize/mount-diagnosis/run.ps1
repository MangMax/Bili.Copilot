param([Parameter(Mandatory)][ValidatePattern("^[A-Za-z0-9][A-Za-z0-9_-]*$")][string]$Stage,[ValidateSet('MatrixRed','MatrixGreen','ExtraRed','ExtraGreen','ImageChecks','Focus','ResizeChecks')][string]$Action='MatrixRed',[string]$Family='Season',[string]$Cache='0.5',[switch]$Fixed,[switch]$Unique)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$hostDir="$root/artifacts/recycled-cards/host"
$out="$root/artifacts/recycled-cards/$Stage"
if(Test-Path $out){throw 'Fresh stage required'}
New-Item -ItemType Directory $out | Out-Null
Get-ChildItem $hostDir -File | Where-Object Extension -in '.cs','.xaml','.csproj' | Get-FileHash | ConvertTo-Json | Set-Content "$out/source-hashes.json"
New-Item -ItemType Directory "$out/sources" | Out-Null
Get-ChildItem $hostDir -File | Where-Object Extension -in '.cs','.xaml','.csproj' | Copy-Item -Destination "$out/sources"
if(Test-Path "$hostDir/probe-provenance.json"){Copy-Item "$hostDir/probe-provenance.json" $out}
Copy-Item "$hostDir/RecommendationCoverImage.cs" "$out/instrumented-cover.cs"
$manifest=[xml](Get-Content "$hostDir/Package.appxmanifest" -Raw)
$identity=[string]$manifest.Package.Identity.Name
if($identity -notmatch '^BiliCopilot\.Recycle[A-Za-z0-9]+\.Owned$'){throw 'Not a replay-owned identity'}
$layout="$out/layout"
if(Get-AppxPackage -Name $identity){throw 'Owned identity already registered'}
$env:WINAPP_UI_WORKFLOW_ID=$identity.ToLowerInvariant()
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
    $retainedHandle=$owned.Handle
    Get-ChildItem $layout -File | Where-Object Name -in 'Harness.dll','Harness.exe','Richasy.WinUIKernel.Share.dll','Microsoft.WinUI.dll' | Get-FileHash | ConvertTo-Json | Set-Content "$out/binary-hashes.json"
    git -C $root rev-parse HEAD | Set-Content "$out/head.txt"

    @{pid=$appPid;path=$ownedPath;startedUtc=$started.ToUniversalTime().ToString('O')} | ConvertTo-Json | Set-Content "$out/identity.json"
    $package=Get-AppxPackage -Name $identity
    $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    if(Test-Path "$local/matrix-finished.txt"){throw 'Stale result'}
    & winapp ui wait-for MatrixRed -a $appPid -t 15000 --json | Set-Content "$out/ready.json"
    if($LASTEXITCODE -ne 0){throw 'UI readiness failed'}
    if($Unique){"unique" | Set-Content "$local/mount-unique.txt"}
    if($Fixed){"fixed" | Set-Content "$local/mount-fixed.txt"}
    $Cache | Set-Content "$local/mount-cache.txt"
    $Family | Set-Content "$local/related-family.txt"
    & winapp ui invoke $Action -a $appPid --json | Set-Content "$out/invoke.json"
    if($LASTEXITCODE -ne 0){throw 'Invoke failed'}
    $samples=[Collections.Generic.List[object]]::new()
    $deadline=(Get-Date).AddMinutes(8)
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
    if($LASTEXITCODE -ne 0){throw 'Screenshot failed'}
    & winapp ui invoke Close -a $appPid --json | Set-Content "$out/close.json"
    if($LASTEXITCODE -ne 0){throw 'Close failed'}
    $closed=Get-Date
    Start-Sleep -Seconds 32
    $still=Get-Process -Id $appPid -ErrorAction SilentlyContinue
    @{pid=$appPid;path=$ownedPath;closeUtc=$closed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$closed).TotalSeconds;absent=($null -eq $still);retainedHasExited=$owned.HasExited;retainedExitCode=$(if($owned.HasExited){$owned.ExitCode}else{$null})} | ConvertTo-Json | Set-Content "$out/exit.json"
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
        @{pid=$appPid;path=$ownedPath;observationStart=$observed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$observed).TotalSeconds;absent=($null -eq $still);incomplete=$true;retainedHasExited=$owned.HasExited;retainedExitCode=$(if($owned.HasExited){$owned.ExitCode}else{$null})} | ConvertTo-Json | Set-Content "$out/exit.json"
    }
    if($appPid){
        $wer=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 1000,1001 -and $_.Message -like "*$layout*" } | Select-Object TimeCreated,Id,Message)
        ConvertTo-Json -InputObject $wer | Set-Content "$out/wer.json"
    }
    if($appPid){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){Stop-Process -Id $appPid}}
    $package=Get-AppxPackage -Name $identity
    if($package -and [IO.Path]::GetFullPath($package.InstallLocation) -eq [IO.Path]::GetFullPath($layout)){Remove-AppxPackage -Package $package.PackageFullName}
    @(Get-AppxPackage -Name $identity).Count | Set-Content "$out/remaining-owned-packages.txt"
    & winapp ui yield --json | Set-Content "$out/yield.json"
    if($LASTEXITCODE -ne 0){throw "Yield failed"}
}
if(Test-Path "$out/matrix-focus.json"){Get-Content "$out/matrix-focus.json"}

if(Test-Path "$out/matrix-error.txt"){throw (Get-Content "$out/matrix-error.txt" -Raw)}
if(Test-Path "$out/matrix-checks.json"){
    $failed=@(Get-Content "$out/matrix-checks.json" -Raw | ConvertFrom-Json | Where-Object {$_ -like '*/FAIL'})
    if($failed.Count){Write-Output $failed; exit 2}
}
