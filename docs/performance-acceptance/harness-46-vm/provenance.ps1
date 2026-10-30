param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Prepared,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$SourceRoot=(Resolve-Path $SourceRoot).Path
[xml]$project=Get-Content "$Prepared/Harness.csproj"
$files=foreach($include in $project.Project.ItemGroup.Compile.Include){foreach($path in ($include -split ';')){$resolved=$path.Replace('$(SourceRoot)',$SourceRoot);if($resolved.Contains('**/')){Get-ChildItem ($resolved.Split('**/')[0]) -File -Recurse -Filter '*.cs'}else{Get-ChildItem $resolved -File}}}
$hashes=@($files|Sort-Object FullName -Unique|Get-FileHash|ForEach-Object {@{path=[IO.Path]::GetRelativePath($SourceRoot,$_.Path);sha256=$_.Hash}})
$xaml=foreach($name in 'AIPanel','AIBody','AIFooter','AIHeader'){
 $sourceHash=(Get-FileHash "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/AI/$name.xaml").Hash
 $preparedHash=(Get-FileHash "$Prepared/$name.xaml").Hash
 if($sourceHash -ne $preparedHash){throw "Changed production XAML: $name"}
 @{name=$name;sha256=$sourceHash;exactCopy=$true}
}
$built="$Prepared/bin/x64/Debug/net9.0-windows10.0.22621.0/win-x64/Harness.dll"
$launchedHash=(Get-FileHash "$Layout/Harness.dll").Hash
$rebuildHash=(Get-FileHash $built).Hash
if($launchedHash -ne $rebuildHash){throw 'Clean rebuild differs from tested assembly'}
$fixtureHashes=Get-ChildItem "$Prepared/*.cs","$Prepared/*.xaml","$Prepared/*.csproj"|Get-FileHash|ForEach-Object{@{name=[IO.Path]::GetFileName($_.Path);sha256=$_.Hash}}
@{sourceHead=(git -C $SourceRoot rev-parse HEAD);kernel=(git -C "$SourceRoot/externals/bili-kernel" rev-parse HEAD);sourceHashes=$hashes;productXaml=$xaml;fixtureHashes=$fixtureHashes;testedDll=$launchedHash;cleanRebuiltDll=$rebuildHash;testedLayout=$Layout;prepared=$Prepared;runtime='Debug net9.0 WinUI x64 self-contained JIT';ownedIdentity='BiliCopilot.Acceptance46VM8B8F';ownedPids=@(40308,57192,46968)}|ConvertTo-Json -Depth 7|Set-Content "$Evidence/provenance.json"
