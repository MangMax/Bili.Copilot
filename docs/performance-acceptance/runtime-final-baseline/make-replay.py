"""Derive v3 replay, retaining its source and strengthening exit acceptance."""
import pathlib
HERE=pathlib.Path(__file__).resolve().parent
old=(HERE.parent/'net10-adaptation/replay.ps1').read_text(encoding='utf-8-sig')
def replace(a,b):
    global old
    assert old.count(a)==1, (a,old.count(a))
    old=old.replace(a,b)
replace('$out="$root/artifacts/net10-adaptation/$Run"','$out="$root/artifacts/runtime-final-baseline/$Run"')
replace('Mang.BiliCopilot.Runtime.N854.$Run','Mang.BiliCopilot.Runtime.Final4F1.$Run')
replace('runtime-n854-$Run','runtime-final4f1-$Run')
replace('$appPid=0; $observer=$null; $observerIdentity=$null', '''$appPid=0; $observer=$null; $observerIdentity=$null; $target=$null; $completed=$false
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1 -ErrorAction Stop).RecordId
function Background($name) {
 @{utc=[DateTime]::UtcNow.ToString('o');rows=@(Get-Process | ForEach-Object { @{pid=$_.Id;name=$_.ProcessName;cpuSeconds=$_.CPU;startedUtc=$(try{$_.StartTime.ToUniversalTime().ToString('o')}catch{$null})} })} | ConvertTo-Json -Depth 5 | Set-Content "$out/background-$name.json"
}
Background 'before'
''')
replace(' $package=Get-AppxPackage -Name $identity\n if($package.InstallLocation', ''' $target=Get-Process -Id $appPid
 $handle=$target.Handle; $creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc(); $start=$target.StartTime.ToUniversalTime().ToString('o')
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation''')
replace(' foreach($name in \'run-ui.ps1\',\'run-main.ps1\'){', ''' @{pid=$appPid;exe="$layout/BiliCopilot.UI.exe";creationFileTime=$creation;startUtc=$start;handleRetained=($handle -ne 0);package=$package.PackageFullName} | ConvertTo-Json | Set-Content "$out/ready.json"
 foreach($name in 'run-ui.ps1','run-main.ps1'){''')
replace('Copy-Item "$PSScriptRoot/$observerScript"', 'Copy-Item "$PSScriptRoot/../net10-adaptation/$observerScript"')
replace(' @{explicitExit=$true;pid=$appPid;protocol="external-os-$Protocol"} | ConvertTo-Json | Set-Content "$out/accepted.json"', ' $completed=$true')
replace('} finally {', '''} catch {
 @{error="$($_.Exception.Message)";utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$out/failure.json"
} finally {
 if($target) {
  $forced=$false
  if(!$target.HasExited) {
   # Cleanup only the retained, already validated owned handle; never another PID.
   $forced=$true; $target.Kill(); $target.WaitForExit(15000)|Out-Null
  }
  if($target.HasExited) {
   $code=[uint32]([int64]$target.ExitCode -band 0xffffffffL)
   $termination=[DateTime]::UtcNow; $watch=[Diagnostics.Stopwatch]::StartNew()
   @{pid=$appPid;creationFileTime=$creation;exitCode=$code;handleRetained=($handle -ne 0);terminationUtc=$termination.ToString('o');forced=$forced} | ConvertTo-Json | Set-Content "$out/exit-initial.json"
   $events=@{};$queries=@();$complete=$true
   do {
    $queryStart=$watch.Elapsed.TotalSeconds;$ok=$true
    try {
     $records=@(Get-WinEvent -LogName Application -FilterXPath "*[System[(EventID=1000 or EventID=1001) and EventRecordID > $bookmark]]" -ErrorAction Stop)
     foreach($r in $records) {
      [xml]$xml=$r.ToXml();$d=@{};foreach($n in $xml.Event.EventData.Data){$d[$n.Name]=[string]$n.'#text'}
      if($d.AppPath -eq "$layout/BiliCopilot.UI.exe" -or $d.P1 -eq $package.PackageFullName) {
       $safe=@{};foreach($k in 'AppPath','ProcessId','ProcessCreationTime','IntegratorReportId','ReportId','P1','ExceptionCode','ModuleName'){if($d.ContainsKey($k)){$safe[$k]=$d[$k]}}
       $events[[string]$r.RecordId]=@{id=$r.Id;record=$r.RecordId;utc=$r.TimeCreated.ToUniversalTime().ToString('o');data=$safe}
      }
     }
    } catch {if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){$ok=$false;$complete=$false}}
    $lastQuery=[DateTime]::UtcNow.ToString('o');$queries+=@{utc=$lastQuery;startedSecondsAfterExit=$queryStart;ok=$ok}
    if($queryStart -ge 30){break};Start-Sleep -Milliseconds 500
   } while($true)
   $unhandled=$null
   if(Test-Path "$state/events.jsonl") {
    $unhandled=@(Get-Content "$state/events.jsonl" | ForEach-Object {$_|ConvertFrom-Json} | Where-Object {$_.kind -eq 'logger' -and $_.data.message -eq 'Unhandled exception occurred.'}).Count
   }
   @{pid=$appPid;exe="$layout/BiliCopilot.UI.exe";package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$start;handleRetained=($handle -ne 0);exitCode=$code;terminated=$true;forced=$forced;queryComplete=$complete;observationComplete=($queries[-1].startedSecondsAfterExit -ge 30);terminationUtc=$termination.ToString('o');lastQueryUtc=$lastQuery;collectionEndUtc=[DateTime]::UtcNow.ToString('o');queries=$queries;managedUnhandled=$unhandled} | ConvertTo-Json -Depth 6 | Set-Content "$out/exit-observation.json"
   ConvertTo-Json -InputObject @($events.Values) -Depth 6 | Set-Content "$out/events-safe.json"
  }
  $target.Dispose()
 }
 Background 'after'
''')
old+='''
& python -B "$PSScriptRoot/gate.py" $out
if($LASTEXITCODE -ne 0 -or !$completed){throw 'Run not accepted; stop sequence and retain evidence'}
@{explicitExit=$true;pid=$appPid;protocol="external-os-$Protocol"} | ConvertTo-Json | Set-Content "$out/accepted.json"
'''
(HERE/'replay.ps1').write_text(old,encoding='utf-8')
