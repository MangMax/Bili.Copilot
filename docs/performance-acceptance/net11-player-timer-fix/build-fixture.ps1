param(
    [Parameter(Mandatory)][ValidateSet('stop-only','green','final')][string]$Variant,
    [Parameter(Mandatory)][string]$SdkRoot
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$base="$root/artifacts/timer-fix/$Variant"
if(Test-Path "$base/fixture/frozen-output"){throw 'Frozen variant exists; use a new checkout, never overwrite evidence'}
$env:DOTNET_ROOT=$SdkRoot
$sdk=Join-Path $SdkRoot 'dotnet.exe'
if((& $sdk --version) -ne '11.0.100-rc.3.26522.148'){throw 'Exact RC3 SDK required'}
& $sdk build "$base/fixture/BiliCopilot.UI.csproj" -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/artifacts/timer-fix/stop-only/source/src/" "-p:SourceRoot=$root/artifacts/timer-fix/stop-only/source" "-p:BuildOutDir=$root/artifacts/timer-fix/stop-only/references/" -nr:false -m:4 *> "$base/build.log"
if($LASTEXITCODE -ne 0){throw 'Build failed; preserve log and outputs before another attempt'}
& python "$PSScriptRoot/freeze.py" $base
if($LASTEXITCODE -ne 0){throw 'Freeze failed; do not launch'}
