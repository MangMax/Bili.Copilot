"""Versioned transformation of sealed RC runner; never overwrites historical evidence."""
import pathlib
import subprocess

root = pathlib.Path.cwd()
script = subprocess.check_output(['git', 'show', 'e55180:docs/performance-acceptance/net11-primary/replay.ps1']).decode()
script = script.replace('$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")', "$root='" + str(root) + "'")
script = script.replace('$out="$root/artifacts/net11-primary/$commit/$Run"', '$out="$root/artifacts/exit-reproduction/$Run"')
script = script.replace('Mang.BiliCopilot.Runtime.R669.', 'Mang.BiliCopilot.ExitDiag.XBC1.')
script = script.replace('runtime-r669-', 'exitdiag-xbc1-')
script = script.replace("[string]$Protocol='v3')", "[string]$Protocol='v3',[switch]$Capture)")
script = script.replace('$appPid=0;', '$collector=$null; $appPid=0;')
script = script.replace(" foreach($name in 'run-ui.ps1','run-main.ps1'){", ''' if($Capture){
  $collector=Start-Process (Get-Command python).Source -ArgumentList @("`"$root/docs/performance-acceptance/net11-exit-reproduction/capture_owned.py`"",$appPid,"`"$out`"") -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/collector.stdout" -RedirectStandardError "$out/collector.stderr"
  $readyDeadline=[DateTime]::UtcNow.AddSeconds(10)
  while(!(Test-Path "$out/collector-ready.json") -and [DateTime]::UtcNow -lt $readyDeadline){Start-Sleep -Milliseconds 100}
  if(!(Test-Path "$out/collector-ready.json")){throw 'Collector did not attach'}
 }
 foreach($name in 'run-ui.ps1','run-main.ps1'){''')
script = script.replace('$started=Get-Date', '''$started=Get-Date
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1 -ErrorAction Stop).RecordId
$target=$null
$observed=$false''')
needle = ' if(Compare-Object $expected (Hashes $layout))'
script = script.replace(needle, ''' $target=Get-Process -Id $appPid
 $retainedHandle=$target.Handle
 $creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc()
 $startUtc=$target.StartTime.ToUniversalTime().ToString('o')
 $launchTicks=[Diagnostics.Stopwatch]::GetTimestamp()
''' + needle)
start = script.index(' # Process disappearance')
end = script.index('\n} catch {', start)
script = script[:start] + ''' Write-ExitObservation
 if($gateCode -ne 0){throw 'Diagnostic target exit BLOCKED; see exit-verdict.json'}
''' + script[end:]
script = script.replace('try {\n $launch=', '''function Write-ExitObservation {
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
 $record=@{pid=$appPid;exe="$layout/BiliCopilot.UI.exe";package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$startUtc;handleRetained=($null -ne $target);exitCode=$exitCode;terminated=$terminated;queryComplete=$queryComplete;observationComplete=$true;collectionEndUtc=[DateTime]::UtcNow.ToString('o');terminationUtc=$terminationUtc;terminationTicks=$terminationTicks;launchTicks=$launchTicks;queries=$queries;exitRequest=$request;managedUnhandled=$unhandled}
 $record | ConvertTo-Json -Depth 8 | Set-Content "$out/exit-observation.json"
 ConvertTo-Json -InputObject @($xmls.Values) | Set-Content "$out/event-xml.json"
 python "$root/docs/performance-acceptance/net11-exit-reproduction/exit_gate.py" $out
 $script:gateCode=$LASTEXITCODE
}
try {
 $launch=''')
script = script.replace('} finally {\n if($state', '''} finally {
 if($target -and !$observed){Write-ExitObservation}
 if($state -and (Test-Path "$state/exit-trace.jsonl")){Copy-Item "$state/exit-trace.jsonl" $out}
 if($state''')
script = script.replace('"$PSScriptRoot/../net10-adaptation/$observerScript"', '"$root/docs/performance-acceptance/net10-adaptation/$observerScript"')
script = script.replace(' & winapp ui yield --on local --json', ''' if($collector -and !$collector.WaitForExit(5000)){throw 'Owned collector did not stop; explicit cleanup required'}
 if($target){$target.Dispose()}
 & winapp ui yield --on local --json''')
(root / 'artifacts/exit-reproduction/replay.ps1').write_text(script, encoding='utf-8')
