param([Parameter(Mandatory)][string]$Fixture,[string]$Executable='BiliCopilot.UI.exe')
$ErrorActionPreference='Stop'
$outputs=@(Get-ChildItem "$Fixture/bin" -Recurse -Filter AppxManifest.xml | Where-Object { $_.FullName -like '*\Release\*' -and (Test-Path "$($_.Directory.FullName)/$Executable") })
if($outputs.Count -ne 1){throw 'Ambiguous build output'}
$build=$outputs[0].Directory.FullName
$frozen=Join-Path $Fixture 'frozen-output'
if(Test-Path $frozen){throw 'Do not overwrite frozen output'}
New-Item -ItemType Directory $frozen | Out-Null
Copy-Item "$build/*" $frozen -Recurse -Exclude '*.appxrecipe'
$recipes=@(Get-ChildItem $build -Filter '*.build.appxrecipe')
if($recipes.Count -ne 1){throw 'Missing unique recipe'}
[xml]$recipe=Get-Content $recipes[0].FullName
$prefix=[IO.Path]::GetFullPath($frozen)+[IO.Path]::DirectorySeparatorChar
$extra=@(foreach($item in $recipe.SelectNodes("//*[local-name()='AppxPackagedFile']")){
    $target=[IO.Path]::GetFullPath((Join-Path $frozen $item.PackagePath))
    if(!$target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escape'}
    if(!(Test-Path $target)){
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $item.GetAttribute('Include') -Destination $target
        @{path=$item.PackagePath;source=$item.GetAttribute('Include');sha256=(Get-FileHash $target).Hash}
    }
})
$extra | ConvertTo-Json -Depth 5 | Set-Content "$Fixture/frozen-content-provenance.json"
if(Get-ChildItem $frozen -Recurse -Filter '*.appxrecipe'){throw 'Recipe remained'}
@(Get-ChildItem $frozen -Recurse -File | ForEach-Object {
    @{path=[IO.Path]::GetRelativePath($frozen,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash;bytes=$_.Length}
}) | ConvertTo-Json -Depth 5 | Set-Content "$Fixture/frozen-all-files.json"
$build | Set-Content "$Fixture/build-output.txt"
Write-Output "Frozen $build"
