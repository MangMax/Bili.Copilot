param([Parameter(Mandatory)][string]$Project,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
if(-not (Test-Path -LiteralPath $Project -PathType Leaf)){throw 'Prepared project missing'}
if(Test-Path -LiteralPath $Layout){throw 'Use a fresh non-existing layout directory'}
if(Test-Path -LiteralPath "$Evidence/results.json"){throw 'Do not overwrite previous evidence'}
if(Get-AppxPackage -Name BiliCopilot.Acceptance52Resources741){throw 'Test identity already registered; do not take over another run'}
$env:WINAPP_UI_WORKFLOW_ID='acceptance-52-resources-741'
$ownedPid=0
$results=[Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments) {
 $raw=& winapp ui @Arguments -a $ownedPid --json
 if($LASTEXITCODE -ne 0){throw "winapp ui $Arguments failed: $raw"}
 $raw
}
try {
 $raw=& winapp run $Project --no-build --arch x64 --output-appx-directory $Layout --detach --json
 $raw | Set-Content "$Evidence/launch.json"
 if($LASTEXITCODE -ne 0){throw "Launch failed: $raw"}
 $launch=$raw | ConvertFrom-Json
 if($launch.Error -or -not $launch.ProcessId){throw 'Launch did not return an owned PID'}
 $ownedPid=[int]$launch.ProcessId
 $p=Get-Process -Id $ownedPid
 if($ownedPid -in @(0,9608,4780) -or -not $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned process guard failed'}
 Ui @('wait-for','Status','--value','Idle','-t','20000') | Out-Null
 $windows=Ui @('list-windows') | ConvertFrom-Json
 $ownedHwnd=[string]($windows | Where-Object title -eq '52 SVG and resource experiment 741' | Select-Object -First 1).hwnd
 if(-not $ownedHwnd){throw 'Owned window not found'}
 $names=@('svg-comparison','portrait-complete','dpi-left','dpi-right','resources-complete')
 for($i=0;$i -lt $names.Count;$i++) {
  Ui @('invoke','Next') | Out-Null
  Ui @('wait-for','Status','--value',"Ready $($i+1)",'-t','55000') | Out-Null
  Ui @('screenshot','-w',$ownedHwnd,'--output',"$Evidence/$($names[$i]).png") | Set-Content "$Evidence/$($names[$i]).capture.json"
  Ui @('inspect','--depth','12') | Set-Content "$Evidence/$($names[$i]).uia.json"
  $results.Add(@{name=$names[$i];ready=$true})
 }
} catch { $results.Add(@{error="$_"}) }
finally {
 if(Test-Path "$Layout/experiment") { Get-ChildItem "$Layout/experiment" -File | Where-Object Extension -ne '.bmp' | Copy-Item -Destination $Evidence }
 if(Test-Path "$Layout/Harness.dll") { Get-FileHash "$Layout/Harness.dll" | ConvertTo-Json | Set-Content "$Evidence/dll-hash.json" }
 try {
  $p=if($ownedPid){Get-Process -Id $ownedPid -ErrorAction SilentlyContinue}else{$null}
  if($p) {
   if($ownedPid -in @(9608,4780) -or -not $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup ownership guard failed'}
   Ui @('invoke','Close') | Out-Null
   if(-not $p.WaitForExit(15000)){throw 'Owned app did not close'}
  }
  $package=Get-AppxPackage -Name BiliCopilot.Acceptance52Resources741
  if($package){if($package.InstallLocation -ne $Layout){throw 'Package layout mismatch'};Remove-AppxPackage -Package $package.PackageFullName}
  $absent=if($ownedPid -gt 0){$null -eq (Get-Process -Id $ownedPid -ErrorAction SilentlyContinue)}else{$true}
  @{pid=$ownedPid;processAbsent=$absent;packageAbsent=($null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance52Resources741))} | ConvertTo-Json | Set-Content "$Evidence/cleanup.json"
 } catch { $results.Add(@{cleanupError="$_"}) }
 & winapp ui yield --json | Set-Content "$Evidence/yield.json"
 $results.ToArray() | ConvertTo-Json -Depth 6 | Set-Content "$Evidence/results.json"
}
if(@($results | Where-Object {$_.error -or $_.cleanupError}).Count){exit 1}
