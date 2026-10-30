param([Parameter(Mandatory)][string]$Fixture,[Parameter(Mandatory)][string]$BuildOutput)
$ErrorActionPreference='Stop'
[xml]$manifest=Get-Content "$Fixture/Package.appxmanifest"
$identity=$manifest.Package.Identity.Name
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

if(Get-ChildItem $frozen -Recurse -Filter '*.appxrecipe'){throw 'Frozen output must not contain an appxrecipe'}
[xml]$frozenManifest=Get-Content "$frozen/AppxManifest.xml"
if($frozenManifest.Package.Identity.Name -ne $identity){throw 'Frozen manifest identity differs from owned fixture'}
Get-ChildItem $frozen -Recurse -File | ForEach-Object { @{path=[IO.Path]::GetRelativePath($frozen,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash} } | ConvertTo-Json | Set-Content "$Fixture/frozen-all-files.json"
