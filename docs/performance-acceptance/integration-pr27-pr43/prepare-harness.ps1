param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
& "$root/docs/performance-acceptance/harness-50-53/prepare.ps1" -SourceRoot $root -Destination $Destination
python "$PSScriptRoot/prepare-harness.py" $root $Destination
if ($LASTEXITCODE -ne 0) { throw 'Harness preparation failed' }
