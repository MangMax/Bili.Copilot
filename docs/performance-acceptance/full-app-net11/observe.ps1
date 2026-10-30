param([Parameter(Mandatory)][string]$Run)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$layout="$root/artifacts/full-app-net11/layout-v2"
$out="$PSScriptRoot/$Run"
New-Item -ItemType Directory $out -ErrorAction Stop | Out-Null
$package=Get-AppxPackage -Name Mang.BiliCopilot.PerfAcceptance.Caa6a7be
if($package.InstallLocation -ne [IO.Path]::GetFullPath($layout) -or $package.Publisher -ne 'CN=BiliCopilotDev'){throw 'Wrong registration'}
$exe=[IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")
if(@(Get-Process | Where-Object Path -eq $exe).Count){throw 'Already running'}
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1).RecordId
Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App'
$deadline=[DateTime]::UtcNow.AddSeconds(90)
do {$target=Get-Process | Where-Object Path -eq $exe; if(!$target){Start-Sleep -Milliseconds 100}}while(!$target -and [DateTime]::UtcNow -lt $deadline)
if(!$target -or @($target).Count -ne 1 -or $target.Id -eq 9608){throw 'Owned target unavailable'}
$handle=$target.Handle
$creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc()
$start=$target.StartTime.ToUniversalTime().ToString('o')
@{pid=$target.Id;exe=$exe;creationFileTime=$creation;startUtc=$start;observerPid=$PID;handleRetained=($handle -ne 0)} | ConvertTo-Json | Set-Content "$out/ready.json"
$target.WaitForExit()
$code=[uint32]([int64]$target.ExitCode -band 0xffffffffL)
$terminated=[DateTime]::UtcNow
$events=@{}; $queries=@(); $complete=$true
$deadline=$terminated.AddSeconds(30)
do {
 $ok=$true
 try {
  $records=@(Get-WinEvent -LogName Application -FilterXPath "*[System[(EventID=1000 or EventID=1001) and EventRecordID > $bookmark]]" -ErrorAction Stop)
  foreach($r in $records){
   [xml]$xml=$r.ToXml(); $d=@{}; foreach($n in $xml.Event.EventData.Data){$d[$n.Name]=[string]$n.'#text'}
   # Persist only package/path-associated whitelisted crash fields; never Message, URL or dump.
   if($d.AppPath -eq $exe -or $d.P1 -eq $package.PackageFullName){
    $safe=@{}; foreach($k in 'AppPath','ProcessId','ProcessCreationTime','IntegratorReportId','ReportId','P1','ExceptionCode','ModuleName'){if($d.ContainsKey($k)){$safe[$k]=$d[$k]}}
    $events[[string]$r.RecordId]=@{id=$r.Id;record=$r.RecordId;utc=$r.TimeCreated.ToUniversalTime().ToString('o');data=$safe}
   }
  }
 }catch{if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){$ok=$false;$complete=$false}}
 $queries+=@{utc=[DateTime]::UtcNow.ToString('o');ok=$ok}
 Start-Sleep -Milliseconds 500
}while([DateTime]::UtcNow -lt $deadline)
@{pid=$target.Id;exe=$exe;package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$start;handleRetained=($handle -ne 0);exitCode=$code;terminated=$true;forced=$false;queryComplete=$complete;observationComplete=$true;collectionEndUtc=[DateTime]::UtcNow.ToString('o');terminationUtc=$terminated.ToString('o');queries=$queries;managedUnhandled=$null;managedLogScan='pending'} | ConvertTo-Json -Depth 6 | Set-Content "$out/exit-observation.json"
ConvertTo-Json -InputObject @($events.Values) -Depth 6 | Set-Content "$out/events-safe.json"
$target.Dispose()
