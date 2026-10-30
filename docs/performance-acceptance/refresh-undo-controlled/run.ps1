param([Parameter(Mandatory)][string]$Run)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$fixture="$root/artifacts/refresh-undo/fixture"
$out=[IO.Path]::GetFullPath("$root/artifacts/refresh-undo/$Run")
if(Test-Path $out){throw 'Fresh run directory required'}
New-Item -ItemType Directory $out | Out-Null
$identity="Mang.BiliCopilot.UndoControlled.T283.$Run"
if(Get-AppxPackage -Name $identity){throw 'Existing registration prohibited'}
$env:WINAPP_UI_WORKFLOW_ID="undo-t283-$Run"
$layout=[IO.Path]::GetFullPath("$out/layout");$inputDir=[IO.Path]::GetFullPath("$out/input")
Copy-Item "$fixture/frozen-output" $inputDir -Recurse
[xml]$manifest=Get-Content "$inputDir/AppxManifest.xml"
$manifest.Package.Identity.Name=$identity;$manifest.Save("$inputDir/AppxManifest.xml")
$target=$null;$appPid=0;$state=$null;$failure=$null
$bookmark=(Get-WinEvent -LogName Application -MaxEvents 1).RecordId
function UI([string[]]$a){
 $text=@(& winapp ui @a --on local -a $appPid --json);$code=$LASTEXITCODE
 @{utc=[DateTime]::UtcNow.ToString('o');arguments=$a;exit=$code;output=$text} | ConvertTo-Json -Depth 6 -Compress | Add-Content "$out/actions.jsonl"
 if($code -ne 0){throw "UI failed: $a"};$text
}
try {
 $launch=@(& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $layout --detach --json)
 $launch | Set-Content "$out/launch.json";if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 $target=Get-Process -Id $appPid
 if($appPid -eq 9608 -or [IO.Path]::GetFullPath($target.Path) -ne [IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")){throw 'Owned process mismatch'}
 $retainedHandle=$target.Handle;$creation=$target.StartTime.ToUniversalTime().ToFileTimeUtc();$startUtc=$target.StartTime.ToUniversalTime().ToString('o')
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout){throw 'Owned package mismatch'}
 $state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
 @{pid=$appPid;creation=$creation;layout=$layout;identity=$identity;state=$state;startUtc=$startUtc} | ConvertTo-Json | Set-Content "$out/ready.json"
 $hashes=@(foreach($f in Get-ChildItem "$fixture/frozen-output" -Recurse -File){
  $rel=[IO.Path]::GetRelativePath("$fixture/frozen-output",$f.FullName)
  if($rel -eq 'AppxManifest.xml'){continue}
  $a=(Get-FileHash $f.FullName).Hash;$b=(Get-FileHash (Join-Path $layout $rel)).Hash
  if($a -ne $b){throw "Payload mismatch $rel"};@{path=$rel;sha256=$b}
 })
 $hashes | ConvertTo-Json | Set-Content "$out/runtime-payload.json"
 UI @('wait-for','RunControlled','-t','120000') | Out-Null
 UI @('invoke','RunControlled') | Out-Null
 UI @('wait-for','ControlledStatus','--value','CAPTURE before-visible-refresh','-t','240000') | Out-Null
 UI @('screenshot','--output',"$out/before-refresh.png") | Out-Null
 UI @('invoke','ContinueEvidence') | Out-Null
 UI @('click','RefreshRecommendButton') | Out-Null
 UI @('wait-for','ControlledStatus','--value','CAPTURE after-visible-refresh','-t','20000') | Out-Null
 UI @('screenshot','--output',"$out/after-refresh.png") | Out-Null
 UI @('invoke','ContinueEvidence') | Out-Null
 UI @('click','UndoRefreshButton') | Out-Null
 UI @('wait-for','ControlledStatus','--value','CAPTURE after-visible-undo','-t','20000') | Out-Null
 UI @('screenshot','--output',"$out/after-undo.png") | Out-Null
 UI @('invoke','ContinueEvidence') | Out-Null
 UI @('wait-for','ControlledStatus','--value','DONE','--contains','-t','30000') | Out-Null
 @($target.Modules | Where-Object {$_.FileName.StartsWith($layout+'\')} | ForEach-Object {@{path=$_.FileName;sha256=(Get-FileHash $_.FileName).Hash}}) | ConvertTo-Json | Set-Content "$out/loaded-modules.json"
 Copy-Item "$state/controlled-results.json","$state/controlled.jsonl" $out
 $results=Get-Content "$out/controlled-results.json" -Raw | ConvertFrom-Json
 if(@($results | Where-Object status -ne 'PASS').Count){throw 'Behavior cases failed'}
} catch { $failure="$_";@{failure=$failure;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$out/failure.json" }
finally {
 if($target){
  if(!$target.HasExited){try{UI @('invoke','CloseOwned') | Out-Null}catch{$failure="Close failed: $_"}}
  $terminated=$target.WaitForExit(15000);$exitCode=$null
  if($terminated){$exitCode=[uint32]([int64]$target.ExitCode -band 0xffffffffL)}
  $terminationUtc=[DateTime]::UtcNow.ToString('o');$watch=[Diagnostics.Stopwatch]::StartNew()
  $queries=@();$xmls=@{};$safe=@();$queryComplete=$true
  # Query first, then check the monotonic deadline. The final query starts AFTER 30s.
  do {
   $queryStart=$watch.Elapsed.TotalSeconds;$ok=$true
   try {
    $records=@(Get-WinEvent -LogName Application -FilterXPath "*[System[(EventID=1000 or EventID=1001) and EventRecordID > $bookmark]]" -ErrorAction Stop)
    foreach($event in $records){
     [xml]$xml=$event.ToXml();$data=@{};foreach($entry in $xml.Event.EventData.Data){$data[[string]$entry.Name]=[string]$entry.'#text'}
     if($data['AppPath'] -eq "$layout\BiliCopilot.UI.exe" -or $data['P1'] -eq $package.PackageFullName){
      $xmls[[string]$event.RecordId]=$event.ToXml()
     }
    }
   }catch{if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){$ok=$false;$queryComplete=$false}}
   $lastQuery=[DateTime]::UtcNow.ToString('o');$queries+=@{utc=$lastQuery;startedSecondsAfterExit=$queryStart;ok=$ok}
   if($queryStart -ge 30){break};Start-Sleep -Milliseconds 500
  }while($true)
  if($state -and (Test-Path $state)){Get-ChildItem $state -Filter 'controlled*' | Copy-Item -Destination $out}
  $unhandled=if(Test-Path "$out/controlled.jsonl"){@(Get-Content "$out/controlled.jsonl" | ConvertFrom-Json | Where-Object kind -eq 'unhandled').Count}else{0}
  $observation=@{pid=$appPid;exe="$layout\BiliCopilot.UI.exe";package=$package.PackageFullName;creationFileTime=$creation;bookmark=$bookmark;startUtc=$startUtc;handleRetained=$true;exitCode=$exitCode;terminated=$terminated;forced=$false;queryComplete=$queryComplete;observationComplete=($queries[-1].startedSecondsAfterExit -ge 30);terminationUtc=$terminationUtc;lastQueryUtc=$lastQuery;collectionEndUtc=[DateTime]::UtcNow.ToString('o');queries=$queries;managedUnhandled=$unhandled}
  $observation | ConvertTo-Json -Depth 6 | Set-Content "$out/exit-observation.json"
  ConvertTo-Json -InputObject @($xmls.Values) | Set-Content "$out/event-xml.json"
  python "$root/docs/performance-acceptance/net11-exit-reproduction/exit_gate.py" $out
  if($LASTEXITCODE -ne 0){$failure='Exit gate BLOCKED'}
  if(!$terminated){$failure='Owned process did not exit; forced cleanup';$observation.forced=$true;$observation | ConvertTo-Json -Depth 6 | Set-Content "$out/exit-observation.json";Stop-Process -Id $appPid;python "$root/docs/performance-acceptance/net11-exit-reproduction/exit_gate.py" $out}
  $target.Dispose()
 }
 $package=Get-AppxPackage -Name $identity
 if($package -and $package.InstallLocation -eq $layout){Remove-AppxPackage -Package $package.PackageFullName}
 & winapp ui yield --on local --json | Set-Content "$out/yield.json"
 @{pid=$appPid;processAbsent=!(Get-Process -Id $appPid -ErrorAction SilentlyContinue);identity=$identity;registrationAbsent=!(Get-AppxPackage -Name $identity);utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$out/cleanup.json"
}
if($failure){throw $failure}
