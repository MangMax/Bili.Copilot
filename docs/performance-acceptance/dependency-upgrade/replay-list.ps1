param([Parameter(Mandatory)][string]$BaselineExecutable,[Parameter(Mandatory)][string]$UpgradedExecutable,[Parameter(Mandatory)][string]$Net11Executable,[ValidatePattern('^[A-Za-z0-9-]+$')][string]$Tag='repeat')
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$out="$root/artifacts/dependency-upgrade"
$executables=@{base=$BaselineExecutable;up=$UpgradedExecutable;net11=$Net11Executable}
$counts=@{base=0;up=0;net11=0}
$ledger=@()
# Three independent processes per variant, fixed interleaving; each process runs
# the unchanged 3-warmup / 10-measurement workload at all three list sizes.
foreach($variant in 'base','up','net11','net11','base','up','up','net11','base') {
    $counts[$variant]++
    $result="$out/list-$variant-$Tag$($counts[$variant]).json"
    if(Test-Path $result){throw "Refusing to overwrite $result"}
    $exe=(Resolve-Path $executables[$variant]).Path
    $start=[DateTime]::UtcNow.ToString('o')
    & $exe $result
    if($LASTEXITCODE -ne 0){throw "Benchmark process failed: $variant"}
    $dir=Split-Path $exe
    $ledger+=@{variant=$variant;run=$counts[$variant];startedUtc=$start;result=$result;hashes=@(Get-FileHash "$dir/Measure.dll","$dir/Measure.runtimeconfig.json","$dir/coreclr.dll")}
}
$ledger | ConvertTo-Json -Depth 8 | Set-Content "$out/list-replay-$Tag-ledger.json"
