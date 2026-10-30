param([Parameter(Mandatory)][string]$Destination,[ValidateSet('net9.0','net11.0')][string]$Framework='net9.0')
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$runtime=if($Framework -eq 'net9.0'){'9.0.22'}else{'11.0.0-rc.3.26522.148'}
if(Test-Path $Destination){throw 'Use a fresh destination'}
New-Item -ItemType Directory $Destination | Out-Null
$source=[Security.SecurityElement]::Escape("$root/src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs")
"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>$Framework</TargetFramework><RuntimeIdentifier>win-x64</RuntimeIdentifier><SelfContained>true</SelfContained><RuntimeFrameworkVersion>$runtime</RuntimeFrameworkVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include=`"$source`" Link=`"RecommendListDiff.cs`"/></ItemGroup></Project>" | Set-Content "$Destination/Measure.csproj"
# Preserve the historical workload byte-for-byte; only SDK/TFM/runtime selection differs.
$legacy=Get-Content "$PSScriptRoot/../integration-pr27-pr43/measure-list.ps1" -Raw
$program=[regex]::Match($legacy,"(?s)@'\r?\n(.*?)\r?\n'@").Groups[1].Value
if(!$program){throw 'Historical benchmark program not found'}
Set-Content "$Destination/Program.cs" $program
$dotnet=if($Framework -eq 'net11.0'){"$root/artifacts/dependency-upgrade/sdk11/dotnet.exe"}else{'dotnet'}
$previousRoot=$env:DOTNET_ROOT
$previousPath=$env:PATH
if($Framework -eq 'net11.0') {
    '{"sdk":{"version":"11.0.100-rc.3.26522.148","rollForward":"disable","allowPrerelease":true}}' | Set-Content "$Destination/global.json"
    $env:DOTNET_ROOT=Split-Path $dotnet
    $env:PATH="$env:DOTNET_ROOT;$previousPath"
}
Push-Location $Destination
try {
    & $dotnet run --project Measure.csproj -c Release -- "$Destination/raw.json"
    if($LASTEXITCODE -ne 0){throw 'Measurement failed'}
    git -C $root rev-parse HEAD | Set-Content "$Destination/source-sha.txt"
    Get-FileHash "$root/src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs" | ConvertTo-Json | Set-Content "$Destination/source-hash.json"
} finally {Pop-Location;$env:DOTNET_ROOT=$previousRoot;$env:PATH=$previousPath}
