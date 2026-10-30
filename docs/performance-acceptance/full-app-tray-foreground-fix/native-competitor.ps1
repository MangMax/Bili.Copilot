param([Parameter(Mandatory)][string]$Out,[Parameter(Mandatory)][long]$MainHwnd)
$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot/Competitor.cs"
[NativeCompetitor]::Run($Out,$MainHwnd)
