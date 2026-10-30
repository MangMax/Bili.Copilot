param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$out="$root/artifacts/net10-adaptation"
# Execute only after every owned native sampling run has exited and yielded.
if(Get-AppxPackage -Name 'Mang.BiliCopilot.Runtime.N854.*'){throw 'Owned native registration still active; finish replay first'}
if(Get-AppxPackage -Name 'BiliCopilot.DependencyUpgrade.Controls.N854Ctrl'){throw 'Controls registration still active'}
& dotnet --info *> "$out/final-toolchain.log"
if($LASTEXITCODE -ne 0){throw 'SDK inspection failed'}
foreach($configuration in 'Debug','Release'){
 & dotnet build "$root/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj" -t:Rebuild -c $configuration -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/src/" *> "$out/final-$($configuration.ToLowerInvariant()).log"
 if($LASTEXITCODE -ne 0){throw "$configuration rebuild failed"}
}
& dotnet build "$root/src/Desktop/BiliCopilot.Visor/BiliCopilot.Visor.csproj" -c Release -p:Platform=x64 -p:PublishAot=false "-p:SolutionDir=$root/src/" *> "$out/final-visor.log"
if($LASTEXITCODE -ne 0){throw 'Visor build failed'}
& dotnet msbuild "$root/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj" -t:ValidateRuntimeDependencies -p:Platform=x64 "-p:SolutionDir=$root/src/" *> "$out/final-runtime.log"
if($LASTEXITCODE -ne 0){throw 'Runtime asset validation failed'}
& dotnet test "$root/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj" -c Release --logger 'trx;LogFileName=final-net10.trx' --results-directory "$out/tests-final" *> "$out/final-tests.log"
if($LASTEXITCODE -ne 0){throw 'Full suite failed'}
$graphs=foreach($relative in 'src/Desktop/BiliCopilot.UI/obj/project.assets.json','src/Desktop/BiliCopilot.Visor/obj/project.assets.json','src/Desktop/BiliCopilot.Visor.Models/obj/project.assets.json','tests/BiliCopilot.UI.Tests/obj/project.assets.json'){
 $file="$root/$relative"; $assets=Get-Content $file -Raw | ConvertFrom-Json
 Copy-Item $file "$out/final-assets-$($relative.Replace('/','_'))"
 @{path=$relative;sha256=(Get-FileHash $file).Hash;targets=@($assets.targets.PSObject.Properties.Name);libraries=@($assets.libraries.PSObject.Properties.Name | Sort-Object)}
}
$graphs | ConvertTo-Json -Depth 8 | Set-Content "$PSScriptRoot/production-package-graphs.json"
