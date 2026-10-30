param([ValidateSet('test','build','environment')][string]$Gate = 'test', [string]$Stage = 'replay')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Set-Location $root
$out = Join-Path $root "artifacts/integration-pr27-pr43/$Stage"
New-Item -ItemType Directory -Force $out | Out-Null
switch ($Gate) {
    environment {
        git rev-parse HEAD
        git submodule status
        dotnet --info
        winapp --version
        Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber
        Get-CimInstance Win32_Processor | Select-Object Name, NumberOfLogicalProcessors
        Get-FileHash src/Directory.Packages.props, tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj
    }
    test {
        dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=$Stage.trx" --results-directory $out
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
        [xml]$trx = Get-Content (Join-Path $out "$Stage.trx")
        $c = $trx.TestRun.ResultSummary.Counters
        if ([int]$c.total -eq 0 -or [int]$c.failed -ne 0 -or $c.executed -ne $c.passed) { throw "Incomplete/failed TRX: $($c.OuterXml)" }
        $c | ConvertTo-Json | Set-Content (Join-Path $out 'counters.json')
    }
    build {
        dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/src/"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    }
}
