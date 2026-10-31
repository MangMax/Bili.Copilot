$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
Set-Location $root
if(Get-AppxPackage -Name BiliCopilot.ListP14011.Owned){throw 'Finish native performance and cleanup before building'}
$out=Join-Path $root 'artifacts/list-p1'
function Record-Run([string]$name,[scriptblock]$command) {
    if(Test-Path "$out/$name.json"){throw 'Fresh validation outputs required'}
    $start=Get-Date
    & $command *> "$out/$name.log"
    $code=$LASTEXITCODE
    @{startUtc=$start.ToUniversalTime().ToString('O');endUtc=(Get-Date).ToUniversalTime().ToString('O');exitCode=$code} | ConvertTo-Json | Set-Content "$out/$name.json"
    if($code -ne 0){Get-Content "$out/$name.log" -Tail 25; throw "$name failed: $code"}
}
Record-Run 'tests-targeted' {
    dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:NuGetAudit=false --filter 'FullyQualifiedName~Recommend|FullyQualifiedName~PopularPageLoading|FullyQualifiedName~IncrementVerticalScrollControl|FullyQualifiedName~MarkdownImageLoader' --logger 'trx;LogFileName=targeted.trx' --results-directory artifacts/list-p1/tests-targeted
}
Record-Run 'tests-full' {
    dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:NuGetAudit=false --logger 'trx;LogFileName=full.trx' --results-directory artifacts/list-p1/tests-full
}
Record-Run 'production-build' {
    dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:Rebuild -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:NuGetAudit=false "-p:SolutionDir=$root/src/"
}
