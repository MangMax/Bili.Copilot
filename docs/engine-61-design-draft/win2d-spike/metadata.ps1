param([string]$WorkRoot = 'artifacts/win2d-spike')
$ErrorActionPreference='Stop'
$WorkRoot=[IO.Path]::GetFullPath($WorkRoot)
$files=[Collections.Generic.List[object]]::new()
function HashFile([string]$role,[string]$path) {
    $files.Add(@{role=$role;file=(Split-Path $path -Leaf);sha256=(Get-FileHash -LiteralPath $path).Hash.ToLower();bytes=(Get-Item -LiteralPath $path).Length})
}
foreach($name in @('Program.cs','App.xaml','Spike.csproj','Package.appxmanifest','app.manifest')) { HashFile "source/$name" "$WorkRoot/harness/$name" }
foreach($name in @('Spike.dll','Spike.exe','Microsoft.Graphics.Canvas.dll','Microsoft.Graphics.Canvas.Interop.dll','AppxManifest.xml')) { HashFile "layout/$name" "$WorkRoot/layout/$name" }
foreach($name in @('01-real-control.png','02-complete.png','events.jsonl')) { HashFile "evidence/$name" "$WorkRoot/run/$name" }
$cache=Join-Path $env:USERPROFILE '.nuget/packages/microsoft.graphics.win2d/1.3.2'
HashFile 'nuget-package' "$cache/microsoft.graphics.win2d.1.3.2.nupkg"
HashFile 'cache/native' "$cache/runtimes/win-x64/native/Microsoft.Graphics.Canvas.dll"
HashFile 'cache/interop' "$cache/lib/net6.0-windows10.0.19041.0/Microsoft.Graphics.Canvas.Interop.dll"
$assets=Get-Content "$WorkRoot/harness/obj/project.assets.json" -Raw | ConvertFrom-Json
$packages=@($assets.libraries.PSObject.Properties.Name | Where-Object {$_ -match 'Win2D|WindowsAppSDK|NETCore.App.Runtime|BuildTools'})
$events=Get-Content "$WorkRoot/run/events.jsonl" | ConvertFrom-Json
@{utc=[DateTime]::UtcNow.ToString('o');sdk=(& dotnet --version);winapp=(& winapp --version);packages=$packages;win2dSource=(Get-Content "$cache/Win2d.githash.txt" -Raw).Trim();identity=($events | Where-Object name -eq Identity).data;loadedModules=@(($events | Where-Object name -eq LoadedModule).data);files=$files.ToArray()} | ConvertTo-Json -Depth 8 | Set-Content "$WorkRoot/run/metadata.json"
