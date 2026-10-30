param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$Destination=(Resolve-Path $Destination).Path
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Force $Evidence | Out-Null
$process=Get-Process -Id $AppPid
if($AppPid -eq 9608 -or $process.Path -ne "$Destination\layout\BiliCopilot.UI.exe"){throw 'Not owned process'}
$rows=@(foreach($file in (git -C $root ls-files src/Desktop/BiliCopilot.UI)){
 $rel=$file.Substring('src/Desktop/BiliCopilot.UI/'.Length)
 if($rel -in @('App.xaml.cs','GlobalDependencies.cs') -or $rel -notmatch '\.(cs|xaml)$'){continue}
 $source=(Get-FileHash (Join-Path $root $file)).Hash
 $copy=(Get-FileHash (Join-Path "$Destination/app" $rel)).Hash
 if($source -ne $copy){throw "Source differs: $rel"}
 @{path=$file;sha256=$source;copyEqual=$true}
})
$rows | ConvertTo-Json -Depth 4 | Set-Content "$Evidence/source-hashes.json"
$hostRows=@(foreach($name in 'App.xaml.cs','Fixture.cs'){
 $source=(Get-FileHash "$PSScriptRoot/$name").Hash;$copy=(Get-FileHash "$Destination/app/$name").Hash
 if($source -ne $copy){throw "Host source differs: $name"};@{path=$name;sha256=$source;copyEqual=$true}
})
$hostRows | ConvertTo-Json | Set-Content "$Evidence/host-source-hashes.json"
$modules=@($process.Modules | Where-Object {$_.ModuleName -match '^BiliCopilot.UI.dll$|^Richasy.BiliKernel.(Core|Services.Media|Abstractions|Grpc).dll$|^libmpv-2.dll$'})
if($modules.Count -lt 6){throw 'Missing expected loaded modules'}
$moduleRows=@(foreach($module in $modules){@{name=$module.ModuleName;path=$module.FileName;sha256=(Get-FileHash $module.FileName).Hash}})
$moduleRows | ConvertTo-Json | Set-Content "$Evidence/loaded-module-hashes.json"
$build=@(Get-ChildItem "$Destination/app/bin" -Recurse -Filter BiliCopilot.UI.dll)
if($build.Count -ne 1){throw 'Ambiguous build DLL'}
$buildHash=(Get-FileHash $build[0].FullName).Hash
$loadedHash=($moduleRows | Where-Object name -eq BiliCopilot.UI.dll).sha256
if($buildHash -ne $loadedHash){throw 'Build/layout/loaded DLL mismatch'}
$kernelFiles=@('src/Services/Services.Media/DanmakuService.cs','src/Services/Services.Media/Core/DanmakuClient.cs','src/Services/Services.Media/Core/Adapters/PgcAdapter.cs','src/Services/Services.Media/Core/Adapters/VideoAdapter.cs','src/BiliKernel.Core/Http/BiliHttpClient.cs','src/BiliKernel.Core/Http/BiliHttpClient.Extension.cs')
@($kernelFiles | ForEach-Object {@{path=$_;sha256=(Get-FileHash "$root/externals/bili-kernel/$_").Hash}}) | ConvertTo-Json | Set-Content "$Evidence/kernel-source-hashes.json"
$package=Get-AppxPackage -Name Mang.BiliCopilot.Offline48.C6355575b
@{head=(git -C $root rev-parse HEAD);parent=(git -C $root rev-parse feat/performance-45);kernel=(git -C "$root/externals/bili-kernel" rev-parse HEAD);pid=$AppPid;exe=$process.Path;package=$package.PackageFullName;layout=$package.InstallLocation;productionCopiedFiles=$rows.Count;buildDll=$build[0].FullName;buildDllSha256=$buildHash;buildLayoutLoadedEqual=$true;buildLogSha256=(Get-FileHash "$Destination/build-launch.log").Hash;uiLogSha256=(Get-FileHash "$Destination/ui.log").Hash;freshDestination=$Destination} | ConvertTo-Json | Set-Content "$Evidence/provenance.json"
