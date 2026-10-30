param([ValidateSet('base','up')][string]$Arm,[ValidateSet('profile','reference')][string]$Mode,[Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Run)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$data="$root/artifacts/dependency-cpu-profile"
$out="$data/$Run"
if(Test-Path $out){throw 'Fresh run required'}
New-Item -ItemType Directory $out | Out-Null
$fixture=if($Arm -eq 'base'){"$env:TEMP/bili-dep-d48-base-player"}else{[IO.Path]::GetFullPath("$root/../t3-340954c4/artifacts/dependency-performance-diagnosis/player-a")}
$frozen="$fixture/frozen-output"
$inputDir=[IO.Path]::GetFullPath("$out/input"); $layout=[IO.Path]::GetFullPath("$out/layout")
$identity="Mang.BiliCopilot.Cpu.D3d.$Run"
if(Get-AppxPackage -Name $identity){throw 'Identity already registered'}
$env:WINAPP_UI_WORKFLOW_ID="cpu-$Run"
function Hashes($dir){ @(Get-ChildItem $dir -Recurse -Filter '*.dll' | ForEach-Object { @{path=[IO.Path]::GetRelativePath($dir,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash} }) }
$original=Hashes $frozen
if($Arm -eq 'base'){
 $ledger=Get-Content "$root/../t3-d48c749d/artifacts/dependency-upgrade/base2complete-closure.json" -Raw | ConvertFrom-Json
 $expected=@($ledger | ForEach-Object { "$($_.path.ToLowerInvariant())=$($_.buildSha256)" })
}else{
 $ledger=Get-Content "$fixture/frozen-all-files.json" -Raw | ConvertFrom-Json
 $expected=@($ledger | Where-Object path -Like '*.dll' | ForEach-Object { "$($_.path.ToLowerInvariant())=$($_.sha256)" })
}
$actual=@($original | ForEach-Object { "$($_.path.ToLowerInvariant())=$($_.sha256)" })
if(Compare-Object $expected $actual){throw 'Frozen source hash gate failed'}
if(Get-ChildItem $frozen -Recurse -Filter '*.appxrecipe'){throw 'Recipe prohibited'}
New-Item -ItemType Directory $inputDir | Out-Null
Copy-Item "$frozen/*" $inputDir -Recurse
[xml]$manifest=Get-Content "$inputDir/AppxManifest.xml"
$manifest.Package.Identity.Name=$identity
$manifest.Save("$inputDir/AppxManifest.xml")
Copy-Item "$fixture/source-provenance.txt" "$inputDir/source-provenance.txt"
# Explicit identity edit only; no project evaluation or runtimeconfig edits.
@{arm=$Arm;mode=$Mode;run=$Run;fixture=$fixture;identity=$identity;utc=[DateTime]::UtcNow.ToString('o');source=(Get-Content "$fixture/source-provenance.txt" -Raw).Trim();runtime=(Get-Content "$frozen/BiliCopilot.UI.runtimeconfig.json" -Raw | ConvertFrom-Json);runtimeHash=(Get-FileHash "$frozen/BiliCopilot.UI.runtimeconfig.json").Hash;originalDlls=$original;power=(powercfg /getactivescheme)} | ConvertTo-Json -Depth 8 | Set-Content "$out/provenance.json"
$appPid=0; $collector=$null
try {
 $launch=& winapp run $inputDir --manifest "$inputDir/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $layout --detach --json
 $launch | Set-Content "$out/launch.json"
 if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 if(!$appPid -or $appPid -eq 9608 -or (Get-Process -Id $appPid).Path -ne "$layout\BiliCopilot.UI.exe"){throw 'Not owned process'}
 $deployed=Hashes $layout
 if(Compare-Object $actual @($deployed | ForEach-Object { "$($_.path.ToLowerInvariant())=$($_.sha256)" })){throw 'Two-way deployed DLL hash gate failed'}
 @{count=$deployed.Count;missing=0;extra=0;mismatched=0;runtimeMatch=((Get-FileHash "$layout/BiliCopilot.UI.runtimeconfig.json").Hash -eq (Get-FileHash "$frozen/BiliCopilot.UI.runtimeconfig.json").Hash)} | ConvertTo-Json | Set-Content "$out/hash-gate.json"
 $package=Get-AppxPackage -Name $identity
 if($package.InstallLocation -ne $layout){throw 'Registration path mismatch'}
 $state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
 $runner=(Get-Content "$root/../t3-340954c4/artifacts/dependency-performance-diagnosis/player-a/run-ui.ps1" -Raw).Replace('Mang.BiliCopilot.DependencyUpgrade.D34A',$identity).Replace('dependency-D34A',"cpu-$Run")
 Set-Content "$out/run-ui.ps1" $runner
 & "$out/run-ui.ps1" -AppPid $appPid -Layout $layout -Evidence "$out/player" *> "$out/player.log"
 if($LASTEXITCODE -ne 0){throw 'Player failed'}
 # Only this offline owned process; retain every loaded module hash, no command lines.
 @((Get-Process -Id $appPid).Modules | ForEach-Object { @{name=$_.ModuleName;path=$_.FileName;version=$_.FileVersionInfo.ProductVersion;sha256=(Get-FileHash $_.FileName).Hash} }) | ConvertTo-Json -Depth 4 | Set-Content "$out/modules.json"
 $collector=Start-Process "$data/host/ProfileHost.exe" -ArgumentList @($Mode,$appPid,$layout,$state,"$out/profile") -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out/collector.stdout" -RedirectStandardError "$out/collector.stderr"
 & winapp ui invoke MainChecks --on local -a $appPid --json | Set-Content "$out/main-invoke.json"
 if($LASTEXITCODE -ne 0){throw 'Main invoke failed'}
 $deadline=[DateTime]::UtcNow.AddMinutes(5)
 while(!(Test-Path "$state/main-ready.json") -and [DateTime]::UtcNow -lt $deadline){
  if(!(Get-Process -Id $appPid -ErrorAction SilentlyContinue)){throw 'Target exited'}
  Start-Sleep -Seconds 1
 }
 if(!(Test-Path "$state/main-ready.json")){throw 'Main timed out'}
 if(!$collector.WaitForExit(35000)){throw 'Collector timed out'}
 $collector.Refresh(); if($collector.ExitCode -ne 0){throw 'Collector failed'}
 foreach($name in 'idle-baseline.json','main-ready.json','main-results.json','font-results.json'){if(Test-Path "$state/$name"){Copy-Item "$state/$name" $out}}
 $baseline=Get-Content "$out/idle-baseline.json" -Raw | ConvertFrom-Json
 if($baseline.rows.Count -ne 183 -or @($baseline.rows | Where-Object scale -ne 1.5).Count){throw 'Sampling conditions failed'}
 $results=Get-Content "$out/main-results.json" -Raw | ConvertFrom-Json
 if($results.Count -ne 4 -or @($results | Where-Object status -ne PASS).Count){throw 'Main checks failed'}
 $ready=Get-Content "$out/main-ready.json" -Raw | ConvertFrom-Json
 & winapp ui invoke ExitMain -w $ready.controllerHwnd --on local -a $appPid --json | Set-Content "$out/exit-invoke.json"
 if($LASTEXITCODE -ne 0){throw 'Exit invoke failed'}
 $deadline=[DateTime]::UtcNow.AddSeconds(15)
 while((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 200}
 if(Get-Process -Id $appPid -ErrorAction SilentlyContinue){throw 'Explicit exit failed'}
 @{explicitExit=$true;pid=$appPid;rows=$baseline.rows.Count} | ConvertTo-Json | Set-Content "$out/accepted.json"
} finally {
 if($collector -and !$collector.HasExited){Stop-Process -Id $collector.Id}
 if($appPid -and $appPid -ne 9608){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and $p.Path -eq "$layout\BiliCopilot.UI.exe"){Stop-Process -Id $appPid}}
 $package=Get-AppxPackage -Name $identity
 if($package -and $package.InstallLocation -eq $layout){Remove-AppxPackage -Package $package.PackageFullName}
 & winapp ui yield --on local --json | Set-Content "$out/yield.json"
 @{pid=$appPid;processAbsent=!(Get-Process -Id $appPid -ErrorAction SilentlyContinue);identity=$identity;registrationAbsent=!(Get-AppxPackage -Name $identity);utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$out/cleanup.json"
}
