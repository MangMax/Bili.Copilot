param([Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$RunId,
      [Parameter(Mandatory)][string]$Assets)
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../dependency-upgrade/prepare.ps1" -Destination $Destination -RunId $RunId -Kind controls
$data=Get-Content $Assets -Raw | ConvertFrom-Json
$versions=@{}
foreach($key in $data.libraries.PSObject.Properties.Name){$parts=$key.Split('/');$versions[$parts[0]]=$parts[1]}
[xml]$project=Get-Content "$Destination/Harness.csproj"
foreach($ref in $project.Project.ItemGroup.PackageReference){
    if($ref -and $versions.ContainsKey($ref.Include)){$ref.Version=$versions[$ref.Include]}
}
$group=$project.CreateElement('ItemGroup')
foreach($name in 'Microsoft.Graphics.Win2D','Microsoft.Windows.CsWinRT','CommunityToolkit.WinUI.Extensions','CommunityToolkit.WinUI.Media','WinUIEx'){
    if(!$versions.ContainsKey($name)){throw "Missing $name"}
    $ref=$project.CreateElement('PackageReference');$ref.SetAttribute('Include',$name);$ref.SetAttribute('Version',$versions[$name]);[void]$group.AppendChild($ref)
}
[void]$project.Project.AppendChild($group)
$property=$project.CreateElement('CsWinRTGenerateProjection');$property.InnerText='false';[void]$project.Project.PropertyGroup.AppendChild($property)
$project.Save("$Destination/Harness.csproj")
