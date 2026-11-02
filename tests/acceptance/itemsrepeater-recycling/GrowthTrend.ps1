#Requires -Version 7.0
<#
.SYNOPSIS
    Pure trend/verdict helpers for the ItemsRepeater recycling acceptance run.

.DESCRIPTION
    Measure.ps1 samples memory/handle counters at cycle boundaries and then has
    to answer one question: does the series grow with the number of cycles, or
    is it flat? That arithmetic lives here, separate from Win32 process driving,
    so it can be unit tested (see GrowthTrend.Tests.ps1).

    No side effects, no process access, no I/O.
#>

Set-StrictMode -Version Latest

function Get-LeastSquaresFit {
    <#
    .SYNOPSIS
        Least squares fit of values against their sample index (0..n-1).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][double[]]$Values
    )

    $n = $Values.Count
    if ($n -eq 1) {
        return [pscustomobject]@{ Slope = 0.0; Intercept = $Values[0]; RSquared = 1.0 }
    }

    $meanX = ($n - 1) / 2.0
    $meanY = 0.0
    foreach ($v in $Values) { $meanY += $v }
    $meanY /= $n

    $sxx = 0.0
    $sxy = 0.0
    $syy = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $dx = $i - $meanX
        $dy = $Values[$i] - $meanY
        $sxx += $dx * $dx
        $sxy += $dx * $dy
        $syy += $dy * $dy
    }

    $slope = if ($sxx -gt 0) { $sxy / $sxx } else { 0.0 }
    $intercept = $meanY - ($slope * $meanX)
    # A constant series is a perfect fit of a zero-slope line.
    $rSquared = if ($syy -gt 0) { ($slope * $sxy) / $syy } else { 1.0 }

    return [pscustomobject]@{ Slope = $slope; Intercept = $intercept; RSquared = $rSquared }
}

function Measure-GrowthTrend {
    <#
    .SYNOPSIS
        Least squares trend of a series against its sample index.

    .PARAMETER Values
        Ordered samples (index 0 = first sample). Empty input returns $null.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [double[]]$Values
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return $null
    }

    $n = $Values.Count
    $fit = Get-LeastSquaresFit -Values $Values

    $window = [math]::Min($n, [math]::Max(3, [int][math]::Ceiling($n / 4.0)))
    $tailSlope = $fit.Slope
    if ($window -lt $n) {
        $tail = $Values[($n - $window)..($n - 1)]
        $tailSlope = (Get-LeastSquaresFit -Values $tail).Slope
    }

    return [pscustomobject]@{
        Count            = $n
        Slope            = $fit.Slope
        Intercept        = $fit.Intercept
        RSquared         = $fit.RSquared
        Start            = $Values[0]
        End              = $Values[$n - 1]
        Delta            = $Values[$n - 1] - $Values[0]
        LastQuarterSlope = $tailSlope
    }
}

function Get-RecyclingVerdict {
    <#
    .SYNOPSIS
        Turns cycle-boundary samples into a Growth / NoGrowth / Inconclusive verdict.

    .DESCRIPTION
        Rules, evaluated in order:
          1. fewer than 4 samples            -> Inconclusive (insufficient-samples)
          2. slope <= FlatSlope              -> NoGrowth   (flat)
          3. total growth < MinTotalGrowth   -> NoGrowth   (total-growth-below-threshold)
          4. R^2 < MinRSquared               -> Inconclusive (trend-not-monotonic-enough)
          5. tail slope < PlateauRatio*slope -> Inconclusive (saturating-trend)
          6. slope >= LeakSlope              -> Growth
          7. otherwise                       -> Inconclusive (slope-below-leak-threshold)

        Handle count never changes the memory verdict; it is reported separately
        because this defect is a native object leak, not a handle leak.

    .PARAMETER PrivateCommitMb
        Private commit (Get-Process PrivateMemorySize64 / 1MB) per cycle.

    .PARAMETER HandleCount
        Optional handle count per cycle.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [double[]]$PrivateCommitMb,

        [AllowEmptyCollection()]
        [double[]]$HandleCount = @(),

        [double]$FlatSlopeMbPerCycle = 2.0,
        [double]$LeakSlopeMbPerCycle = 8.0,
        [double]$MinRSquared = 0.8,
        [double]$MinTotalGrowthMb = 40.0,
        [double]$PlateauRatio = 0.5,
        [int]$MinimumSamples = 4
    )

    $trend = Measure-GrowthTrend -Values $PrivateCommitMb
    $handleTrend = if ($HandleCount.Count -gt 0) { Measure-GrowthTrend -Values $HandleCount } else { $null }

    $isPlateau = $false
    if ($null -ne $trend) {
        $isPlateau = ($trend.Slope -gt 0) -and ($trend.LastQuarterSlope -lt ($PlateauRatio * $trend.Slope))
    }

    $thresholds = [pscustomobject]@{
        flatSlopeMbPerCycle = $FlatSlopeMbPerCycle
        leakSlopeMbPerCycle = $LeakSlopeMbPerCycle
        minRSquared         = $MinRSquared
        minTotalGrowthMb    = $MinTotalGrowthMb
        plateauRatio        = $PlateauRatio
        minimumSamples      = $MinimumSamples
    }

    if ($null -eq $trend) {
        return [pscustomobject]@{
            Verdict     = 'Inconclusive'
            Reason      = 'no-samples'
            Message     = 'No cycle samples were recorded.'
            Trend       = $null
            HandleTrend = $handleTrend
            IsPlateau   = $false
            Thresholds  = $thresholds
        }
    }

    $verdict = 'Inconclusive'
    $reason = 'slope-below-leak-threshold'
    $message = ''

    if ($trend.Count -lt $MinimumSamples) {
        $reason = 'insufficient-samples'
        $message = "Only $($trend.Count) cycle sample(s); at least $MinimumSamples are required."
    }
    elseif ($trend.Slope -le $FlatSlopeMbPerCycle) {
        $verdict = 'NoGrowth'
        $reason = 'flat'
        $message = "Private commit slope $([math]::Round($trend.Slope, 2)) MB/cycle is at or below the flat threshold $FlatSlopeMbPerCycle MB/cycle."
    }
    elseif ($trend.Delta -lt $MinTotalGrowthMb) {
        $verdict = 'NoGrowth'
        $reason = 'total-growth-below-threshold'
        $message = "Total growth $([math]::Round($trend.Delta, 1)) MB is below the $MinTotalGrowthMb MB threshold."
    }
    elseif ($trend.RSquared -lt $MinRSquared) {
        $reason = 'trend-not-monotonic-enough'
        $message = "Slope $([math]::Round($trend.Slope, 2)) MB/cycle but R^2 $([math]::Round($trend.RSquared, 3)) is below $MinRSquared; the series is too noisy to call a trend."
    }
    elseif ($isPlateau) {
        $reason = 'saturating-trend'
        $message = "Growth saturates: heading slope $([math]::Round($trend.LastQuarterSlope, 2)) MB/cycle is below $([math]::Round($PlateauRatio * $trend.Slope, 2)) MB/cycle; this is warm-up, not a linear leak."
    }
    elseif ($trend.Slope -ge $LeakSlopeMbPerCycle) {
        $verdict = 'Growth'
        $reason = 'linear-growth'
        $message = "Private commit grows $([math]::Round($trend.Slope, 2)) MB/cycle (R^2 $([math]::Round($trend.RSquared, 3))), $([math]::Round($trend.Delta, 1)) MB over $($trend.Count) cycles."
    }
    else {
        $message = "Slope $([math]::Round($trend.Slope, 2)) MB/cycle is between the flat ($FlatSlopeMbPerCycle) and leak ($LeakSlopeMbPerCycle) thresholds."
    }

    return [pscustomobject]@{
        Verdict     = $verdict
        Reason      = $reason
        Message     = $message
        Trend       = $trend
        HandleTrend = $handleTrend
        IsPlateau   = $isPlateau
        Thresholds  = $thresholds
    }
}

