param([Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$oldDotnet=$env:DOTNET_TieredCompilation;$oldComPlus=$env:COMPlus_TieredCompilation
try {
    $env:DOTNET_TieredCompilation='0';$env:COMPlus_TieredCompilation='0'
    & "$PSScriptRoot/run-lcs.ps1" -Executable $Executable -Evidence $Evidence
    @{utc=[DateTime]::UtcNow.ToString('o');DOTNET_TieredCompilation=$env:DOTNET_TieredCompilation;
      COMPlus_TieredCompilation=$env:COMPlus_TieredCompilation;
      purpose='Same binary diagnostic; not production default; no forced GC';
      hashes=@(Get-FileHash $Executable,([IO.Path]::ChangeExtension($Executable,'.dll')))} | ConvertTo-Json -Depth 5 | Set-Content "$Evidence/config.json"
} finally {$env:DOTNET_TieredCompilation=$oldDotnet;$env:COMPlus_TieredCompilation=$oldComPlus}
