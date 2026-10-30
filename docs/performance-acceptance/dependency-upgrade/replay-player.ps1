param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,[string]$BuildOutput)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$Fixture=[IO.Path]::GetFullPath($Fixture)
$Layout=[IO.Path]::GetFullPath($Layout)
[xml]$manifest=Get-Content "$Fixture/Package.appxmanifest"
$identity=$manifest.Package.Identity.Name
if($identity -notlike 'Mang.BiliCopilot.DependencyUpgrade.*'){throw 'Not a task-owned identity'}
if(Get-AppxPackage -Name $identity){throw 'Registration already exists; inspect before replay'}
$evidence="$root/artifacts/dependency-upgrade"
if(Test-Path "$evidence/native-$Run-main"){throw 'Use a fresh evidence run name'}
if(!$BuildOutput){
    $outputs=@(Get-ChildItem "$Fixture/bin" -Recurse -Filter AppxManifest.xml | Where-Object { $_.FullName -like '*\Release\*' -and (Test-Path "$($_.Directory.FullName)/BiliCopilot.UI.exe") })
    if($outputs.Count -ne 1){throw 'Specify the exact frozen BuildOutput directory'}
    $BuildOutput=$outputs[0].Directory.FullName
}
# Even folder mode honors .build.appxrecipe, whose absolute source paths can
# inject today's reference outputs. Freeze the copied closure without recipes.
$frozen="$Fixture/frozen-output"
if(!(Test-Path $frozen)){
    New-Item -ItemType Directory $frozen | Out-Null
    Copy-Item "$BuildOutput/*" $frozen -Recurse -Exclude '*.appxrecipe'
}
# Some content (notably the generated silent WAV) exists only in the recipe.
# Copy missing packaged content once, preferring frozen build files over every
# absolute recipe source. Never let a recipe overwrite retained reference DLLs.
$extra=@()
$recipe=@(Get-ChildItem $BuildOutput -Filter '*.build.appxrecipe')
if($recipe.Count -ne 1){throw 'Expected one build recipe to complete packaged content'}
[xml]$mapping=Get-Content $recipe[0].FullName
$prefix=[IO.Path]::GetFullPath($frozen)+[IO.Path]::DirectorySeparatorChar
foreach($item in $mapping.SelectNodes("//*[local-name()='AppxPackagedFile']")){
    $relative=$item.PackagePath
    $destination=[IO.Path]::GetFullPath((Join-Path $frozen $relative))
    if(!$destination.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Recipe path escapes frozen directory'}
    if(!(Test-Path $destination)){
        $source=$item.GetAttribute('Include')
        New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
        $extra+=@{path=$relative;source=$source;sha256=(Get-FileHash $destination).Hash}
    }
}
if($extra.Count){$extra | ConvertTo-Json -Depth 5 | Set-Content "$Fixture/frozen-content-provenance.json"}
if(Test-Path "$Fixture/frozen-content-provenance.json"){Copy-Item "$Fixture/frozen-content-provenance.json" "$evidence/$Run-frozen-content.json"}
if(Get-ChildItem $frozen -Recurse -Filter '*.appxrecipe'){throw 'Frozen output must not contain an appxrecipe'}
[xml]$frozenManifest=Get-Content "$frozen/AppxManifest.xml"
if($frozenManifest.Package.Identity.Name -ne $identity){throw 'Frozen manifest identity differs from owned fixture'}
$launch=& winapp run $frozen --manifest "$frozen/AppxManifest.xml" --exe BiliCopilot.UI.exe --output-appx-directory $Layout --detach --json
$launch | Set-Content "$evidence/$Run-launch.json"
if($LASTEXITCODE -ne 0){throw 'Owned activation failed'}
$appPid=($launch -join "`n" | ConvertFrom-Json).ProcessId
if(!$appPid -or $appPid -eq 9608 -or (Get-Process -Id $appPid).Path -ne "$Layout\BiliCopilot.UI.exe"){throw 'Unexpected process'}
$layoutHash=(Get-FileHash "$Layout/BiliCopilot.UI.dll").Hash
$built=@(Get-ChildItem "$Fixture/bin" -Recurse -Filter BiliCopilot.UI.dll | Get-FileHash | Where-Object Hash -eq $layoutHash)
if(!$built.Count){throw 'Layout DLL does not match the prepared fixture build'}
$closure=@(foreach($file in Get-ChildItem $BuildOutput -Recurse -Filter '*.dll'){
    $relative=[IO.Path]::GetRelativePath($BuildOutput,$file.FullName)
    $original=(Get-FileHash $file.FullName).Hash
    $deployed=(Get-FileHash (Join-Path $Layout $relative)).Hash
    @{path=$relative;buildSha256=$original;deployedSha256=$deployed;match=$original -eq $deployed;productVersion=$file.VersionInfo.ProductVersion}
})
$closure | ConvertTo-Json -Depth 5 | Set-Content "$evidence/$Run-closure.json"
if(@($closure | Where-Object { !$_.match }).Count){throw 'Deployed dependency closure differs from frozen build'}
$files=Get-ChildItem $Layout -File | Where-Object { $_.Name -in 'BiliCopilot.UI.dll','BiliCopilot.UI.runtimeconfig.json','coreclr.dll','Microsoft.UI.Xaml.dll','Microsoft.Graphics.Canvas.dll','H.NotifyIcon.WinUI.dll' }
@{source=(Get-Content "$Fixture/source-provenance.txt" -Raw);pid=$appPid;identity=$identity;utc=[DateTime]::UtcNow.ToString('o');hashes=@($files | Get-FileHash);matchingBuilds=$built;runtime=(Get-Content "$Layout/BiliCopilot.UI.runtimeconfig.json" -Raw | ConvertFrom-Json)} | ConvertTo-Json -Depth 10 | Set-Content "$evidence/$Run-provenance.json"
& "$Fixture/run-ui.ps1" -AppPid $appPid -Layout $Layout -Evidence "$evidence/native-$Run-player" *> "$evidence/native-$Run-player.log"
if($LASTEXITCODE -ne 0){throw 'Player replay failed; preserve owned state for diagnosis'}
(Get-Process -Id $appPid).Modules | Where-Object ModuleName -in 'Microsoft.UI.Xaml.dll','Microsoft.Graphics.Canvas.dll','coreclr.dll','libmpv-2.dll','Microsoft.WindowsAppRuntime.dll' | ForEach-Object {
    @{module=$_.ModuleName;path=$_.FileName;fileVersion=$_.FileVersionInfo.FileVersion;productVersion=$_.FileVersionInfo.ProductVersion;sha256=(Get-FileHash $_.FileName).Hash}
} | ConvertTo-Json | Set-Content "$evidence/$Run-loaded-modules.json"
& "$Fixture/run-main.ps1" -AppPid $appPid -Layout $Layout -Evidence "$evidence/native-$Run-main" *> "$evidence/native-$Run-main.log"
if($LASTEXITCODE -ne 0){throw 'Main replay failed; preserve owned state for diagnosis'}
if(Get-Process -Id $appPid -ErrorAction SilentlyContinue){throw 'Owned process did not exit'}
$package=Get-AppxPackage -Name $identity
if($package.InstallLocation -ne $Layout){throw 'Owned layout changed'}
Remove-AppxPackage -Package $package.PackageFullName
@{pid=$appPid;exited=$true;identity=$identity;registrationRemoved=!(Get-AppxPackage -Name $identity)} | ConvertTo-Json | Set-Content "$evidence/$Run-cleanup.json"
Write-Output "Completed $Run; owned process exited and registration removed."
