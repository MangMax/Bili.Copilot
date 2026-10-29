param([string]$ArtifactRoot = (Join-Path (Get-Location) 'artifacts/engine49-trace'))
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$env:NUGET_PACKAGES = "$ArtifactRoot/trace/packages"
$env:NUGET_HTTP_CACHE_PATH = "$ArtifactRoot/trace/http-cache"
$env:DOTNET_CLI_HOME = "$ArtifactRoot/trace/cli"
dotnet build "$ArtifactRoot/harness/Harness.csproj" -c Release -p:Platform=x64 -p:PackageVersion=0.0.1-preview6-trace49-9d66 --no-incremental -v:minimal *> "$ArtifactRoot/trace/build-host.log"
exit $LASTEXITCODE
