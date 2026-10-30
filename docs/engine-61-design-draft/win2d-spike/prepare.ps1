param([string]$WorkRoot = 'artifacts/win2d-spike-replay')
$ErrorActionPreference = 'Stop'
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
if(Test-Path $WorkRoot) { throw 'Use a new ignored artifacts directory.' }
$harness = Join-Path $WorkRoot 'harness'
New-Item -ItemType Directory "$harness/Assets" -Force | Out-Null
Copy-Item "$PSScriptRoot/Spike.csproj","$PSScriptRoot/Program.cs","$PSScriptRoot/App.xaml","$PSScriptRoot/app.manifest","$PSScriptRoot/Package.appxmanifest" $harness
# Preserve measured source bytes across Git autocrlf and cosmetic manifest whitespace cleanup.
foreach($name in @('Program.cs','App.xaml','Spike.csproj')) {
    $text=[IO.File]::ReadAllText("$harness/$name").Replace("`r`n","`n")
    [IO.File]::WriteAllText("$harness/$name",$text,[Text.UTF8Encoding]::new($false))
}
$manifests=Get-Content "$PSScriptRoot/evidence/source-manifests.json" -Raw | ConvertFrom-Json
foreach($entry in $manifests) { [IO.File]::WriteAllBytes("$harness/$($entry.file)",[Convert]::FromBase64String($entry.base64)) }
'<Project />' | Set-Content "$harness/Directory.Build.props"
'<Project />' | Set-Content "$harness/Directory.Packages.props"
$repo = (Resolve-Path "$PSScriptRoot/../../..").Path
Copy-Item "$repo/src/Desktop/BiliCopilot.UI/Assets/Logo/Square150x150Logo.scale-100.png" "$harness/Assets/Logo.png"
# Existing cache only. No remote feeds, upgrades, tool installation, or app dependencies.
dotnet restore "$harness/Spike.csproj" --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false -v:minimal
if($LASTEXITCODE -ne 0) { throw 'Offline restore failed; do not install missing assets.' }
dotnet build "$harness/Spike.csproj" -c Release --no-restore -v:minimal
if($LASTEXITCODE -ne 0) { throw 'Harness build failed' }
