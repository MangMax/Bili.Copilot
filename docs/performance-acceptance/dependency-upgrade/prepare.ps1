param([Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]+$')][string]$RunId,[ValidateSet('player','controls')][string]$Kind='player',[switch]$UseCurrentPackages)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$old="$PSScriptRoot/../integration-pr27-pr43"
if($Kind -eq 'player') {
    & "$old/prepare-player.ps1" -Destination $Destination
    Copy-Item "$old/run-main.ps1" "$Destination/run-main.ps1"
    $before='Mang.BiliCopilot.Integration2743.Player'
    $after="Mang.BiliCopilot.DependencyUpgrade.$RunId"
} else {
    & "$old/prepare-harness.ps1" -Destination $Destination
    Copy-Item "$old/run-controls.ps1" "$Destination/run-controls.ps1"
    $before='BiliCopilot.Integration2743.Controls'
    $after="BiliCopilot.DependencyUpgrade.Controls.$RunId"
    $checks=Get-Content "$Destination/RuntimeChecks.cs" -Raw
    $checks=$checks.Replace('Require(first.SizeInPixels.Width==(uint)Math.Ceiling(240*scale),"Decode ignores physical width");', 'Trace.Write("cover-size",new {actualWidth=cover.ActualWidth,actualHeight=cover.ActualHeight,scale,width=first.SizeInPixels.Width,height=first.SizeInPixels.Height}); Require(scale==1.5,"Replay requires 150% DPI"); Require(first.SizeInPixels.Width==361,"Expected 361 physical pixels for the rounded 203px height and 16:9 source");')
    $checks=$checks.Replace('Require(first.SizeInPixels.Height==(uint)Math.Ceiling(135*scale),"Decode ignores physical height");', 'Require(first.SizeInPixels.Height==203,"Expected 203 physical pixels at 150% DPI");')
    Set-Content "$Destination/RuntimeChecks.cs" $checks
    $runner=Get-Content "$Destination/run-controls.ps1" -Raw
    $runner=$runner.Replace(" UI @('invoke','IntegrationChecks')", " UI @('wait-for','IntegrationChecks','-t','20000') | Out-Null`n UI @('invoke','IntegrationChecks')")
    Set-Content "$Destination/run-controls.ps1" $runner
    if($UseCurrentPackages) {
        $assets=Get-Content "$root/src/Desktop/BiliCopilot.UI/obj/project.assets.json" -Raw | ConvertFrom-Json
        $versions=@{}
        foreach($key in $assets.libraries.PSObject.Properties.Name) {$parts=$key.Split('/');$versions[$parts[0]]=$parts[1]}
        [xml]$project=Get-Content "$Destination/Harness.csproj"
        foreach($ref in $project.Project.ItemGroup.PackageReference) {
            if($ref -and $versions.ContainsKey($ref.Include)) {$ref.Version=$versions[$ref.Include]}
        }
        $project.Save("$Destination/Harness.csproj")
    }
}
foreach($file in Get-ChildItem $Destination -File | Where-Object Extension -in '.ps1','.appxmanifest','.cs') {
    $text=Get-Content $file.FullName -Raw
    $text=$text.Replace($before,$after).Replace('Integration2743.Offline',"DependencyUpgrade.$RunId").Replace("'integration2743'","'dependency-$RunId'").Replace("'integration2743-latest'","'dependency-$RunId'")
    # Relocated runner must resolve the actual checkout, not its temporary parent.
    $text=$text.Replace('$sourceRoot=(Resolve-Path "$PSScriptRoot/../../..").Path',"`$sourceRoot='$($root.Replace("'","''"))'")
    $text=$text.Replace('git -C $sourceRoot rev-parse HEAD | Set-Content "$Layout/source-provenance.txt"','Copy-Item "$PSScriptRoot/source-provenance.txt" "$Layout/source-provenance.txt" -Force')
    $text=$text.Replace('git -C $sourceRoot diff -- src | Add-Content "$Layout/source-provenance.txt"','')
    Set-Content $file.FullName $text
}
Copy-Item "$root/nuget.config" $Destination
git -C $root rev-parse HEAD | Set-Content "$Destination/source-provenance.txt"
git -C $root diff -- src | Add-Content "$Destination/source-provenance.txt"
Write-Output "Prepared $after at $Destination"
