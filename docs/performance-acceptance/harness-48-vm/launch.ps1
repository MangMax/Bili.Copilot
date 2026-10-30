param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw 'Choose an entirely fresh output directory'}
if(Get-AppxPackage -Name Mang.BiliCopilot.Offline48.C6355575b){throw 'Clean up the previous owned host before using a fresh destination'}
& "$PSScriptRoot/prepare.ps1" -SourceRoot $root -Destination "$Destination/app"
& winapp run "$Destination/app/BiliCopilot.UI.csproj" -c Debug --arch x64 --detach --json --output-appx-directory "$Destination/layout" -p Platform=x64 -p AppxPackageSigningEnabled=false -p TreatWarningsAsErrors=false -p "BuildOutDir=$Destination/build/" -p "SolutionDir=$root/src/" *> "$Destination/build-launch.log"
$code=$LASTEXITCODE
Get-Content "$Destination/build-launch.log" -Tail 8
exit $code
