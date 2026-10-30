param([Parameter(Mandatory)][string]$Project,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence,[Parameter(Mandatory)][string]$TracePrefix,[switch]$CaptureLifecycle,[switch]$NoTrace,[switch]$ScreenCapture)
$ErrorActionPreference='Stop'
$Project=(Resolve-Path $Project).Path
$Layout=[IO.Path]::GetFullPath($Layout);$Evidence=[IO.Path]::GetFullPath($Evidence);$TracePrefix=[IO.Path]::GetFullPath($TracePrefix)
if((Test-Path $Layout) -or (Test-Path $Evidence) -or (Test-Path "$TracePrefix.ready")){throw 'Require fresh paths'}
if(Get-AppxPackage -Name BiliCopilot.Cache56F577){throw 'Identity in use'}
New-Item -ItemType Directory $Evidence | Out-Null
$env:WINAPP_UI_WORKFLOW_ID='cache56-f577'
$ownedPid=0;$collector=$null;$errorText=$null
$trace=(Resolve-Path "$PSScriptRoot/../harness-47-gc/Trace/bin/Release/net10.0/Trace.exe").Path
function Ui([string[]]$Arguments) { $raw=& winapp ui @Arguments -a $ownedPid --json; if($LASTEXITCODE -ne 0){throw "UI failed ($Arguments): $raw"}; $raw }
try {
 $raw=& winapp run $Project -c Release --no-build --arch x64 -p Platform=x64 --output-appx-directory $Layout --detach --json
 $code=$LASTEXITCODE;$raw | Set-Content "$Evidence/launch.json";if($code -ne 0){throw 'Launch failed'}
 $launch=$raw|ConvertFrom-Json;$ownedPid=[int]$launch.ProcessId
 if($ownedPid -le 0 -or $ownedPid -eq 9608){throw 'Invalid owned PID'}
 $process=Get-Process -Id $ownedPid
 if(-not $process.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Process path mismatch'}
 Ui @('wait-for','Status','--value','Idle','-t','60000') | Out-Null
 $hwnd=[string]((Ui @('list-windows')|ConvertFrom-Json)|Where-Object title -eq 'SYNTHETIC #56 cache observation'|Select-Object -First 1).hwnd
 if(-not $hwnd){throw 'Missing window'}
 $built=Get-ChildItem (Join-Path (Split-Path $Project) 'bin') -Filter Harness.dll -Recurse | Where-Object FullName -Match 'Release' | Select-Object -First 1
 $hash=Get-FileHash "$Layout/Harness.dll"
 if($hash.Hash -ne (Get-FileHash $built.FullName).Hash){throw 'Build/layout hash mismatch'}
 $process.Modules | Where-Object ModuleName -in @('Harness.dll','coreclr.dll','Microsoft.UI.Xaml.dll') | ForEach-Object { @{name=$_.ModuleName;path=$_.FileName;hash=(Get-FileHash $_.FileName).Hash;version=$_.FileVersionInfo.FileVersion} } | ConvertTo-Json | Set-Content "$Evidence/modules.json"
 @{project=$Project;layout=$Layout;pid=$ownedPid;dllHash=$hash.Hash;builtDll=$built.FullName;trace=(-not $NoTrace)} | ConvertTo-Json | Set-Content "$Evidence/provenance.json"
 Copy-Item (Join-Path (Split-Path $Project) 'production-hashes.json'),(Join-Path (Split-Path $Project) 'fixture-hashes.json') $Evidence
 for($step=1;$step -le 11;$step++) {
  if($step -eq 1 -and -not $NoTrace) {
   $collector=Start-Process $trace -ArgumentList @('collect',"$ownedPid",$TracePrefix,$Layout) -PassThru -WindowStyle Hidden -RedirectStandardError "$Evidence/collector-error.txt"
   $clock=[Diagnostics.Stopwatch]::StartNew()
   while(-not(Test-Path "$TracePrefix.ready")) {if($collector.HasExited -or $clock.Elapsed.TotalSeconds -gt 15){throw 'Collector did not become ready'};Start-Sleep -Milliseconds 100}
  }
  Ui @('invoke','Next') | Out-Null
  $watch=[Diagnostics.Stopwatch]::StartNew()
  while($true){
   $records=@(Get-Content "$Layout/experiment/records.jsonl" | ForEach-Object { try { $_|ConvertFrom-Json } catch {} })
   if(@($records|Where-Object {$_.kind -eq 'step-pass' -and $_.data -eq $step}).Count -eq 1){break}
   $failed=@($records|Where-Object {$_.kind -in @('failure','unhandled')})
   if($failed.Count -gt 0 -or $watch.Elapsed.TotalSeconds -gt 420){throw "Protocol failed at step $step : $($failed|ConvertTo-Json -Depth 6)"}
   Start-Sleep -Milliseconds 250
  }
  if($CaptureLifecycle -or $step -in @(1,3,4,5,6,7,8,9,10,11)) { $captureArgs=@('screenshot','-w',$hwnd,'--output',"$Evidence/state-$step.png");if($ScreenCapture){$captureArgs+='--capture-screen'};Ui $captureArgs | Set-Content "$Evidence/state-$step.capture.json"}
 }
 $process=Get-Process -Id $ownedPid
 $process.Modules | Where-Object ModuleName -match '^(Harness|Richasy|BiliKernel|Microsoft.UI.Xaml|Microsoft.Graphics|ComputeSharp|Humanizer|coreclr)' | ForEach-Object { @{name=$_.ModuleName;path=$_.FileName;hash=(Get-FileHash $_.FileName).Hash;version=$_.FileVersionInfo.FileVersion} } | ConvertTo-Json | Set-Content "$Evidence/loaded-dependency-hashes.json"
 Get-ChildItem "$Layout/*.dll" | Where-Object Name -match '^(Harness|Richasy|BiliKernel|Microsoft.UI.Xaml|Microsoft.Graphics|ComputeSharp|CommunityToolkit|Humanizer|coreclr)' | ForEach-Object { @{name=$_.Name;hash=(Get-FileHash $_.FullName).Hash;version=$_.VersionInfo.FileVersion} } | ConvertTo-Json | Set-Content "$Evidence/dependency-hashes.json"
 Ui @('inspect','--depth','10') | Set-Content "$Evidence/final.uia.json"
} catch {
 $errorText="$_"
 if($ownedPid -gt 0 -and $ownedPid -ne 9608) {
  try {Ui @('inspect','--depth','10') | Set-Content "$Evidence/failure.uia.json";Ui @('screenshot','-w',$hwnd,'--output',"$Evidence/failure.png") | Set-Content "$Evidence/failure.capture.json"}catch{$_ | Out-String | Set-Content "$Evidence/failure-capture-error.txt"}
 }
}
finally {
 if($collector) {Set-Content "$TracePrefix.stop" 'stop';if(-not $collector.WaitForExit(20000)){throw 'Collector stop timeout'};if($collector.ExitCode -ne 0){$errorText="Collector exit $($collector.ExitCode): $errorText"}; & $trace parse "$TracePrefix.nettrace" "$TracePrefix.events.json";if($LASTEXITCODE -ne 0){$errorText="Trace parse failed: $errorText"};& python "$PSScriptRoot/compact.py" "$TracePrefix.events.json" "$Evidence/events.json";if($LASTEXITCODE -ne 0){$errorText="Compaction failed: $errorText"};Get-FileHash "$TracePrefix.nettrace" | ConvertTo-Json | Set-Content "$Evidence/trace-hash.json"}
 if(Test-Path "$Layout/experiment"){Copy-Item "$Layout/experiment/*" $Evidence}
 if($ownedPid -gt 0 -and $ownedPid -ne 9608) {
  $p=Get-Process -Id $ownedPid -ErrorAction SilentlyContinue
  if($p) {if(-not $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup ownership mismatch'};Ui @('invoke','Close') | Out-Null;if(-not $p.WaitForExit(15000)){throw 'Owned close timeout'}}
 }
 $pkg=Get-AppxPackage -Name BiliCopilot.Cache56F577
 if($pkg){if($pkg.InstallLocation -ne $Layout){throw 'Package ownership mismatch'};Remove-AppxPackage -Package $pkg.PackageFullName}
 & winapp ui yield --json | Set-Content "$Evidence/yield.json"
 @{error=$errorText;pid=$ownedPid;processAbsent=($ownedPid -eq 0 -or $null -eq (Get-Process -Id $ownedPid -ErrorAction SilentlyContinue));packageAbsent=($null -eq (Get-AppxPackage -Name BiliCopilot.Cache56F577))} | ConvertTo-Json | Set-Content "$Evidence/result.json"
}
if($errorText){throw $errorText}
