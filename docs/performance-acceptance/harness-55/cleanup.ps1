param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='acceptance55-a187'
$package=Get-AppxPackage -Name BiliCopilot.Acceptance55.A187
try {
 if($package){
  $local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
  $ids=@(45116,55640,58912,59160)
  foreach($file in Get-ChildItem $local -Filter child.json -Recurse){$ids+=((Get-Content $file.FullName -Raw|ConvertFrom-Json).pid)}
  $alive=@(foreach($id in ($ids|Sort-Object -Unique)){
   if($id -eq 9608){throw 'Protected PID'}
   $p=Get-Process -Id $id -ErrorAction SilentlyContinue
   if($p -and $p.Path.StartsWith($package.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase)){$id}
  })
  if($alive.Count){throw "Owned processes still alive: $alive; finish via UI/gate before package removal"}
  New-Item -ItemType Directory -Force "$Evidence/final-localstate"|Out-Null
  Copy-Item "$local/*" "$Evidence/final-localstate/" -Recurse -Force
  Remove-AppxPackage $package.PackageFullName
  @{schemaVersion=1;ownedPids=$ids;ownedProcessesAlive=$alive;ownedPackageGone=($null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance55.A187));noKillUsed=$true;utc=[DateTimeOffset]::UtcNow}|ConvertTo-Json -Depth 5|Set-Content "$Evidence/cleanup.json"
 }
}finally{& winapp ui yield --on local --json|Set-Content "$Evidence/cleanup-yield.json";if($LASTEXITCODE){throw 'Yield failed'}}
