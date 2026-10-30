param([string]$Destination)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
if(!$Destination){$Destination=Join-Path $root 'artifacts/dependency-upgrade/sdk11'}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path $Destination){throw 'Use a fresh isolated destination; existing SDK will not be overwritten'}
$version='11.0.100-rc.3.26522.148'
$expected='dae0a0450d92b5315034db134259acffe9ec0043d93dced4fb2a9b8700520e87a99005d6845ad80716f4a8f30df24ae5363ca7c9be391797b4f53170e9b2c088'
$metadata=Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json'
$sdk=@($metadata.releases | ForEach-Object {$_.sdks} | Where-Object version -eq $version)[0]
if(!$sdk){$sdk=@($metadata.releases.sdk | Where-Object version -eq $version)[0]}
$file=@($sdk.files | Where-Object {$_.rid -eq 'win-x64' -and $_.name -like '*.zip'})[0]
if(!$file -or $file.hash -ne $expected){throw 'Official metadata differs from reviewed SDK checksum'}
$archive="$Destination.zip"
New-Item -ItemType Directory -Force (Split-Path $Destination) | Out-Null
Invoke-WebRequest $file.url -OutFile $archive
if((Get-FileHash $archive -Algorithm SHA512).Hash -ne $expected){throw 'SDK archive checksum mismatch'}
Expand-Archive -LiteralPath $archive -DestinationPath $Destination
$validation=Join-Path $Destination '.validation'
New-Item -ItemType Directory $validation | Out-Null
@{sdk=@{version=$version;rollForward='disable';allowPrerelease=$true}} | ConvertTo-Json | Set-Content "$validation/global.json"
Push-Location $validation
try {
    $actual=& "$Destination/dotnet.exe" --version
    if($LASTEXITCODE -ne 0 -or $actual -ne $version){throw 'Extracted SDK version differs from pinned metadata'}
    & "$Destination/dotnet.exe" --info
    if($LASTEXITCODE -ne 0){throw 'Extracted SDK did not execute'}
} finally {Pop-Location}
# Caller sets DOTNET_ROOT/PATH only in its process and selects the candidate global.json.
