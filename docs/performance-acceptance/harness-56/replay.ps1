param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Scratch,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$SourceRoot=(Resolve-Path $SourceRoot).Path
$Scratch=[IO.Path]::GetFullPath($Scratch)
$Evidence=[IO.Path]::GetFullPath($Evidence)
if((Test-Path $Scratch) -or (Test-Path $Evidence)){throw 'Require new scratch and evidence paths'}
if(Get-AppxPackage -Name BiliCopilot.Cache56F577){throw 'Owned identity already registered; refuse takeover'}
New-Item -ItemType Directory $Scratch,$Evidence | Out-Null
& "$PSScriptRoot/prepare.ps1" -SourceRoot $SourceRoot -Destination "$Scratch/project"
$cache=Join-Path $env:USERPROFILE '.nuget/packages'
& dotnet build "$Scratch/project/Harness.csproj" -c Release -p:Platform=x64 "-p:RestoreSources=$cache" -p:NuGetAudit=false *> "$Evidence/build.txt"
if($LASTEXITCODE -ne 0){throw 'Harness build failed; inspect build.txt'}
& dotnet build "$PSScriptRoot/../harness-47-gc/Trace/Trace.csproj" -c Release "-p:RestoreSources=$cache" -p:NuGetAudit=false *> "$Evidence/trace-build.txt"
if($LASTEXITCODE -ne 0){throw 'Cached trace build failed; no install fallback'}
& python "$PSScriptRoot/source-hashes.py" $SourceRoot "$Evidence/linked-source-hashes.json"
if($LASTEXITCODE -ne 0){throw 'Source hashes failed'}
foreach($number in 1..2){
 & "$PSScriptRoot/run.ps1" -Project "$Scratch/project/Harness.csproj" -Layout "$Scratch/layout-$number" -Evidence "$Evidence/run-$number" -TracePrefix "$Scratch/trace-$number"
}
& python "$PSScriptRoot/analyze.py" $Evidence run-1 run-2
if($LASTEXITCODE -ne 0){throw 'Observation oracle failed'}
& python "$PSScriptRoot/verify.py" $Evidence run-1 run-2
if($LASTEXITCODE -ne 0){throw 'Provenance check failed'}
& python "$PSScriptRoot/chart.py" $Evidence run-1 run-2
if($LASTEXITCODE -ne 0){throw 'Chart generation failed'}
Write-Output 'Protocol and provenance checked. Every PNG still requires individual visual review; no production-budget approval.'
