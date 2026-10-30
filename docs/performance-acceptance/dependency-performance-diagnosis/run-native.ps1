param([Parameter(Mandatory)][string]$Workspace,[ValidatePattern('^[A-Za-z0-9-]+$')][string]$Tag='g1',[ValidateSet('a','b','c')][string[]]$Arms=@('a','b','b','a'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Workspace)
$data="$root/artifacts/dependency-performance-diagnosis"
$index=0
foreach($arm in $Arms){
    $index++
    $run="d34-$Tag-$index-$arm"
    $fixture="$data/player-$arm"
    $layout="$data/layout-$run"
    if(Test-Path $layout){throw 'Fresh layout required'}
    @{run=$run;startUtc=[DateTime]::UtcNow.ToString('o');arm=$arm;
      power=(powercfg /getactivescheme);memory=(Get-CimInstance Win32_OperatingSystem | Select-Object FreePhysicalMemory);
      processors=[Environment]::ProcessorCount;order=$index} | ConvertTo-Json -Depth 4 | Set-Content "$data/$run-environment.json"
    & "$PSScriptRoot/replay-player.ps1" -Fixture $fixture -Layout $layout -Run $run
    @{run=$run;endUtc=[DateTime]::UtcNow.ToString('o');completed=$true} | ConvertTo-Json | Set-Content "$data/$run-complete.json"
}
