param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Evidence,[ValidateSet('Normal','LastWindow')][string]$Phase='Normal',[string]$RunId='final')
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='acceptance55-a187'
$package=Get-AppxPackage -Name BiliCopilot.Acceptance55.A187
$app=Get-Process -Id $AppPid
if($AppPid -eq 9608 -or !$package -or !$app.Path.StartsWith($package.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Not owned'}
New-Item -ItemType Directory -Force $Evidence|Out-Null
$Evidence=(Resolve-Path $Evidence).Path
$local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
$deadline=(Get-Date).AddSeconds(12)
do {
 $identity=if(Test-Path "$local/identity.json"){Get-Content "$local/identity.json" -Raw|ConvertFrom-Json}else{$null}
 if($identity.pid -eq $AppPid){break}
 Start-Sleep -Milliseconds 80
}while((Get-Date)-lt $deadline)
if($identity.pid -ne $AppPid){throw 'No identity handshake for this launch'}
$hwnd=[string]$identity.hwnd
$results=[Collections.Generic.List[object]]::new()
$calls=[Collections.Generic.List[object]]::new()
$ownedChildren=[Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments){
 $raw=@(& winapp ui @Arguments --on local --json);$code=$LASTEXITCODE
 $calls.Add(@{args=$Arguments;exit=$code;output=($raw -join "`n")})
 if($code){throw "UI failed: $($Arguments -join ' '): $($raw -join ' ')"}
 $raw
}
function Act([string]$id){Ui @('invoke',$id,'-w',$hwnd)|Out-Null}
function Assert($condition,[string]$why){if(!$condition){throw $why}}
function Until([scriptblock]$condition,[string]$why){$end=(Get-Date).AddSeconds(12);do{if(& $condition){return};Start-Sleep -Milliseconds 80}while((Get-Date)-lt $end);throw "Timeout: $why"}
function Events {if(Test-Path "$script:caseRoot/events.jsonl"){ @(Get-Content "$script:caseRoot/events.jsonl"|ForEach-Object{$_|ConvertFrom-Json}) }else{@()}}
function Count([string]$kind){@((Events)|Where-Object kind -eq $kind).Count}
function Toggle([string]$id,[bool]$on){$action=if($on){'toggle-on'}else{'toggle-off'};Ui @('invoke',$id,'--action',$action,'-w',$hwnd)|Out-Null; $value=if($on){'On'}else{'Off'};Ui @('wait-for',$id,'--value',$value,'-t','3000','-w',$hwnd)|Out-Null}
function Capture([string]$name){Ui @('inspect','-d','12','-w',$hwnd)|Set-Content "$Evidence/$name-tree.json";Ui @('screenshot','-w',$hwnd,'--output',"$Evidence/$name.png")|Set-Content "$Evidence/$name-capture.json";Assert ((Get-Item "$Evidence/$name.png").Length -gt 0) 'Screenshot missing'}
function Prepare([string]$name,[string]$mode='ExitZero'){
 $ownedName="$RunId-$name"
 $script:caseRoot=Join-Path $local $ownedName
 Ui @('set-value','CaseName',$ownedName,'-w',$hwnd)|Out-Null;Act $mode;Act 'Prepare'
 Ui @('wait-for','Status','--value',"Prepared $ownedName",'-t','3000','-w',$hwnd)|Out-Null
 Act 'DownloadMenu';Ui @('wait-for','DownloadCover','-t','3000','-w',$hwnd)|Out-Null
 Capture "$name-menu"
 Act 'DownloadCover'
}
function Child {
 Until {Test-Path "$script:caseRoot/child.json"} 'owned child handshake'
 $info=Get-Content "$script:caseRoot/child.json" -Raw|ConvertFrom-Json
 Assert ($info.pid -ne 9608) 'Protected PID'
 $p=Get-Process -Id $info.pid
 $null=$p.Handle # retain an external process handle while it is alive, to obtain ExitCode later
 Assert ($p.Path -eq "$($package.InstallLocation)\Assets\Child\BBDown.exe") 'Unexpected child path'
 $script:ownedChildren.Add(@{process=$p;root=$script:caseRoot})
 return $p
}
function Release($child,[int]$expected){
 'release'|Set-Content "$script:caseRoot/release"
 Assert ($child.WaitForExit(12000)) 'Child did not exit'
 Assert ($child.ExitCode -eq $expected) "Wrong child exit code $($child.ExitCode)"
}
function Completed([int]$folders,[int]$logs){
 Until { (Count 'process-disposed') -eq 1 -and (Count 'folder-adapter') -eq $folders -and (Count 'production-log') -eq $logs } 'production completion'
 $disposed=(Events)|Where-Object kind -eq 'process-disposed'
 Assert ($disposed.data.handleCaptured -and $disposed.data.handleClosed) 'Runner did not close captured handle'
 Assert ((Count 'tip') -eq 0) 'Unexpected success tip'
}
function Case([string]$name,[scriptblock]$body){try{& $body;$results.Add(@{name=$name;status='PASS'})}catch{$results.Add(@{name=$name;status='FAIL';detail="$_"});throw}}
try {
 # Expansion is idempotent and all selectors refer to the owned main HWND.
 $tree=Ui @('inspect','-d','12','-w',$hwnd)|ConvertFrom-Json
 function Nodes($items){foreach($n in $items){$n;Nodes $n.children}}
 $expander=@(Nodes $tree.windows.elements|Where-Object {$_.type -eq 'Button' -and $_.name -eq 'DownloadSetting'})
 Assert ($expander.Count -eq 1) 'Settings expander selector missing'
 Ui @('invoke',$expander[0].selector,'--action','expand','-w',$hwnd)|Out-Null;Act 'TagControls'
 Capture "$Phase-ready"
 if($Phase -eq 'Normal'){
  Case 'SuccessOpen' {
   Toggle 'OpenFolderAfterDownload' $true;Prepare 'SuccessOpen';$child=Child
   Assert (!( $child.HasExited) -and (Count 'folder-adapter') -eq 0 -and (Count 'process-disposed') -eq 0) 'Premature completion'
   Capture 'SuccessOpen-waiting';Release $child 0;Completed 1 0
   $e=Events;Assert (($e.kind -join ',') -match 'process-disposed,folder-adapter') 'Folder callback must follow disposal'
  }
  Case 'SuccessDisabled' {
   Toggle 'OpenFolderAfterDownload' $false;Prepare 'SuccessDisabled';$child=Child
   Assert (!$child.HasExited -and (Count 'folder-adapter') -eq 0) 'Premature completion';Release $child 0;Completed 0 0
  }
  Case 'NonzeroExit' {
   Toggle 'OpenFolderAfterDownload' $true;Prepare 'NonzeroExit' 'ExitSeven';$child=Child;Release $child 7;Completed 0 1
   $log=(Events)|Where-Object kind -eq 'production-log';Assert ($log.data.level -eq 'Warning' -and $log.data.message -eq 'Download process exited with code 7.') 'Wrong failure log'
  }
  Case 'StartFailure' {
   Prepare 'StartFailure' 'MissingExe';Until {(Count 'production-log') -eq 1} 'start error'
   $log=(Events)|Where-Object kind -eq 'production-log'
   Assert ($log.data.level -eq 'Error' -and $log.data.message -eq 'Download process failed (Win32Exception).') 'Wrong startup error log'
   Assert ((Count 'folder-adapter') -eq 0 -and (Count 'tip') -eq 0 -and (Count 'process-disposed') -eq 1 -and !(Test-Path "$script:caseRoot/child.json")) 'Startup failure leaked success/child'
  }
  Case 'CopyOnlyNoProcess' {
   Toggle 'UseExternalBBDown' $true;Toggle 'OnlyCopyCommandWhenDownload' $true;Prepare 'CopyOnlyNoProcess';Act 'VerifyClipboard'
   Ui @('wait-for','Status','--value','Owned clipboard matches','-t','3000','-w',$hwnd)|Out-Null
   Capture 'CopyOnlyNoProcess-copied'
   Assert ((Count 'process-created') -eq 0 -and !(Test-Path "$script:caseRoot/child.json") -and (Count 'folder-adapter') -eq 0 -and (Count 'tip') -eq 1) 'Copy launched a process or wrong notification'
   $clip=(Events)|Where-Object kind -eq 'clipboard-owned';Assert $clip.data.matches 'Synthetic clipboard mismatch'
   Act 'ClearOwnedClipboard';Ui @('wait-for','Status','--value','Owned clipboard cleared','-t','3000','-w',$hwnd)|Out-Null
  }
  Case 'CloseWindowHostAlive' {
   Toggle 'OnlyCopyCommandWhenDownload' $false
   # Actual external BBDown name resolution through a process-local PATH prefix.
   Prepare 'CloseWindowHostAlive';$child=Child;Act 'KeepHost';Act 'CloseMain'
   Until {(Count 'main-closed') -eq 1} 'main closed'
   Assert (!$app.HasExited -and !$child.HasExited -and (Count 'folder-adapter') -eq 0) 'Host/child unexpectedly exited'
   @{schemaVersion=1;hostAlive=$true;childAlive=$true;childPid=$child.Id;hostPid=$AppPid;mainClosed=$true}|ConvertTo-Json|Set-Content "$Evidence/close-window-external-observation.json"
   Release $child 0;Completed 1 0
   $windows=Ui @('list-windows','-a',"$AppPid")|ConvertFrom-Json
   $keep=@($windows|Where-Object title -Like '*keeper*');Assert ($keep.Count -eq 1) 'Keeper missing'
   Ui @('inspect','-w',"$($keep[0].hwnd)")|Set-Content "$Evidence/keeper-tree.json"
   Ui @('screenshot','-w',"$($keep[0].hwnd)",'--output',"$Evidence/keeper.png")|Set-Content "$Evidence/keeper-capture.json"
   Ui @('invoke','Close','-w',"$($keep[0].hwnd)")|Out-Null
   Assert ($app.WaitForExit(10000)) 'Keeper close did not exit'
  }
 }else{
  Case 'CloseLastWindowChildSurvives' {
   Prepare 'CloseLastWindowChildSurvives';$child=Child
   Assert ((Count 'folder-adapter') -eq 0) 'Premature folder'
   Act 'CloseMain';Assert ($app.WaitForExit(10000)) 'Host did not exit normally'
   Assert (!$child.HasExited) 'Child killed by window/application exit'
   @{schemaVersion=1;hostExited=$app.HasExited;hostExitCode=$app.ExitCode;childAlive=(!$child.HasExited);childPid=$child.Id;hostPid=$AppPid;folderCallbacks=(Count 'folder-adapter');disposals=(Count 'process-disposed')}|ConvertTo-Json|Set-Content "$Evidence/last-window-external-observation.json"
   Release $child 0
   Assert ((Count 'folder-adapter') -eq 0 -and (Count 'process-disposed') -eq 0) 'Exited host cannot execute callback/disposal'
  }
 }
}catch{
 "$_"|Set-Content "$Evidence/failure.txt"
 throw
}finally{
 foreach($entry in $ownedChildren){if(!$entry.process.HasExited){'cleanup-release'|Set-Content "$($entry.root)/release";if(!$entry.process.WaitForExit(12000)){Write-Warning 'Owned child did not stop after release'}}}
 $results.ToArray()|ConvertTo-Json -Depth 8|Set-Content "$Evidence/results.json"
 $calls.ToArray()|ConvertTo-Json -Depth 8|Set-Content "$Evidence/uia-calls.json"
 try {New-Item -ItemType Directory -Force "$Evidence/localstate"|Out-Null;Copy-Item "$local/*" "$Evidence/localstate/" -Recurse -Force}finally{& winapp ui yield --on local --json|Set-Content "$Evidence/yield.json"}
}
Assert (@($results|Where-Object status -eq 'FAIL').Count -eq 0) 'Some tests failed'
$results|Format-Table
