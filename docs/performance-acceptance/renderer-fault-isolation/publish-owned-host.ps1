param([Parameter(Mandatory)][string]$Dotnet,[Parameter(Mandatory)][string]$Output, [string]$ArtifactRoot="artifacts/home-aot", [string]$MpvFeed="artifacts/aot-payload-verification/local-feed")
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$artifact=[IO.Path]::GetFullPath((Join-Path $root $ArtifactRoot))
$feed=[IO.Path]::GetFullPath((Join-Path $root $MpvFeed))
$out=[IO.Path]::GetFullPath($Output)
if(Test-Path $out){throw 'Fresh output required'}
New-Item -ItemType Directory $out | Out-Null
$argsList=@('publish',"$artifact/fixture/BiliCopilot.UI.csproj",'-c','Release','-r','win-x64',
 '-p:Platform=x64','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false','-p:NuGetAudit=false',
 "-p:SolutionDir=$artifact/source/src/","-p:PublishDir=$out/publish/",
 "-p:CustomAfterMicrosoftCommonTargets=$PSScriptRoot/../evidence-aot-payload/local-candidate.targets",
 "-p:RestoreAdditionalProjectSources=$feed","-bl:$out/publish.binlog")
$argsList|ConvertTo-Json|Set-Content "$out/arguments.json"
& $Dotnet @argsList *> "$out/publish.log"
$code=$LASTEXITCODE;$code|Set-Content "$out/exit.txt"
if($code){throw "Native host publish failed: $code"}
Copy-Item "$artifact/fixture/bin/x64/Release/net11.0-windows10.0.22621.0/win-x64/AppxManifest.xml" "$out/publish/AppxManifest.xml"
