param([Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]+$')][string]$RunId,[ValidateSet('player','controls')][string]$Kind='player',[switch]$UseCurrentPackages)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$old="$PSScriptRoot/../integration-pr27-pr43"
if($RunId.Length -gt 8 -and $Kind -eq 'controls'){throw 'Controls RunId must be at most 8 characters for the MSIX identity limit'}
if($Kind -eq 'player') {
    & "$old/prepare-player.ps1" -Destination $Destination
    Copy-Item "$old/run-main.ps1" "$Destination/run-main.ps1"
    Copy-Item "$PSScriptRoot/FontRuntimeChecks.cs" "$Destination/FontRuntimeChecks.cs"
    $main=Get-Content "$Destination/MainRuntimeChecks.cs" -Raw
    # Only replace the standalone post-sampling open, not the earlier assigned player.
    $main=$main.Replace('        await Offline48.Open();','        await FontRuntimeChecks.Run(await Offline48.Open());')
    Set-Content "$Destination/MainRuntimeChecks.cs" $main
    $runner=Get-Content "$Destination/run-main.ps1" -Raw
    $runner=$runner.Replace('    & winapp ui invoke ExitMain', @'
    Copy-Item "$state/font-results.json" $Evidence -Force
    $font=Get-Content "$state/font-results.json" -Raw | ConvertFrom-Json
    & winapp ui screenshot -w $font.hwnd --on local -a $AppPid --output "$Evidence/font-settings.png" --json
    if($font.status -ne 'PASS'){throw 'First subtitle font settings check failed'}
    & winapp ui invoke ExitMain
'@)
    Set-Content "$Destination/run-main.ps1" $runner
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
        # These are transitive in the old minimal fixture. Pin the actual app
        # closure explicitly so the graphics replay cannot silently test Win2D 1.3.
        $group=$project.CreateElement('ItemGroup')
        foreach($name in 'Microsoft.Graphics.Win2D','Microsoft.Windows.CsWinRT','CommunityToolkit.WinUI.Extensions','CommunityToolkit.WinUI.Media','WinUIEx') {
            if(!$versions.ContainsKey($name)){throw "Missing production dependency $name"}
            $ref=$project.CreateElement('PackageReference');$ref.SetAttribute('Include',$name);$ref.SetAttribute('Version',$versions[$name]);[void]$group.AppendChild($ref)
        }
        [void]$project.Project.AppendChild($group)
        $property=$project.CreateElement('CsWinRTGenerateProjection');$property.InnerText='false';[void]$project.Project.PropertyGroup.AppendChild($property)
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
    # First activation after a new Windows App SDK registration can exceed 20s.
    # This is readiness only, outside the measured Player -> Main sequence.
    $text=$text.Replace("'--value','READY','-t','20000'", "'--value','READY','-t','120000'")
    Set-Content $file.FullName $text
}
Copy-Item "$root/nuget.config" $Destination
git -C $root rev-parse HEAD | Set-Content "$Destination/source-provenance.txt"
git -C $root diff -- src | Add-Content "$Destination/source-provenance.txt"
if($Kind -eq 'player'){& "$PSScriptRoot/harden-runner.ps1" -Path "$Destination/run-ui.ps1"}
Write-Output "Prepared $after at $Destination"
