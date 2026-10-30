param([switch]$RestoreTools)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Set-Location $repoRoot
$outputRoot = Join-Path $repoRoot 'artifacts/exit-full-analysis'
$dotnetExe = 'C:/Program Files/dotnet/dotnet.exe'
New-Item -ItemType Directory -Force $outputRoot | Out-Null
foreach ($name in @('Program.cs', 'Metadata.cs', 'Analyze.csproj', 'packages.lock.json')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $outputRoot $name)
}
python (Join-Path $PSScriptRoot 'parse_full.py') > (Join-Path $outputRoot 'parsed-summary.json')
if ($LASTEXITCODE) { throw 'Authorized dump verification failed' }
python (Join-Path $PSScriptRoot 'identities.py') > (Join-Path $outputRoot 'identities-check.txt')
if ($LASTEXITCODE) { throw 'Frozen module verification failed' }
if ($RestoreTools) {
    & $dotnetExe restore (Join-Path $outputRoot 'Analyze.csproj') --locked-mode --source https://api.nuget.org/v3/index.json --packages (Join-Path $outputRoot 'packages') -v quiet
    if ($LASTEXITCODE) { throw 'Task-local tool restore failed' }
    foreach ($package in Get-ChildItem (Join-Path $outputRoot 'packages') -Recurse -Filter '*.nupkg') {
        & $dotnetExe nuget verify $package.FullName >> (Join-Path $outputRoot 'package-signatures.txt')
        if ($LASTEXITCODE) { throw "Package verification failed: $($package.Name)" }
    }
}
& $dotnetExe run --project (Join-Path $outputRoot 'Analyze.csproj') --no-restore > (Join-Path $outputRoot 'managed.txt')
if ($LASTEXITCODE) { throw 'Managed/LEO analysis failed' }
& $dotnetExe run --project (Join-Path $outputRoot 'Analyze.csproj') --no-restore -- metadata > (Join-Path $outputRoot 'metadata.txt')
if ($LASTEXITCODE) { throw 'Frozen IL/PDB inspection failed' }
Write-Output 'Verified authorized dump, matching DAC, LEO, JIT code and frozen metadata. This is not a clean-exit verdict.'