function New-SummaryMarkdown {
    param([pscustomobject]$Summary)

    $trend = $Summary.verdict.Trend
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# ItemsRepeater recycling measurement - $($Summary.runId)")
    $lines.Add('')
    $lines.Add("**Verdict: $($Summary.verdict.Verdict)** ($($Summary.verdict.Reason))")
    $lines.Add('')
    $lines.Add($Summary.verdict.Message)
    $lines.Add('')
    $lines.Add('## Fixed inputs')
    $lines.Add('')
    $lines.Add('| item | value |')
    $lines.Add('| --- | --- |')
    $lines.Add("| package | $($Summary.package) |")
    $lines.Add("| package version | $($Summary.packageVersion) |")
    $lines.Add("| app file version | $($Summary.build.fileVersion) |")
    $lines.Add("| Microsoft.ui.xaml.dll | $($Summary.build.xamlDllVersion) |")
    $lines.Add("| Microsoft.WindowsAppRuntime.dll | $($Summary.build.appRuntimeVersion) |")
    $lines.Add("| repo commit | $($Summary.repository.commit) |")
    $lines.Add("| cycles (measured) | $($Summary.cycles) |")
    $lines.Add("| warmup cycles | $($Summary.warmupCycles) |")
    $lines.Add('')
    $lines.Add('## Cycle samples')
    $lines.Add('')
    $lines.Add('| cycle | private commit MB | private WS MB | handles |')
    $lines.Add('| --- | --- | --- | --- |')
    for ($i = 0; $i -lt $Summary.series.privateCommitMb.Count; $i++) {
        $ws = $Summary.series.workingSetPrivateMb[$i]
        $lines.Add("| $($i + 1) | $($Summary.series.privateCommitMb[$i]) | $ws | $($Summary.series.handleCount[$i]) |")
    }
    $lines.Add('')
    if ($null -ne $trend) {
        $lines.Add('## Trend')
        $lines.Add('')
        $lines.Add("Private commit: slope $([math]::Round($trend.Slope, 2)) MB/cycle, R^2 $([math]::Round($trend.RSquared, 3)), delta $([math]::Round($trend.Delta, 1)) MB over $($trend.Count) cycles.")
        if ($null -ne $Summary.verdict.HandleTrend) {
            $lines.Add("Handle count: slope $([math]::Round($Summary.verdict.HandleTrend.Slope, 2)) handles/cycle.")
        }
        $lines.Add('')
    }
    if ($Summary.notes.Count -gt 0) {
        $lines.Add('## Run notes')
        $lines.Add('')
        foreach ($note in $Summary.notes) { $lines.Add("- $note") }
        $lines.Add('')
    }
    $lines.Add('## Artifacts')
    $lines.Add('')
    $lines.Add("Raw data: $($Summary.artifacts.runDirectory)\raw.json")
    $lines.Add("Screenshots: $($Summary.artifacts.screenshots.Count)")
    $lines.Add('')
    $lines.Add('See README.md for what this measurement does and does not prove.')
    return ($lines -join [Environment]::NewLine)
}
