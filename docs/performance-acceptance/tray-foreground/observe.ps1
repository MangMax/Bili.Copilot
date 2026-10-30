param([Parameter(Mandatory)][string]$Run,[switch]$LoggedApp,[string]$FixturePath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$base="$root/artifacts/tray-foreground";$out="$base/$Run"
New-Item -ItemType Directory $out -ErrorAction Stop|Out-Null
$layout=[IO.Path]::GetFullPath("$out/layout");$inputDir="$base/fixture/frozen-output"
if($FixturePath){$inputDir=[IO.Path]::GetFullPath("$FixturePath/frozen-output")}
$identity='Mang.BiliCopilot.TrayDiag.EE493'
if(!$LoggedApp -and (Get-AppxPackage -Name $identity)){throw 'Existing owned registration; do not overwrite'}
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1).RecordId
if($LoggedApp){
 $identity='Mang.BiliCopilot.PerfAcceptance.Caa6a7be'
 $layout=[IO.Path]::GetFullPath("$root/../t3-4d615e72/artifacts/full-app-net11/layout-v2")
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout -or $package.Version -ne '2.2610.0.2'){throw 'Protected registration mismatch'}
 $exe=[IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")
 if(@(Get-Process|Where-Object Path -eq $exe).Count){throw 'Protected app already running'}
 Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$($package.PackageFamilyName)!App"
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 do{$owned=@(Get-Process|Where-Object Path -eq $exe);if(!$owned.Count){Start-Sleep -Milliseconds 100}}while(!$owned.Count -and [DateTime]::UtcNow -lt $deadline)
 if($owned.Count -ne 1){throw 'No unique logged app process'}
 $appPid=$owned[0].Id
}else{
$launch=@(& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $layout --detach --json)
$launch|Set-Content "$out/launch.json";if($LASTEXITCODE -ne 0){throw 'Launch failed'}
$appPid=($launch -join "`n"|ConvertFrom-Json).ProcessId
}
$target=Get-Process -Id $appPid;$exe=[IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")
if($appPid -eq 9608 -or $target.Path -ne $exe){throw 'Wrong process'}
$handle=$target.Handle;$creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc();$start=$target.StartTime.ToUniversalTime().ToString('o')
$package=Get-AppxPackage -Name $identity
if($package.InstallLocation -ne $layout){throw 'Wrong registration'}
$state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
@{pid=$appPid;exe=$exe;creationFileTime=$creation;startUtc=$start;observerPid=$PID;handleRetained=($handle -ne 0);state=$state;package=$package.PackageFullName;aumid="$($package.PackageFamilyName)!App"}|ConvertTo-Json|Set-Content "$out/ready.json"
$target.WaitForExit()
$code=[uint32]([int64]$target.ExitCode -band 0xffffffffL);$termination=[DateTime]::UtcNow;$watch=[Diagnostics.Stopwatch]::StartNew()
@{pid=$appPid;creationFileTime=$creation;exitCode=$code;handleRetained=($handle -ne 0);terminationUtc=$termination.ToString('o')}|ConvertTo-Json|Set-Content "$out/exit-initial.json"
$unhandled=$null;$copyError=$null
if(!$LoggedApp){
 try{Copy-Item "$state/tray-probe.jsonl","$state/tray-ready.json" $out;$unhandled=@(Get-Content "$out/tray-probe.jsonl"|ConvertFrom-Json|Where-Object kind -eq unhandled).Count}catch{$copyError="$_"}
}
$events=@{};$queries=@();$complete=$true
do{
 $queryStart=$watch.Elapsed.TotalSeconds;$ok=$true
 try{
  $records=@(Get-WinEvent -LogName Application -FilterXPath "*[System[(EventID=1000 or EventID=1001) and EventRecordID > $bookmark]]" -ErrorAction Stop)
  foreach($r in $records){
   [xml]$xml=$r.ToXml();$d=@{};foreach($n in $xml.Event.EventData.Data){$d[$n.Name]=[string]$n.'#text'}
   if($d.AppPath -eq $exe -or $d.P1 -eq $package.PackageFullName){
    $safe=@{};foreach($k in 'AppPath','ProcessId','ProcessCreationTime','IntegratorReportId','ReportId','P1','ExceptionCode','ModuleName'){if($d.ContainsKey($k)){$safe[$k]=$d[$k]}}
    $events[[string]$r.RecordId]=@{id=$r.Id;record=$r.RecordId;utc=$r.TimeCreated.ToUniversalTime().ToString('o');data=$safe}
   }
  }
 }catch{if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){$ok=$false;$complete=$false}}
 $lastQuery=[DateTime]::UtcNow.ToString('o');$queries+=@{utc=$lastQuery;startedSecondsAfterExit=$queryStart;ok=$ok}
 if($queryStart -ge 30){break};Start-Sleep -Milliseconds 500
}while($true)
@{pid=$appPid;exe=$exe;package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$start;handleRetained=($handle -ne 0);exitCode=$code;terminated=$true;forced=$false;queryComplete=$complete;observationComplete=($queries[-1].startedSecondsAfterExit -ge 30);terminationUtc=$termination.ToString('o');lastQueryUtc=$lastQuery;collectionEndUtc=[DateTime]::UtcNow.ToString('o');queries=$queries;managedUnhandled=$unhandled;copyError=$copyError}|ConvertTo-Json -Depth 6|Set-Content "$out/exit-observation.json"
ConvertTo-Json -InputObject @($events.Values) -Depth 6|Set-Content "$out/events-safe.json"
$target.Dispose()
