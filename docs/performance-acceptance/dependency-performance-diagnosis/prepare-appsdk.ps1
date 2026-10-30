# Run only after graphics-group sampling ends and only if it gives no clear direction.
param([Parameter(Mandatory)][string]$Workspace)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Workspace)
$data="$root/artifacts/dependency-performance-diagnosis"
$fixture="$data/player-c"
& "$PSScriptRoot/prepare-native.ps1" -Destination $fixture -RunId D34C -Variant win2d-icons
$kernel="$root/externals/bili-kernel/src/Directory.Packages.props"
if(git -C "$root/externals/bili-kernel" status --porcelain){throw 'Do not overwrite dirty kernel'}
$original=[IO.File]::ReadAllBytes($kernel)
$before='<PackageVersion Include="Microsoft.WindowsAppSDK" Version="2.6.4" />'
$after='<PackageVersion Include="Microsoft.WindowsAppSDK" Version="1.8.251106002" />'
try {
    foreach($path in @("$fixture/Directory.Packages.props",$kernel)){
        $text=[IO.File]::ReadAllText($path)
        if(!$text.Contains($before)){throw "Unexpected AppSDK declaration in $path"}
        [IO.File]::WriteAllText($path,$text.Replace($before,$after))
    }
    Copy-Item $kernel "$data/kernel-appsdk-c.props"
    Get-FileHash "$fixture/Directory.Packages.props",$kernel | ConvertTo-Json | Set-Content "$data/appsdk-c-props-hashes.json"
    git -C "$root/externals/bili-kernel" diff -- src/Directory.Packages.props | Set-Content "$data/kernel-appsdk-c.patch"
    dotnet build "$fixture/BiliCopilot.UI.csproj" -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/src/" *> "$data/build-c.log"
    if($LASTEXITCODE -ne 0){throw 'AppSDK group build failed; preserve red and stop'}
    python "$PSScriptRoot/graphs.py" $fixture "$data/graph-c.json"
    if($LASTEXITCODE -ne 0){throw 'AppSDK group graph invalid'}
    foreach($name in 'Resolvers.WinUICookies','Resolvers.WinUIQRCode','Resolvers.WinUIToken'){
        python "$PSScriptRoot/graphs.py" "$root/externals/bili-kernel/src/Resolvers/$name" "$data/graph-c-$name.json"
        if($LASTEXITCODE -ne 0){throw 'Kernel closure invalid'}
    }
    & "$PSScriptRoot/freeze.ps1" -Fixture $fixture
} finally {
    [IO.File]::WriteAllBytes($kernel,$original)
    if((Get-FileHash $kernel).Hash -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($original))){throw 'Kernel props restoration failed'}
}
