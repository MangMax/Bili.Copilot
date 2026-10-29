param([string]$Output = (Join-Path $PSScriptRoot 'evidence/reproduction'))
$ErrorActionPreference = 'Stop'
function Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $Command (exit $LASTEXITCODE)" }
}
& (Join-Path $PSScriptRoot 'prepare.ps1')
$cache = Join-Path $env:USERPROFILE '.nuget/packages'
$project = Join-Path $PSScriptRoot 'Replay.csproj'
$tests = Join-Path $PSScriptRoot 'Tests/Replay.Tests.csproj'
Checked { dotnet restore $tests --source $cache -p:NuGetAudit=false }
Checked { dotnet test $tests -c Release --no-restore --logger 'trx;LogFileName=contracts.trx' --results-directory $Output }
$previousTiering = $env:DOTNET_TieredCompilation
$previousSha = $env:ISSUE58_INTEGRATION_SHA
try {
    $env:DOTNET_TieredCompilation = '0'
    $env:ISSUE58_INTEGRATION_SHA = git -C $PSScriptRoot rev-parse HEAD
    # Never build or test concurrently with these measurements.
    Checked { dotnet run --project $project -c Release --no-build --no-restore -- $Output }
    Checked { dotnet run --project $project -c Release --no-build --no-restore -- $Output errors }
} finally {
    $env:DOTNET_TieredCompilation = $previousTiering
    $env:ISSUE58_INTEGRATION_SHA = $previousSha
}
