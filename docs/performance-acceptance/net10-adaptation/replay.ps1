param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Run,[ValidateSet('v2','v3')][string]$Protocol='v3')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$out="$root/artifacts/net10-adaptation/$Run"
if(Test-Path $out){throw 'Fresh run required'}
New-Item -ItemType Directory $out | Out-Null
$frozen="$Fixture/frozen-output"
$inputDir="$out/input"; $layout=[IO.Path]::GetFullPath("$out/layout")
$identity="Mang.BiliCopilot.Runtime.N854.$Run"
$workflow="runtime-n854-$Run"
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
$appPid=0; $observer=$null; $observerIdentity=$null
try {
 $launch=& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $layout --detach --json
 $launch | Set-Content "$out/launch.json"
 if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 if(!$appPid -or $appPid -eq 9608 -or [IO.Path]::GetFullPath((Get-Process -Id $appPid).Path) -ne [IO.Path]::GetFullPath("$layout/BiliCopilot.UI.exe")){throw 'Not owned process'}
 if(Compare-Object $expected (Hashes $layout)){throw 'Two-way deployed DLL mismatch'}
 @{count=$expected.Count;missing=0;extra=0;mismatched=0} | ConvertTo-Json | Set-Content "$out/hash-gate.json"
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout){throw 'Registration mismatch'}
 $state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
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
 Copy-Item "$PSScriptRoot/$observerScript" "$out/observe.py"
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
 @{explicitExit=$true;pid=$appPid;protocol="external-os-$Protocol"} | ConvertTo-Json | Set-Content "$out/accepted.json"
} finally {
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
