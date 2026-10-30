param([Parameter(Mandatory)][string]$Project,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence,[Parameter(Mandatory)][string]$TracePrefix,[switch]$CaptureLifecycle,[switch]$NoTrace,[switch]$ScreenCapture)
$ErrorActionPreference='Stop'
$Project=(Resolve-Path $Project).Path
$Layout=[IO.Path]::GetFullPath($Layout);$Evidence=[IO.Path]::GetFullPath($Evidence);$TracePrefix=[IO.Path]::GetFullPath($TracePrefix)
if((Test-Path $Layout) -or (Test-Path $Evidence) -or (Test-Path "$TracePrefix.ready")){throw 'Require fresh paths'}
if(Get-AppxPackage -Name BiliCopilot.GC47D0E5){throw 'Identity in use'}
New-Item -ItemType Directory $Evidence | Out-Null
$env:WINAPP_UI_WORKFLOW_ID='gc47-d0e5'
$ownedPid=0;$collector=$null;$errorText=$null
$trace=(Resolve-Path "$PSScriptRoot/Trace/bin/Release/net10.0/Trace.exe").Path
function Ui([string[]]$Arguments) { $raw=& winapp ui @Arguments -a $ownedPid --json; if($LASTEXITCODE -ne 0){throw "UI failed: $raw"}; $raw }
try {
 $raw=& winapp run $Project -c Release --no-build --arch x64 --output-appx-directory $Layout --detach --json
 $code=$LASTEXITCODE;$raw | Set-Content "$Evidence/launch.json";if($code -ne 0){throw 'Launch failed'}
 $launch=$raw|ConvertFrom-Json;$ownedPid=[int]$launch.ProcessId
 if($ownedPid -le 0 -or $ownedPid -eq 9608){throw 'Invalid owned PID'}
 $process=Get-Process -Id $ownedPid
 if(-not $process.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Process path mismatch'}
 Ui @('wait-for','Status','--value','Idle','-t','20000') | Out-Null
 $hwnd=[string]((Ui @('list-windows')|ConvertFrom-Json)|Where-Object title -eq 'GC47 controlled comparison'|Select-Object -First 1).hwnd
 if(-not $hwnd){throw 'Missing window'}
 $built=Get-ChildItem (Join-Path (Split-Path $Project) 'bin') -Filter Harness.dll -Recurse | Where-Object FullName -Match 'Release' | Select-Object -First 1
 $hash=Get-FileHash "$Layout/Harness.dll"
 if($hash.Hash -ne (Get-FileHash $built.FullName).Hash){throw 'Build/layout hash mismatch'}
 $process.Modules | Where-Object ModuleName -in @('Harness.dll','coreclr.dll','Microsoft.UI.Xaml.dll') | ForEach-Object { @{name=$_.ModuleName;path=$_.FileName;hash=(Get-FileHash $_.FileName).Hash;version=$_.FileVersionInfo.FileVersion} } | ConvertTo-Json | Set-Content "$Evidence/modules.json"
 @{project=$Project;layout=$Layout;pid=$ownedPid;dllHash=$hash.Hash;builtDll=$built.FullName;trace=(-not $NoTrace)} | ConvertTo-Json | Set-Content "$Evidence/provenance.json"
 Copy-Item (Join-Path (Split-Path $Project) 'copied-source-hashes.json') $Evidence
 for($step=1;$step -le 4;$step++) {
  if($step -eq 4 -and -not $NoTrace) {
   $collector=Start-Process $trace -ArgumentList @('collect',"$ownedPid",$TracePrefix,$Layout) -PassThru -WindowStyle Hidden -RedirectStandardError "$Evidence/collector-error.txt"
   $clock=[Diagnostics.Stopwatch]::StartNew()
   while(-not(Test-Path "$TracePrefix.ready")) {if($collector.HasExited -or $clock.Elapsed.TotalSeconds -gt 15){throw 'Collector did not become ready'};Start-Sleep -Milliseconds 100}
  }
  Ui @('invoke','Next') | Out-Null
  Ui @('wait-for','Status','--value',"Ready $step",'-t','55000') | Out-Null
  if($CaptureLifecycle -or $step -eq 4) { $captureArgs=@('screenshot','-w',$hwnd,'--output',"$Evidence/state-$step.png");if($ScreenCapture){$captureArgs+='--capture-screen'};Ui $captureArgs | Set-Content "$Evidence/state-$step.capture.json"}
 }
 Ui @('inspect','--depth','10') | Set-Content "$Evidence/final.uia.json"
} catch {
 $errorText="$_"
 if($ownedPid -gt 0 -and $ownedPid -ne 9608) {
  try {Ui @('inspect','--depth','10') | Set-Content "$Evidence/failure.uia.json";Ui @('screenshot','-w',$hwnd,'--output',"$Evidence/failure.png") | Set-Content "$Evidence/failure.capture.json"}catch{$_ | Out-String | Set-Content "$Evidence/failure-capture-error.txt"}
 }
}
finally {
 if($collector) {Set-Content "$TracePrefix.stop" 'stop';if(-not $collector.WaitForExit(20000)){throw 'Collector stop timeout'};if($collector.ExitCode -ne 0){$errorText="Collector exit $($collector.ExitCode): $errorText"}; & $trace parse "$TracePrefix.nettrace" "$Evidence/events.json";if($LASTEXITCODE -ne 0){$errorText="Trace parse failed: $errorText"};Get-FileHash "$TracePrefix.nettrace" | ConvertTo-Json | Set-Content "$Evidence/trace-hash.json"}
 if(Test-Path "$Layout/experiment"){Copy-Item "$Layout/experiment/*" $Evidence}
 if($ownedPid -gt 0 -and $ownedPid -ne 9608) {
  $p=Get-Process -Id $ownedPid -ErrorAction SilentlyContinue
  if($p) {if(-not $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup ownership mismatch'};Ui @('invoke','Close') | Out-Null;if(-not $p.WaitForExit(15000)){throw 'Owned close timeout'}}
 }
 $pkg=Get-AppxPackage -Name BiliCopilot.GC47D0E5
 if($pkg){if($pkg.InstallLocation -ne $Layout){throw 'Package ownership mismatch'};Remove-AppxPackage -Package $pkg.PackageFullName}
 & winapp ui yield --json | Set-Content "$Evidence/yield.json"
 @{error=$errorText;pid=$ownedPid;processAbsent=($ownedPid -eq 0 -or $null -eq (Get-Process -Id $ownedPid -ErrorAction SilentlyContinue));packageAbsent=($null -eq (Get-AppxPackage -Name BiliCopilot.GC47D0E5))} | ConvertTo-Json | Set-Content "$Evidence/result.json"
}
if($errorText){throw $errorText}
