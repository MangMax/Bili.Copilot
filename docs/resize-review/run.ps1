param([string]$Stage='run')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
$out="$root/artifacts/resize-review/$Stage"
if(Test-Path $out){throw 'Fresh output required'}
New-Item -ItemType Directory $out | Out-Null
$identity='BiliCopilot.ResizeReviewB5d1.Owned'
if(Get-AppxPackage -Name $identity){throw 'Identity already exists'}
$layout="$out/layout"
$env:WINAPP_UI_WORKFLOW_ID='resize-review-b5d1'
$appPid=0;$ownedPath=$null;$started=Get-Date
function Checked([string[]]$Arguments){$result=& winapp @Arguments;if($LASTEXITCODE -ne 0){throw "winapp failed: $($Arguments -join ' '): $result"};$result}
try {
    $launch=Checked @('run',"$root/artifacts/resize-review/native/Harness.csproj",'-c','Release','--arch','x64','-p','Platform=x64','--no-build','--output-appx-directory',$layout,'--detach','--json')
    $launch | Set-Content "$out/launch.json"
    $appPid=[int](($launch -join "`n"|ConvertFrom-Json).ProcessId)
    $ownedPath=(Get-Process -Id $appPid).Path
    if([IO.Path]::GetFullPath($ownedPath) -ne [IO.Path]::GetFullPath("$layout/Harness.exe")){throw 'Path mismatch'}
    $package=Get-AppxPackage -Name $identity
    $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    if(Test-Path "$local/finished.txt"){throw 'Stale output'}
    Checked @('ui','wait-for','Run','-a',"$appPid",'-t','15000','--json') | Set-Content "$out/ready.json"
    Checked @('ui','invoke','Run','-a',"$appPid",'--json') | Set-Content "$out/invoke.json"
    $deadline=(Get-Date).AddSeconds(45)
    while(!(Test-Path "$local/finished.txt") -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 500}
    Copy-Item "$local/*.json" $out
    if(Test-Path "$local/unhandled.txt"){Copy-Item "$local/unhandled.txt" $out;throw 'Unhandled exception'}
    if(!(Test-Path "$local/finished.txt")){throw 'Native checks timed out'}
    Checked @('ui','screenshot','-a',"$appPid",'--output',"$out/owned.png",'--json') | Set-Content "$out/screenshot.json"
    Checked @('ui','invoke','Close','-a',"$appPid",'--json') | Set-Content "$out/close.json"
    $closed=Get-Date
    Start-Sleep -Seconds 32
    $absent=$null -eq (Get-Process -Id $appPid -ErrorAction SilentlyContinue)
    @{pid=$appPid;path=$ownedPath;closeUtc=$closed.ToUniversalTime().ToString('O');observedSeconds=((Get-Date)-$closed).TotalSeconds;absent=$absent} | ConvertTo-Json | Set-Content "$out/exit.json"
    $wer=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object {$_.Id -in 1000,1001 -and $_.Message -like "*$layout*"} | Select-Object TimeCreated,Id,Message)
    ConvertTo-Json -InputObject $wer | Set-Content "$out/wer.json"
    if(!$absent -or $wer.Count){throw 'Exit/WER check failed'}
    $checks=Get-Content "$out/checks.json" -Raw|ConvertFrom-Json
    if($checks.Count -ne 6 -or @($checks|Where-Object status -ne PASS).Count){throw 'Native checks failed'}
} finally {
    if($appPid){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue;if($p -and $p.Path -eq $ownedPath){Stop-Process -Id $appPid}}
    $package=Get-AppxPackage -Name $identity
    if($package -and [IO.Path]::GetFullPath($package.InstallLocation) -eq [IO.Path]::GetFullPath($layout)){Remove-AppxPackage -Package $package.PackageFullName}
    Checked @('ui','yield','--json') | Set-Content "$out/yield.json"
}
