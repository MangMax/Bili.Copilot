param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../harness-46-47/prepare-52.ps1" -SourceRoot $SourceRoot -Destination $Destination
# Replace only the generated fixture host; production links and dependencies stay identical.
$project=Join-Path $Destination 'Harness.csproj'
$xml=Get-Content $project -Raw
$xml=$xml.Replace('<Content Include="Assets/**" />','<Compile Remove="App.xaml.cs;Diagnostics.cs;Image52.cs" /><Content Include="Assets/**" />')
Set-Content $project $xml -Encoding utf8
Copy-Item "$PSScriptRoot/Experiment.cs" $Destination
$manifest=Join-Path $Destination 'Package.appxmanifest'
(Get-Content $manifest -Raw).Replace('BiliCopilot.Acceptance52T384527','BiliCopilot.Acceptance52Resources741') | Set-Content $manifest -Encoding utf8
$http=Join-Path $Destination 'Http52.cs'
(Get-Content $http -Raw).Replace('public object Manifest =>','public void SetSvg(byte[] bytes) => fixtures["svg"] = bytes; public byte[] Bytes(string key) => fixtures[key]; public object Manifest =>') | Set-Content $http -Encoding utf8
