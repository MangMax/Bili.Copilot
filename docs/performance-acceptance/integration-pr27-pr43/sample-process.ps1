param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$OwnedExecutable,
    [Parameter(Mandatory)][string]$OutputCsv, [int]$Seconds = 60)
$ErrorActionPreference = 'Stop'
$expected = [IO.Path]::GetFullPath($OwnedExecutable)
$rows = for ($i = 0; $i -le $Seconds; $i++) {
    $p = Get-Process -Id $AppPid
    if ($p.Path -ne $expected) { throw 'Process ownership mismatch' }
    [pscustomobject]@{ Utc = [DateTime]::UtcNow.ToString('o'); Pid = $AppPid;
        CpuMilliseconds = $p.TotalProcessorTime.TotalMilliseconds; PrivateBytes = $p.PrivateMemorySize64;
        WorkingSet = $p.WorkingSet64; Handles = $p.HandleCount; Threads = $p.Threads.Count }
    if ($i -lt $Seconds) { Start-Sleep -Seconds 1 }
}
$rows | Export-Csv -NoTypeInformation $OutputCsv
# Run only for the owned isolated app: same idle page, notifications disabled before restart,
# no playback/download; record visible/hidden state separately. CPU is a delta over UTC time.
# Working set is residency, not retained allocations. Test duration is not UI performance.
