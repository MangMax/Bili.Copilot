param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Run,[ValidateSet('v2','v3')][string]$Protocol='v3')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$out="$root/artifacts/runtime-final-baseline/$Run"
if(Test-Path $out){throw 'Fresh run required'}
New-Item -ItemType Directory $out | Out-Null
$frozen="$Fixture/frozen-output"
$inputDir="$out/input"; $layout=[IO.Path]::GetFullPath("$out/layout")
$identity="Mang.BiliCopilot.Runtime.Final4F1.$Run"
$workflow="runtime-final4f1-$Run"
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
@{run=$Run;fixture=$Fixture;identity=$identity;source=(Get-Content "$Fixture/source-provenance.txt" -Raw);runtime=(Get-Content "$frozen/BiliCopilot.UI.runtimeconfig.json" -Raw | ConvertFrom-Json);dlls=$expected;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 8 | Set-Content "$out/provenance.json"
$appPid=0; $observer=$null; $observerIdentity=$null; $target=$null; $completed=$false
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1 -ErrorAction Stop).RecordId
function Background($name) {
 @{utc=[DateTime]::UtcNow.ToString('o');rows=@(Get-Process | ForEach-Object { @{pid=$_.Id;name=$_.ProcessName;cpuSeconds=$_.CPU;startedUtc=$(try{$_.StartTime.ToUniversalTime().ToString('o')}catch{$null})} })} | ConvertTo-Json -Depth 5 | Set-Content "$out/background-$name.json"
}
Background 'before'

try {
 $launch=& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $layout --detach --json
 $launch | Set-Content "$out/launch.json"
 if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 if(!$appPid -or $appPid -eq 9608 -or [IO.Path]::GetFullPath((Get-Process -Id $appPid).Path) -ne [IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")){throw 'Not owned process'}
 if(Compare-Object $expected (Hashes $layout)){throw 'Two-way deployed DLL mismatch'}
 @{count=$expected.Count;missing=0;extra=0;mismatched=0} | ConvertTo-Json | Set-Content "$out/hash-gate.json"
 $target=Get-Process -Id $appPid
 $handle=$target.Handle; $creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc(); $start=$target.StartTime.ToUniversalTime().ToString('o')
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout){throw 'Registration mismatch'}
 $state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
 @{pid=$appPid;exe="$layout/BiliCopilot.UI.exe";creationFileTime=$creation;startUtc=$start;handleRetained=($handle -ne 0);package=$package.PackageFullName} | ConvertTo-Json | Set-Content "$out/ready.json"
 foreach($name in 'run-ui.ps1','run-main.ps1'){
  $runner=(Get-Content "$Fixture/$name" -Raw).Replace($oldIdentity,$identity)
  $runner=$runner -replace "(?m)^\`$env:WINAPP_UI_WORKFLOW_ID=.*$",("`$env:WINAPP_UI_WORKFLOW_ID='$workflow'")
  Set-Content "$out/$name" $runner
 }
 Copy-Item "$Fixture/source-provenance.txt" "$out/source-provenance.txt"
 & "$out/run-ui.ps1" -AppPid $appPid -Layout $layout -Evidence "$out/player" *> "$out/player.log"
 if($LASTEXITCODE -ne 0){throw 'Player failed'}
 @((Get-Process -Id $appPid).Modules | ForEach-Object { @{name=$_.ModuleName;path=$_.FileName;sha256=(Get-FileHash $_.FileName).Hash;version=$_.FileVersionInfo.ProductVersion} }) | ConvertTo-Json -Depth 4 | Set-Content "$out/modules.json"
 $python=(Get-Command python).Source
 $observerScript=if($Protocol -eq 'v3'){'observe-fast.py'}else{'observe.py'}
 Copy-Item "$PSScriptRoot/../net10-adaptation/$observerScript" "$out/observe.py"
 Get-FileHash "$out/observe.py" | ConvertTo-Json | Set-Content "$out/observer-source-hash.json"
 $arguments=@("`"$out/observe.py`"",$appPid,"`"$layout/BiliCopilot.UI.exe`"","`"$state`"","`"$out`"")
 $observer=Start-Process $python -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/observer.stdout" -RedirectStandardError "$out/observer.stderr"
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 while(!(Test-Path "$out/observer-ready.json") -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(!(Test-Path "$out/observer-ready.json")){throw 'Observer failed to start'}
 $observerIdentity=Get-Content "$out/observer-ready.json" -Raw | ConvertFrom-Json
 if([IO.Path]::GetFullPath((Get-Process -Id $observerIdentity.observerPid).Path) -ne [IO.Path]::GetFullPath($observerIdentity.observerExe)){throw 'Observer identity mismatch'}
 & "$out/run-main.ps1" -AppPid $appPid -Layout $layout -Evidence "$out/main" *> "$out/main.log"
 if($LASTEXITCODE -ne 0){throw 'Main failed'}
 if(!$observer.WaitForExit(5000)){throw 'Observer did not finish'}
 $observer.Refresh(); if($observer.ExitCode -ne 0){throw 'Observer failed'}
 if(Get-Process -Id $appPid -ErrorAction SilentlyContinue){throw 'Explicit exit failed'}
 $completed=$true
} catch {
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

 if($state -and (Test-Path $state)){
  foreach($file in Get-ChildItem -LiteralPath $state -Filter 'observe-*.json'){Copy-Item $file.FullName $out}
 }
 if($observer -and !$observer.HasExited){Stop-Process -Id $observer.Id}
 if($observerIdentity){
  $child=Get-Process -Id $observerIdentity.observerPid -ErrorAction SilentlyContinue
  if($child -and [IO.Path]::GetFullPath($child.Path) -eq [IO.Path]::GetFullPath($observerIdentity.observerExe)){Stop-Process -Id $child.Id}
 }
 if($appPid -gt 0 -and $appPid -ne 9608){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")){Stop-Process -Id $appPid}}
 $package=Get-AppxPackage -Name $identity
 if($package -and $package.InstallLocation -eq $layout){Remove-AppxPackage -Package $package.PackageFullName}
 & winapp ui yield --on local --json | Set-Content "$out/yield.json"
 @{pid=$appPid;processAbsent=($appPid -eq 0 -or !(Get-Process -Id $appPid -ErrorAction SilentlyContinue));identity=$identity;registrationAbsent=!(Get-AppxPackage -Name $identity);observerLauncherPid=if($observer){$observer.Id}else{0};observerIdentity=$observerIdentity;observerAbsent=(!$observerIdentity -or !(Get-Process -Id $observerIdentity.observerPid -ErrorAction SilentlyContinue));utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content "$out/cleanup.json"
}

& python -B "$PSScriptRoot/gate.py" $out
if($LASTEXITCODE -ne 0 -or !$completed){throw 'Run not accepted; stop sequence and retain evidence'}
@{explicitExit=$true;pid=$appPid;protocol="external-os-$Protocol"} | ConvertTo-Json | Set-Content "$out/accepted.json"
