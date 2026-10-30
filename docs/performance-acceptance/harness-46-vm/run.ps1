param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$Evidence=[IO.Path]::GetFullPath($Evidence)
$env:WINAPP_UI_WORKFLOW_ID='ai46vm-8b8f'
if($AppPid -eq 9608 -or (Get-Process -Id $AppPid).Path -ne "$Layout\Harness.exe"){throw 'Ownership mismatch'}
function UI([string[]]$Arguments){$out=& winapp ui @Arguments -a $AppPid --json; if($LASTEXITCODE -ne 0){throw ($out -join "`n")}; $parsed=($out -join "`n")|ConvertFrom-Json; if($parsed.error -or $parsed.timedOut){throw ($out -join "`n")}; $out}
function Capture([string]$Name){UI @('inspect') | Set-Content "$Evidence/$Name-uia.json";UI @('screenshot','--output',"$Evidence/$Name.png") | Set-Content "$Evidence/$Name-capture.json"}
try {
 UI @('wait-for','Status','--value','READY','-t','20000')
 Capture '00-ready'
 UI @('invoke','Run')
 foreach($name in 'article-streaming','article-final-copy','video-final','input-ready','input-streaming-stop'){
  UI @('wait-for','Status','--value',"CAPTURE $name",'-t','45000')
  if($name -eq 'article-final-copy'){UI @('invoke','ProductionCopy')}
  if($name -eq 'input-ready'){
   UI @('send-keys','fixture question','--target','InputBox','--via','send-input','--verbatim')
   UI @('send-keys','enter','--target','InputBox','--via','send-input')
  }
  Capture $name
  if($name -eq 'input-streaming-stop'){UI @('invoke','ProductionCancel');Capture 'stopped'}
  UI @('invoke','Continue')
  if($name -eq 'input-ready'){
   UI @('wait-for','Status','--value','STREAM-ACTIVE','-t','10000')
   UI @('send-keys','home','--target','ProductionModel','--via','send-input')
  }
 }
 UI @('wait-for','Status','--value','DONE','-t','45000')
 Capture 'done'
} finally {
 $package=Get-AppxPackage -Name BiliCopilot.Acceptance46VM8B8F
 if($package){$folder=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState";if(Test-Path "$folder/events.jsonl"){Copy-Item "$folder/events.jsonl" "$Evidence/events.jsonl"}}
 if(Get-Process -Id $AppPid -ErrorAction SilentlyContinue){try{UI @('invoke','Close') | Out-Null}catch{if((Get-Process -Id $AppPid).Path -eq "$Layout\Harness.exe"){Stop-Process -Id $AppPid}}}
 if($package -and $package.InstallLocation -eq $Layout){Remove-AppxPackage $package.PackageFullName}
 & winapp ui yield --json | Set-Content "$Evidence/yield.json"
 @{processAbsent=!(Get-Process -Id $AppPid -ErrorAction SilentlyContinue);packageAbsent=!(Get-AppxPackage -Name BiliCopilot.Acceptance46VM8B8F);pid=$AppPid;layout=$Layout} | ConvertTo-Json | Set-Content "$Evidence/cleanup.json"
}
& "$PSScriptRoot/verify.ps1" -Evidence $Evidence
exit $LASTEXITCODE
