param([string]$ArtifactRoot = (Join-Path (Get-Location) 'artifacts/engine49-trace'))
$ErrorActionPreference = 'Stop'
$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
foreach ($variant in 'original', 'candidate') {
    $base = Join-Path $ArtifactRoot $variant
    $env:NUGET_PACKAGES = Join-Path $base 'packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $base 'http-cache'
    $env:DOTNET_CLI_HOME = Join-Path $base 'cli'
    $source = Join-Path $base 'danmaku-kernel-4d0f8691f08f1e143294402477e72306a1b38b6f'
    if (Get-ChildItem "$source/src" -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' }) {
        throw 'Control builds require a fresh prepared tree; choose a new destination'
    }
    foreach ($name in 'Core', 'Legacy') {
        $project = Join-Path $source "src/Danmaku.$name/Danmaku.$name.csproj"
        & dotnet build $project -c Release -p:Platform=x64 --no-incremental -v:minimal *> (Join-Path $base "build-$name.log")
        $code = $LASTEXITCODE
        Write-Output "$variant $name exit=$code"
        if ($code -ne 0) { throw "Build failed; inspect $base/build-$name.log" }
    }
}
