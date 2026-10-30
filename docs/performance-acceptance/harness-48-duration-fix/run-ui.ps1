param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence,[switch]$Resume)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='offline48-durationfix-1d19'
$Layout=[IO.Path]::GetFullPath($Layout)
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Force $Evidence | Out-Null
if($AppPid -eq 9608 -or (Get-Process -Id $AppPid).Path -ne "$Layout\BiliCopilot.UI.exe"){throw 'Not our host process'}
$package=Get-AppxPackage -Name Mang.BiliCopilot.Offline48.D1d19DurationFix
if(!$package -or $package.InstallLocation -ne $Layout){throw 'Not our registered layout'}
$state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
if(Test-Path "$state/results.json"){throw 'Stale result exists; use a fresh owned host registration'}
function UI([string[]]$Arguments){$out=& winapp ui @Arguments --on local -a $AppPid --json; if($LASTEXITCODE -ne 0){throw ($out -join "`n")};$j=($out -join "`n")|ConvertFrom-Json;if(@($j | Where-Object {$_.error -or $_.timedOut}).Count){throw ($out -join "`n")};return $j}
$captures=[Collections.Generic.List[object]]::new()
try {
 if(!$Resume){UI @('wait-for','Status','--value','READY','-t','20000') | Out-Null; UI @('invoke','Run') | Out-Null}
 $seen=[Collections.Generic.HashSet[string]]::new();$deadline=[DateTime]::UtcNow.AddMinutes(8)
 while([DateTime]::UtcNow -lt $deadline){
  if(!(Get-Process -Id $AppPid -ErrorAction SilentlyContinue)){throw 'Host exited before results'}
  if(Test-Path "$state/results.json"){break}
  if(Test-Path "$state/capture.json"){
   $capture=Get-Content "$state/capture.json" -Raw | ConvertFrom-Json
   if($seen.Add($capture.name)){
    $hwnd=if($capture.hwnd){$capture.hwnd}else{$capture.hostHwnd}
    UI @('list-windows') | ConvertTo-Json -Depth 10 | Set-Content "$Evidence/$($capture.name)-windows.json"
    $shot=UI @('screenshot','-w',"$hwnd",'--capture-screen','--output',"$Evidence/$($capture.name).png")
    if(!(Test-Path "$Evidence/$($capture.name).png")){throw 'Screenshot absent'}
    $captures.Add(@{name=$capture.name;hwnd=$hwnd;capture=$shot})
    UI @('invoke','Continue','-w',"$($capture.hostHwnd)") | Out-Null
   }
  }
  Start-Sleep -Milliseconds 200
 }
 if(!(Test-Path "$state/results.json")){throw 'No final structured result'}
 Copy-Item "$state/results.json" $Evidence
 $result=Get-Content "$Evidence/results.json" -Raw | ConvertFrom-Json
 if(@($result).Count -ne 12){throw 'Expected twelve named cases'}
 $result | Format-Table name,status
} finally {
 foreach($name in 'events.jsonl','results.json','capture.json'){if(Test-Path "$state/$name"){Copy-Item "$state/$name" $Evidence -Force}}
 $captures | ConvertTo-Json -Depth 10 | Set-Content "$Evidence/captures.json"
 & winapp ui yield --on local --json | Set-Content "$Evidence/yield.json"
}
if(@($result | Where-Object status -eq FAIL).Count){exit 1}
