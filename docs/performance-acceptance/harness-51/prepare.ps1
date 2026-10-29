param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$SourceRoot=(Resolve-Path -LiteralPath $SourceRoot).Path
if(Test-Path -LiteralPath $Destination){throw 'Destination must be new'}
New-Item -ItemType Directory $Destination,"$Destination/Assets" | Out-Null
Copy-Item "$PSScriptRoot/*" $Destination
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/Components/AppSearchBox.xaml" $Destination
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/app.manifest" $Destination
Copy-Item "$SourceRoot/src/Desktop/BiliCopilot.UI/Assets/Logo/Square150x150Logo.scale-100.png" "$Destination/Assets/Logo.png"
@'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
<Identity Name="BiliCopilot.Acceptance51B48A" Publisher="CN=BiliCopilotDev" Version="1.0.0.0" />
<Properties><DisplayName>Offline search acceptance 51</DisplayName><PublisherDisplayName>Acceptance</PublisherDisplayName><Logo>Assets\Logo.png</Logo></Properties>
<Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" /></Dependencies><Resources><Resource Language="en-us" /></Resources>
<Applications><Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$"><uap:VisualElements DisplayName="Offline search acceptance 51" Description="Owned offline search harness" BackgroundColor="transparent" Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" /></Application></Applications><Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities></Package>
'@ | Set-Content "$Destination/Package.appxmanifest"
"<Project><PropertyGroup><SourceRoot>$([Security.SecurityElement]::Escape($SourceRoot))</SourceRoot></PropertyGroup></Project>" | Set-Content "$Destination/Directory.Build.props"
Get-FileHash "$Destination/AppSearchBox.xaml","$SourceRoot/src/Desktop/BiliCopilot.UI/Controls/Components/AppSearchBox.xaml" | ConvertTo-Json | Set-Content "$Destination/source-hashes.json"
