param([string]$SourceRoot=(Resolve-Path "$PSScriptRoot/../../..").Path,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path $Destination){throw 'Destination must be fresh'}
New-Item -ItemType Directory $Destination | Out-Null
# Copy tracked UI sources only. Production checkout, kernel and retained app layout are never edited.
$ui=Join-Path $SourceRoot 'src/Desktop/BiliCopilot.UI'
foreach($file in (git -C $SourceRoot ls-files src/Desktop/BiliCopilot.UI)){
 $rel=$file.Substring('src/Desktop/BiliCopilot.UI/'.Length)
 $target=Join-Path $Destination $rel
 New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
 Copy-Item -LiteralPath (Join-Path $SourceRoot $file) -Destination $target
}
Copy-Item "$SourceRoot/src/Directory.Packages.props" $Destination
'<Project><PropertyGroup><LangVersion>preview</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>' | Set-Content "$Destination/Directory.Build.props"
[xml]$project=Get-Content "$Destination/BiliCopilot.UI.csproj"
foreach($ref in $project.Project.ItemGroup.ProjectReference){if($ref){$ref.Include=[IO.Path]::GetFullPath((Join-Path $ui $ref.Include))}}
$project.Save("$Destination/BiliCopilot.UI.csproj")
$jit=Get-Content "$Destination/BiliCopilot.UI.csproj" -Raw
$jit.Replace('<PublishAot>True</PublishAot>','<PublishAot>False</PublishAot>') | Set-Content "$Destination/BiliCopilot.UI.csproj"
# Only host composition differs: no normal launch, auth, notification or tray startup.
Copy-Item "$PSScriptRoot/App.xaml.cs" "$Destination/App.xaml.cs" -Force
Copy-Item "$PSScriptRoot/Fixture.cs" "$Destination/Fixture.cs"
$deps=Get-Content "$ui/GlobalDependencies.cs" -Raw
$deps=$deps.Replace('.Build();','.WithOffline48().Build();')
$deps | Set-Content "$Destination/GlobalDependencies.cs"
$manifest=Get-Content "$ui/Package.Dev.appxmanifest" -Raw
$manifest=$manifest.Replace('Mang.BiliCopilot.Dev','Mang.BiliCopilot.Offline48.D1d19DurationFix').Replace('ms-resource:AppName','Offline Player VM 48')
$manifest | Set-Content "$Destination/Package.Dev.appxmanifest"
$native='C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets/libmpv/x64/libmpv-2.dll'
New-Item -ItemType Directory -Force "$Destination/Assets/libmpv/x64" | Out-Null
Copy-Item $native "$Destination/Assets/libmpv/x64/libmpv-2.dll"
New-Item -ItemType Directory -Force "$Destination/Assets/ffmpeg/x64" | Out-Null
Copy-Item 'C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets/ffmpeg/x64/ffmpeg.exe' "$Destination/Assets/ffmpeg/x64/ffmpeg.exe"
# Generate a silent local PCM WAV; no media or authenticated network requests.
$stream=[IO.File]::Create("$Destination/Assets/offline.wav");$w=[IO.BinaryWriter]::new($stream)
try {$size=8000*2*120;$w.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$w.Write([int]($size+36));$w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$w.Write([int]16);$w.Write([int16]1);$w.Write([int16]1);$w.Write([int]8000);$w.Write([int]16000);$w.Write([int16]2);$w.Write([int16]16);$w.Write([Text.Encoding]::ASCII.GetBytes('data'));$w.Write([int]$size);$w.Write([byte[]]::new($size))} finally {$w.Dispose()}
Get-ChildItem "$ui/ViewModels/Core/PlayerViewModel/*.cs","$ui/ViewModels/Core/VideoConnectorViewModel/*.cs","$ui/ViewModels/Core/PgcConnectorViewModel/*.cs","$ui/ViewModels/Core/DanmakuRenderViewModel/*.cs","$ui/Danmaku/*.cs","$ui/Forms/PlayerWindow*.cs" | Get-FileHash | ConvertTo-Json | Set-Content "$Destination/source-hashes.json"
