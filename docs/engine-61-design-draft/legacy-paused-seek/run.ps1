param([string]$RunName='normal-first',[switch]$Protocol)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$evidence=Join-Path $root "evidence/$RunName"
if(Test-Path $evidence){throw 'fresh evidence required'}
New-Item -ItemType Directory $evidence | Out-Null
$identity='BiliCopilot.LegacySeek1085f94e'
if($Protocol){'protocol' | Set-Content "$root/protocol-only.txt"}elseif(Test-Path "$root/protocol-only.txt"){Remove-Item -LiteralPath "$root/protocol-only.txt"}
if(Get-AppxPackage -Name $identity){throw 'identity already registered'}
$oldWorkflow=$env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID='legacy-seek-1085f94e'
$records=[Collections.Generic.List[object]]::new()
function Run([string[]]$Arguments){
 $output=@(& winapp @Arguments);$code=$LASTEXITCODE
 $records.Add(@{utc=[DateTime]::UtcNow.ToString('o');args=$Arguments;exit=$code;output=$output -join "`n"})
 $records | ConvertTo-Json -Depth 8 | Set-Content "$evidence/commands.json"
 if($code){throw "winapp exit $code"}; $output -join "`n"
}
$target=$null;$creation=$null;$imagePath=$null;$failed=$false;$started=[DateTime]::UtcNow
try {
 Copy-Item "$root/host/Program.cs","$root/run.ps1" $evidence
 $launch=Run @('run',"$root/host/Spike.csproj",'-c','Release','-p','Platform=x64','--no-build','--no-restore','--on','local','--output-appx-directory',"$root/layout",'--detach','--json') | ConvertFrom-Json
 $launch | ConvertTo-Json -Depth 8 | Set-Content "$evidence/launch.json"
 $ownedPid=[int]$launch.ProcessId
 if($ownedPid -le 0){throw 'invalid PID'}
 $target=Get-Process -Id $ownedPid; $retained=$target.Handle
 @{pid=$ownedPid;handle=$retained.ToString();utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$evidence/handle-retained.json"
 $creation=$target.StartTime.ToUniversalTime();$imagePath=$target.MainModule.FileName
 if(!$imagePath.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'outside owned path'}
 @{pid=$ownedPid;creation=$creation.ToString('o');path=$imagePath;retained=$true} | ConvertTo-Json | Set-Content "$evidence/identity.json"
 Get-ChildItem "$root/layout" -Recurse -File | Get-FileHash | ConvertTo-Json | Set-Content "$evidence/layout-hashes.json"
 Run @('ui','wait-for','Start','--on','local','-a',"$ownedPid",'-t','15000','--json') | Out-Null
 Run @('ui','invoke','Start','--on','local','-a',"$ownedPid",'--json') | Out-Null
 $phases=@('BACKWARD','REPEATED')
 foreach($phase in $phases){
  Run @('ui','wait-for','Status','--value',$phase,'--on','local','-a',"$ownedPid",'-t','20000','--json') | Out-Null
  Run @('ui','screenshot','--on','local','-a',"$ownedPid",'--output',"$evidence/$phase.png",'--json') | Out-Null
  $hash=(Get-FileHash "$evidence/$phase.png").Hash
  Write-Output "REVIEW $evidence/$phase.png $hash"
  Run @('ui','invoke','Continue','--on','local','-a',"$ownedPid",'--json') | Out-Null
 }
 Run @('ui','wait-for','Status','--value','DONE','--on','local','-a',"$ownedPid",'-t','30000','--json') | Out-Null
}catch{$failed=$true;$_ | Out-String | Set-Content "$evidence/STOP.txt";Write-Output $_}
finally {
 $forced=$false
 if($target){
  if(!$target.HasExited){$target.CloseMainWindow() | Out-Null;if(!$target.WaitForExit(5000)){$failed=$true;$forced=$true;@{pid=$ownedPid;path=$imagePath;state='UNKNOWN_EXIT_NOT_PASS';reason='close timeout is not safe retirement'} | ConvertTo-Json | Set-Content "$evidence/unknown-exit.json"; Run @('ui','yield','--on','local','--json') | Set-Content "$evidence/yield.txt"; throw 'UNKNOWN_EXIT_NOT_PASS: retained process; no forced kill or package removal'}}
  $target.Refresh();$exit=$target.ExitTime.ToUniversalTime()
  @{pid=$ownedPid;path=$imagePath;creation=$creation;exit=$exit;exitCode=$target.ExitCode;forced=$forced;remaining=!$target.HasExited;retainedHandle=$true} | ConvertTo-Json | Set-Content "$evidence/retainedexit.json"
  if($target.ExitCode -ne 0){$failed=$true}
 }
 foreach($package in @(Get-AppxPackage -Name $identity)){
  if(!$package.InstallLocation.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'foreign package'}
  $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
  if(Test-Path "$local/events.jsonl"){Copy-Item "$local/events.jsonl" $evidence; if(Select-String -Path "$local/events.jsonl" -SimpleMatch '"passed":false'){$failed=$true}};if(Test-Path "$local/mpv.log"){Copy-Item "$local/mpv.log" $evidence}
  $package | Select-Object Name,PackageFullName,InstallLocation | ConvertTo-Json | Set-Content "$evidence/package.json"
  if(!$target -or !$target.HasExited){throw 'UNKNOWN_EXIT_NOT_PASS'}
  Remove-AppxPackage -Package $package.PackageFullName
 }
 try {Run @('ui','yield','--on','local','--json') | Set-Content "$evidence/yield.txt"} finally {$env:WINAPP_UI_WORKFLOW_ID=$oldWorkflow}
 @{packagesRemaining=@(Get-AppxPackage -Name $identity).Count;processRemaining=if($target){!$target.HasExited}else{$null}} | ConvertTo-Json | Set-Content "$evidence/cleanup.json"
 if($target){
  $delay=($exit.AddSeconds(32)-[DateTime]::UtcNow).TotalMilliseconds;if($delay -gt 0){Start-Sleep -Milliseconds ([int]$delay)}
  $query=[DateTime]::UtcNow;Get-WinEvent -ListLog Application -ErrorAction Stop | Out-Null
  $events=@();try{$events=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started.ToLocalTime();Id=1000,1001} -ErrorAction Stop)}catch{if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){throw}}
  $matches=@($events | Where-Object {([xml]$_.ToXml()).Event.EventData.Data | Where-Object { $_.InnerText -eq $imagePath -or $_.InnerText -eq [string]$ownedPid -or $_.InnerText -eq ('0x{0:x}' -f $ownedPid) }} | ForEach-Object {@{id=$_.Id;xml=$_.ToXml()}})
  @{applicationLogReadable=$true;query=$query;secondsAfterExit=($query-$exit).TotalSeconds;matches=$matches} | ConvertTo-Json -Depth 6 | Set-Content "$evidence/wer.json"
  if($matches.Count){$failed=$true}
 }
}
$cases=@()
if(Test-Path "$evidence/events.jsonl"){
 $events=@(Get-Content "$evidence/events.jsonl" | ForEach-Object {$_ | ConvertFrom-Json})
 $cases=@($events | Where-Object {$null -ne $_.data.passed})
 if($cases.Count -ne 12 -or @($cases.name | Select-Object -Unique).Count -ne 12 -or !($events.name -contains 'SuiteCompleted')){$failed=$true}
}else{$failed=$true}
$passed=@($cases | Where-Object {$_.data.passed}).Count
$testFailed=@($cases | Where-Object {!$_.data.passed}).Count
@{passed=$passed;failed=$testFailed;skipped=0;executionFailed=$failed;cases=$cases} | ConvertTo-Json -Depth 10 | Set-Content "$evidence/results.json"
Write-Output "Passed: $passed, Failed: $testFailed, Skipped: 0; executionFailed=$failed"
if($failed){exit 1}
