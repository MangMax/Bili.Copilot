param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Run,[ValidateSet('v2','v3')][string]$Protocol='v3',[switch]$Capture)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$commit=git -C $root rev-parse HEAD
$out="$root/artifacts/exit-reproduction/$Run"
if(Test-Path $out){throw 'Fresh run required'}
New-Item -ItemType Directory $out | Out-Null
$frozen="$Fixture/frozen-output"
$inputDir="$out/input"; $layout=[IO.Path]::GetFullPath("$out/layout")
$identity="Mang.BiliCopilot.TimerFix.E497.$Run"
$workflow="timerfix-e497-$Run"
if(Get-AppxPackage -Name $identity){throw 'Identity already registered'}
$env:WINAPP_UI_WORKFLOW_ID=$workflow
function Hashes($dir){ @(Get-ChildItem $dir -Recurse -Filter '*.dll' | ForEach-Object { "$([IO.Path]::GetRelativePath($dir,$_.FullName).ToLowerInvariant())=$((Get-FileHash $_.FullName).Hash)" }) }
$ledger=Get-Content "$Fixture/frozen-all-files.json" -Raw | ConvertFrom-Json
$expected=@($ledger | Where-Object path -Like '*.dll' | ForEach-Object { "$($_.path.ToLowerInvariant())=$($_.sha256)" })
if(Compare-Object $expected (Hashes $frozen)){throw 'Frozen DLL hash mismatch'}
if(Get-ChildItem $frozen -Recurse -Filter '*.appxrecipe'){throw 'Recipe prohibited'}
Copy-Item $frozen $inputDir -Recurse
[xml]$manifest=Get-Content "$inputDir/AppxManifest.xml"
$oldIdentity=$manifest.Package.Identity.Name
$manifest.Package.Identity.Name=$identity; $manifest.Save("$inputDir/AppxManifest.xml")
Copy-Item "$Fixture/source-provenance.txt" "$inputDir/source-provenance.txt"
@{run=$Run;fixture=$Fixture;identity=$identity;source=(Get-Content "$Fixture/source-provenance.txt" -Raw);runtime=(Get-Content "$frozen/Harness.runtimeconfig.json" -Raw | ConvertFrom-Json);dlls=$expected;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 8 | Set-Content "$out/provenance.json"
$collector=$null; $appPid=0; $observer=$null; $observerIdentity=$null
$started=Get-Date
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1 -ErrorAction Stop).RecordId
$target=$null
$observed=$false
function Write-ExitObservation {
 $script:observed=$true
 $terminated=$target -and $target.WaitForExit(15000)
 $exitCode=$null
 if($terminated){$exitCode=[uint32]([int64]$target.ExitCode -band 0xffffffffL)}
 $terminationUtc=[DateTime]::UtcNow.ToString('o')
 $terminationTicks=[Diagnostics.Stopwatch]::GetTimestamp()
 $xmls=@{}; $queries=@(); $queryComplete=$true
 $deadline=[DateTime]::UtcNow.AddSeconds(30)
 do {
  $ok=$true
  try {
   $records=@(Get-WinEvent -LogName Application -FilterXPath "*[System[(EventID=1000 or EventID=1001) and EventRecordID > $bookmark]]" -ErrorAction Stop)
   foreach($record in $records){$xmls[[string]$record.RecordId]=$record.ToXml()}
  } catch { if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){$ok=$false;$queryComplete=$false} }
  $queries+=@{utc=[DateTime]::UtcNow.ToString('o');ticks=[Diagnostics.Stopwatch]::GetTimestamp();ok=$ok;visibleRecords=@($xmls.Keys)}
  Start-Sleep -Milliseconds 500
 } while([DateTime]::UtcNow -lt $deadline)
 $trace=@()
 if(Test-Path "$state/exit-trace.jsonl"){$trace=@(Get-Content "$state/exit-trace.jsonl" | ForEach-Object {$_ | ConvertFrom-Json})}
 $request=@($trace | Where-Object stage -eq 'explicit-exit' | Select-Object -Last 1)
 $unhandled=@($trace | Where-Object stage -in 'domain-unhandled','xaml-unhandled').Count
 $record=@{pid=$appPid;exe="$layout/Harness.exe";package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$startUtc;handleRetained=($null -ne $target);exitCode=$exitCode;terminated=$terminated;queryComplete=$queryComplete;observationComplete=$true;collectionEndUtc=[DateTime]::UtcNow.ToString('o');terminationUtc=$terminationUtc;terminationTicks=$terminationTicks;launchTicks=$launchTicks;queries=$queries;exitRequest=$request;managedUnhandled=$unhandled}
 $record | ConvertTo-Json -Depth 8 | Set-Content "$out/exit-observation.json"
 ConvertTo-Json -InputObject @($xmls.Values) | Set-Content "$out/event-xml.json"
 python "$root/docs/performance-acceptance/net11-exit-reproduction/exit_gate.py" $out
 $script:gateCode=$LASTEXITCODE
}
try {
 $launch=& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe Harness.exe --output-appx-directory $layout --detach --json
 $launch | Set-Content "$out/launch.json"
 if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 if(!$appPid -or $appPid -eq 9608 -or [IO.Path]::GetFullPath((Get-Process -Id $appPid).Path) -ne [IO.Path]::GetFullPath("$layout/Harness.exe")){throw 'Not owned process'}
 $target=Get-Process -Id $appPid
 $retainedHandle=$target.Handle
 $creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc()
 $startUtc=$target.StartTime.ToUniversalTime().ToString('o')
 $launchTicks=[Diagnostics.Stopwatch]::GetTimestamp()
 if(Compare-Object $expected (Hashes $layout)){throw 'Two-way deployed DLL mismatch'}
 @{count=$expected.Count;missing=0;extra=0;mismatched=0} | ConvertTo-Json | Set-Content "$out/hash-gate.json"
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout){throw 'Registration mismatch'}
 $state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
 if($Capture){
  $collector=Start-Process (Get-Command python).Source -ArgumentList @("`"$root/docs/performance-acceptance/net11-player-timer-fix/capture-controls.py`"",$appPid,"`"$out`"") -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/collector.stdout" -RedirectStandardError "$out/collector.stderr"
  $readyDeadline=[DateTime]::UtcNow.AddSeconds(10)
  while(!(Test-Path "$out/collector-ready.json") -and [DateTime]::UtcNow -lt $readyDeadline){Start-Sleep -Milliseconds 100}
  if(!(Test-Path "$out/collector-ready.json")){throw 'Collector did not attach'}
 }
 $runner=(Get-Content "$Fixture/run-controls.ps1" -Raw).Replace($oldIdentity,$identity)
 $runner=$runner -replace "(?m)^\`$env:WINAPP_UI_WORKFLOW_ID=.*$",("`$env:WINAPP_UI_WORKFLOW_ID='$workflow'")
 Set-Content "$out/run-controls.ps1" $runner
 & "$out/run-controls.ps1" -AppPid $appPid -Layout $layout -Evidence "$out/controls" *> "$out/controls.log"
 if($LASTEXITCODE -ne 0){throw 'Controls checks failed'}
 $windows=& winapp ui list-windows --on local -a $appPid --json | ConvertFrom-Json
 $hwnd=($windows | Where-Object ownerHwnd -eq 0 | Select-Object -First 1).hwnd
 if(!$hwnd){throw 'No owned main window'}
 & winapp ui send-keys 'alt+F4' --via send-input --allow-system-keys --on local -a $appPid -w $hwnd --json | Set-Content "$out/close.json"
 if($LASTEXITCODE -ne 0){throw 'Owned close failed'}
 Write-ExitObservation
 if($gateCode -ne 0){throw 'Diagnostic target exit BLOCKED; see exit-verdict.json'}

} catch {
 @{error="$_";pid=$appPid;identity=$identity;layout=$layout;utc=[DateTime]::UtcNow.ToString('o');certification='STOP: preserve cause evidence; observer failure is not a root cause'} | ConvertTo-Json | Set-Content "$out/failure.json"
 if($state -and (Test-Path $state)){
  foreach($pattern in 'events.jsonl','*-results.json','*ready.json','idle-baseline.json'){
   Get-ChildItem -LiteralPath $state -Filter $pattern | Copy-Item -Destination $out
  }
 }
 @(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started} -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*$identity*" -or $_.Message -like "*$layout*" } | Select-Object TimeCreated,Id,ProviderName,Message) | ConvertTo-Json -Depth 6 | Set-Content "$out/windows-errors.json"
 throw
} finally {
 if($target -and !$observed){Write-ExitObservation}
 if($state -and (Test-Path "$state/exit-trace.jsonl")){Copy-Item "$state/exit-trace.jsonl" $out}
 if($state -and (Test-Path $state)){
  foreach($file in Get-ChildItem -LiteralPath $state -Filter 'observe-*.json'){Copy-Item $file.FullName $out}
 }
 if($observer -and !$observer.HasExited){Stop-Process -Id $observer.Id}
 if($observerIdentity){
  $child=Get-Process -Id $observerIdentity.observerPid -ErrorAction SilentlyContinue
  if($child -and [IO.Path]::GetFullPath($child.Path) -eq [IO.Path]::GetFullPath($observerIdentity.observerExe)){Stop-Process -Id $child.Id}
 }
 if($appPid -gt 0 -and $appPid -ne 9608){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){Stop-Process -Id $appPid}}
 $package=Get-AppxPackage -Name $identity
 if($package -and $package.InstallLocation -eq $layout){Remove-AppxPackage -Package $package.PackageFullName}
 if($collector -and !$collector.WaitForExit(5000)){throw 'Owned collector did not stop; explicit cleanup required'}
 if($target){$target.Dispose()}
 & winapp ui yield --on local --json | Set-Content "$out/yield.json"
 @{pid=$appPid;processAbsent=($appPid -eq 0 -or !(Get-Process -Id $appPid -ErrorAction SilentlyContinue));identity=$identity;registrationAbsent=!(Get-AppxPackage -Name $identity);observerLauncherPid=if($observer){$observer.Id}else{0};observerIdentity=$observerIdentity;observerAbsent=(!$observerIdentity -or !(Get-Process -Id $observerIdentity.observerPid -ErrorAction SilentlyContinue));utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content "$out/cleanup.json"
}
