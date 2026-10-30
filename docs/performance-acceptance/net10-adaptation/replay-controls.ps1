param([Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$out="$root/artifacts/net10-adaptation/controls-native"
if(Test-Path $out){throw 'Fresh evidence directory required'}
New-Item -ItemType Directory $out | Out-Null
$outputs=@(Get-ChildItem "$Fixture/bin" -Recurse -Filter AppxManifest.xml | Where-Object FullName -Like '*\Release\*')
if($outputs.Count -ne 1){throw 'Ambiguous controls output'}
$build=$outputs[0].Directory.FullName
& "$PSScriptRoot/freeze.ps1" -Fixture $Fixture -BuildOutput $build
$frozen="$Fixture/frozen-output"
[xml]$manifest=Get-Content "$frozen/AppxManifest.xml"
$identity=$manifest.Package.Identity.Name
if($identity -ne 'BiliCopilot.DependencyUpgrade.Controls.N854Ctrl'){throw 'Not owned controls identity'}
if(Get-AppxPackage -Name $identity){throw 'Existing registration'}
$layout=[IO.Path]::GetFullPath("$out/layout")
$env:WINAPP_UI_WORKFLOW_ID='dependency-N854Ctrl'
function Hashes($dir){ @(Get-ChildItem $dir -Recurse -Filter '*.dll' | ForEach-Object { "$([IO.Path]::GetRelativePath($dir,$_.FullName).ToLowerInvariant())=$((Get-FileHash $_.FullName).Hash)" }) }
$expected=Hashes $frozen
$appPid=0
try {
 $launch=& winapp run $frozen --manifest "$frozen/AppxManifest.xml" --exe Harness.exe --output-appx-directory $layout --detach --json
 $launch | Set-Content "$out/launch.json"
 if($LASTEXITCODE -ne 0){throw 'Launch failed'}
 $appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
 if(!$appPid -or $appPid -eq 9608 -or [IO.Path]::GetFullPath((Get-Process -Id $appPid).Path) -ne [IO.Path]::GetFullPath("$layout/Harness.exe")){throw 'Not owned process'}
 if(Compare-Object $expected (Hashes $layout)){throw 'Controls DLL closure mismatch'}
 @{count=$expected.Count;missing=0;extra=0;mismatched=0;dlls=$expected} | ConvertTo-Json | Set-Content "$out/hash-gate.json"
 & "$Fixture/run-controls.ps1" -AppPid $appPid -Layout $layout -Evidence $out *> "$out/runner.log"
 if($LASTEXITCODE -ne 0){throw 'Controls failed'}
 $results=Get-Content "$out/results.json" -Raw | ConvertFrom-Json
 if($results.Count -ne 7 -or @($results | Where-Object status -ne PASS).Count){throw 'Expected seven passing checks'}
 & winapp ui invoke Close --on local -a $appPid --json | Set-Content "$out/close.json"
 if($LASTEXITCODE -ne 0){throw 'Close failed'}
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 while((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(Get-Process -Id $appPid -ErrorAction SilentlyContinue){throw 'Controls did not exit'}
} finally {
 if($appPid -gt 0 -and $appPid -ne 9608){$p=Get-Process -Id $appPid -ErrorAction SilentlyContinue; if($p -and [IO.Path]::GetFullPath($p.Path) -eq [IO.Path]::GetFullPath("$layout/Harness.exe")){Stop-Process -Id $appPid}}
 $package=Get-AppxPackage -Name $identity
 if($package -and $package.InstallLocation -eq $layout){Remove-AppxPackage -Package $package.PackageFullName}
 & winapp ui yield --on local --json | Set-Content "$out/final-yield.json"
 @{pid=$appPid;processAbsent=($appPid -eq 0 -or !(Get-Process -Id $appPid -ErrorAction SilentlyContinue));identity=$identity;registrationAbsent=!(Get-AppxPackage -Name $identity)} | ConvertTo-Json | Set-Content "$out/cleanup.json"
}
