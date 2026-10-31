param([string]$Run='runtime')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path (Get-Location) 'artifacts/latest-seek/full'))
$identity=Get-Content (Join-Path $root "$Run/identity.json") -Raw | ConvertFrom-Json
if(!$identity.path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Foreign identity path'}
$process=Get-Process -Id $identity.pid -ErrorAction SilentlyContinue
if($process -and $process.StartTime.ToUniversalTime() -eq [DateTime]$identity.creation){throw 'Owned process is still running; close normally before cleanup'}
$exit=Get-Content (Join-Path $root "$Run/retainedexit.json") -Raw | ConvertFrom-Json
$wer=Get-Content (Join-Path $root "$Run/wer.json") -Raw | ConvertFrom-Json
if(!$exit.retained -or $exit.path -ne $identity.path -or $wer.secondsAfterExit -lt 31){throw 'Retained exit / WER evidence incomplete'}
$pkg=Get-AppxPackage -Name 'Bili.LatestA1de'
if($pkg){
 if(!$pkg.InstallLocation.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned package points outside this workspace'}
 Remove-AppxPackage -Package $pkg.PackageFullName
}
$env:WINAPP_UI_WORKFLOW_ID='latest-a1de'
& winapp ui yield --on local --json | Set-Content (Join-Path $root "$Run/cleanup-yield.json")
if($LASTEXITCODE){throw 'yield failed'}
@{identity='Bili.LatestA1de';remaining=@(Get-AppxPackage -Name 'Bili.LatestA1de').Count;forced=$false}|ConvertTo-Json|Set-Content (Join-Path $root "$Run/cleanup.json")
