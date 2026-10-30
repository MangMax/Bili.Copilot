param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$Fixture=[IO.Path]::GetFullPath($Fixture);$Layout=[IO.Path]::GetFullPath($Layout)
[xml]$manifest=Get-Content "$Fixture/Package.appxmanifest"
$identity=$manifest.Package.Identity.Name
if($identity -notlike 'BiliCopilot.DependencyUpgrade.Controls.D34*'){throw 'Not task owned'}
if((Get-AppxPackage -Name $identity) -or (Test-Path $Layout) -or (Test-Path $Evidence)){throw 'Fresh owned run required'}
New-Item -ItemType Directory $Evidence | Out-Null
$frozen="$Fixture/frozen-output"
if(!(Test-Path $frozen)){& "$PSScriptRoot/freeze.ps1" -Fixture $Fixture -Executable Harness.exe}
$launch=& winapp run $frozen --manifest "$frozen/AppxManifest.xml" --exe Harness.exe --output-appx-directory $Layout --detach --json
$launch | Set-Content "$Evidence/launch.json"
if($LASTEXITCODE -ne 0){throw 'Controls activation failed'}
$appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
if(!$appPid -or $appPid -eq 9608 -or (Get-Process -Id $appPid).Path -ne "$Layout\Harness.exe"){throw 'Ownership mismatch'}
$closure=@(foreach($file in Get-ChildItem $frozen -Recurse -Filter '*.dll'){
    $relative=[IO.Path]::GetRelativePath($frozen,$file.FullName)
    $a=(Get-FileHash $file.FullName).Hash;$b=(Get-FileHash (Join-Path $Layout $relative)).Hash
    @{path=$relative;frozen=$a;deployed=$b;match=$a -eq $b}
})
$closure | ConvertTo-Json | Set-Content "$Evidence/closure.json"
if(@($closure|Where-Object {!$_.match}).Count){throw 'Controls closure drift'}
& "$Fixture/run-controls.ps1" -AppPid $appPid -Layout $Layout -Evidence $Evidence
if($LASTEXITCODE -ne 0){throw 'Controls failed; preserve owned state for diagnosis'}
$env:WINAPP_UI_WORKFLOW_ID='dependency-'+($identity.Split('.')[-1])
winapp ui invoke Close --on local -a $appPid --json | Set-Content "$Evidence/close.json"
if($LASTEXITCODE -ne 0){throw 'Close failed'}
$deadline=[DateTime]::UtcNow.AddSeconds(10)
while((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 200}
if(Get-Process -Id $appPid -ErrorAction SilentlyContinue){throw 'Process survived Close'}
$package=Get-AppxPackage -Name $identity
if($package.InstallLocation -ne $Layout){throw 'Registration ownership mismatch'}
Remove-AppxPackage -Package $package.PackageFullName
@{pid=$appPid;exited=$true;identity=$identity;registrationRemoved=!(Get-AppxPackage -Name $identity)} | ConvertTo-Json | Set-Content "$Evidence/cleanup.json"
winapp ui yield --on local --json | Set-Content "$Evidence/final-yield.json"
