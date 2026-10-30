param([Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$RunId,
    [ValidateSet('candidate','win2d-only-red','win2d-icons')][string]$Variant='candidate')
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../dependency-upgrade/prepare.ps1" -Destination $Destination -RunId $RunId
$runner=Join-Path $Destination 'run-ui.ps1'
(Get-Content $runner -Raw).Replace("'--capture-screen',",'') | Set-Content $runner
$path=Join-Path $Destination 'Directory.Packages.props'
$text=Get-Content $path -Raw
if($Variant -ne 'candidate'){
    $old='<PackageVersion Include="Microsoft.Graphics.Win2D" Version="1.4.0" />'
    if(!$text.Contains($old)){throw 'Unexpected source Win2D'}
    $text=$text.Replace($old,'<PackageVersion Include="Microsoft.Graphics.Win2D" Version="1.3.2" />')
}
if($Variant -eq 'win2d-icons'){
    $old='<PackageVersion Include="FluentIcons.WinUI" Version="2.2.344" />'
    if(!$text.Contains($old)){throw 'Unexpected source FluentIcons'}
    $text=$text.Replace($old,'<PackageVersion Include="FluentIcons.WinUI" Version="1.2.315" />')
}
Set-Content $path $text
Get-FileHash $path | ConvertTo-Json | Set-Content "$Destination/package-props-hash.json"
