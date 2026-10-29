$ErrorActionPreference = 'Stop'
function Median($Values) {
    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
$rows = @(Import-Csv (Join-Path $PSScriptRoot 'evidence/run-2/measurements.csv')) +
        @(Import-Csv (Join-Path $PSScriptRoot 'evidence/run-3/measurements.csv'))
if ($rows.Count -ne 112) { throw "Expected 112 formal batches, got $($rows.Count)" }
$summary = foreach ($group in ($rows | Group-Object fixture,variant)) {
    $r = $group.Group
    $cpu = @($r | ForEach-Object { [double]$_.cpu_ms * 1000 / [int]$_.iterations })
    $wall = @($r | ForEach-Object { [double]$_.wall_ms * 1000 / [int]$_.iterations })
    $alloc = @($r | ForEach-Object { [double]$_.allocated_bytes / [int]$_.iterations })
    [pscustomobject]@{
        fixture=$r[0].fixture; variant=$r[0].variant; batches=$r.Count; payload_bytes=$r[0].bytes
        cpu_us_median=[Math]::Round((Median $cpu),3)
        cpu_us_min=[Math]::Round(($cpu | Measure-Object -Minimum).Minimum,3)
        cpu_us_max=[Math]::Round(($cpu | Measure-Object -Maximum).Maximum,3)
        wall_us_median=[Math]::Round((Median $wall),3)
        bytes_per_op_median=[Math]::Round((Median $alloc),1)
        bytes_per_op_min=[Math]::Round(($alloc | Measure-Object -Minimum).Minimum,1)
        bytes_per_op_max=[Math]::Round(($alloc | Measure-Object -Maximum).Maximum,1)
    }
}
$summary | Export-Csv (Join-Path $PSScriptRoot 'evidence/summary.csv') -NoTypeInformation
$summary | Format-Table -AutoSize
$errors = Import-Csv (Join-Path $PSScriptRoot 'evidence/errors.csv')
if ($errors.Count -ne 42) { throw 'Expected 42 error batches' }
$errorSummary = foreach ($group in ($errors | Group-Object case)) {
    $r = $group.Group
    [pscustomobject]@{
        case=$group.Name
        cpu_us_median=[Math]::Round((Median @($r | ForEach-Object { [double]$_.cpu_ms * 1000 / [int]$_.iterations })),3)
        bytes_per_op_median=[Math]::Round((Median @($r | ForEach-Object { [double]$_.allocated_bytes / [int]$_.iterations })),1)
    }
}
$errorSummary | Export-Csv (Join-Path $PSScriptRoot 'evidence/errors-summary.csv') -NoTypeInformation
$errorSummary | Format-Table -AutoSize
