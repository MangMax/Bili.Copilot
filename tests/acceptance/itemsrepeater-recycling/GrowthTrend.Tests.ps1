#Requires -Version 7.0
<#
.SYNOPSIS
    Behaviour tests for the growth/trend summary used by Measure.ps1.

.DESCRIPTION
    Dependency-free test harness (the repo has no Pester module wired up, and
    Pester 3.4 that ships with Windows PowerShell does not run on pwsh 7).
    Run with:  pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/GrowthTrend.Tests.ps1
    Exit code 0 = all assertions passed, 1 = at least one failure.

    Expected values below are hand-computed (least squares by definition), not
    produced by the implementation, so the tests can disagree with the code.
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'GrowthTrend.ps1')

$script:Failures = [System.Collections.Generic.List[string]]::new()
$script:Assertions = 0

function Test-CloseTo {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$Expected,
        [Parameter(Mandatory)][AllowNull()][object]$Actual,
        [double]$Tolerance = 1e-4
    )

    $script:Assertions++
    if ($null -eq $Actual) {
        $script:Failures.Add("$Name : expected $Expected but got <null>")
        Write-Host "FAIL  $Name (expected $Expected, got <null>)"
        return
    }

    if ([math]::Abs([double]$Actual - $Expected) -gt $Tolerance) {
        $script:Failures.Add("$Name : expected $Expected but got $Actual")
        Write-Host "FAIL  $Name (expected $Expected, got $Actual)"
        return
    }

    Write-Host "PASS  $Name"
}

function Test-Value {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][AllowNull()][object]$Expected,
        [Parameter(Mandatory)][AllowNull()][object]$Actual
    )

    $script:Assertions++
    if ("$Expected" -ne "$Actual") {
        $script:Failures.Add("$Name : expected $Expected but got $Actual")
        Write-Host "FAIL  $Name (expected '$Expected', got '$Actual')"
        return
    }

    Write-Host "PASS  $Name"
}

# --- Measure-GrowthTrend -----------------------------------------------------

$empty = Measure-GrowthTrend -Values @()
Test-Value -Name 'empty series returns null' -Expected $null -Actual $empty

$line = Measure-GrowthTrend -Values @(100, 105, 110, 115, 120)
Test-CloseTo -Name 'line slope' -Expected 5 -Actual $line.Slope
Test-CloseTo -Name 'line intercept' -Expected 100 -Actual $line.Intercept
Test-CloseTo -Name 'line r2' -Expected 1 -Actual $line.RSquared
Test-CloseTo -Name 'line delta' -Expected 20 -Actual $line.Delta
Test-CloseTo -Name 'line last quarter slope' -Expected 5 -Actual $line.LastQuarterSlope

$steep = Measure-GrowthTrend -Values @(100, 110, 120, 130, 140, 150)
Test-CloseTo -Name 'steep slope' -Expected 10 -Actual $steep.Slope
Test-CloseTo -Name 'steep r2' -Expected 1 -Actual $steep.RSquared

$noisy = Measure-GrowthTrend -Values @(100, 160, 120, 180, 140, 200)
Test-CloseTo -Name 'noisy slope' -Expected 14.285714 -Actual $noisy.Slope
Test-CloseTo -Name 'noisy r2' -Expected 0.510204 -Actual $noisy.RSquared

$flat = Measure-GrowthTrend -Values @(100, 101, 100, 101, 100, 100)
Test-CloseTo -Name 'flat slope' -Expected -0.057143 -Actual $flat.Slope

$rising = Measure-GrowthTrend -Values @(0, 0, 0, 0, 10, 20, 30, 40)
Test-CloseTo -Name 'late rise last quarter slope' -Expected 10 -Actual $rising.LastQuarterSlope

# --- Get-RecyclingVerdict ----------------------------------------------------

$insufficient = Get-RecyclingVerdict -PrivateCommitMb @(100, 110, 120)
Test-Value -Name 'three samples are inconclusive' -Expected 'Inconclusive' -Actual $insufficient.Verdict
Test-Value -Name 'three samples say why' -Expected 'insufficient-samples' -Actual $insufficient.Reason
Test-Value -Name 'three samples still expose the slope' -Expected $true -Actual ($insufficient.Trend.Slope -ne $null)

$growing = Get-RecyclingVerdict -PrivateCommitMb @(100, 110, 120, 130, 140, 150)
Test-Value -Name 'steady growth is Growth' -Expected 'Growth' -Actual $growing.Verdict
Test-CloseTo -Name 'growth slope reported' -Expected 10 -Actual $growing.Trend.Slope
Test-Value -Name 'growth is not a plateau' -Expected $false -Actual $growing.IsPlateau

$stable = Get-RecyclingVerdict -PrivateCommitMb @(100, 101, 100, 101, 100, 100)
Test-Value -Name 'flat series is NoGrowth' -Expected 'NoGrowth' -Actual $stable.Verdict

$noisyVerdict = Get-RecyclingVerdict -PrivateCommitMb @(100, 160, 120, 180, 140, 200)
Test-Value -Name 'noisy series is Inconclusive' -Expected 'Inconclusive' -Actual $noisyVerdict.Verdict
Test-Value -Name 'noisy series reason' -Expected 'trend-not-monotonic-enough' -Actual $noisyVerdict.Reason

$small = Get-RecyclingVerdict -PrivateCommitMb @(100, 103, 106, 109, 112, 115)
Test-Value -Name 'small steady creep is NoGrowth' -Expected 'NoGrowth' -Actual $small.Verdict

$slowRise = Get-RecyclingVerdict -PrivateCommitMb @(100, 105, 110, 115, 120, 125, 130, 135, 140, 145)
Test-Value -Name 'mid slope between thresholds is Inconclusive' -Expected 'Inconclusive' -Actual $slowRise.Verdict
Test-Value -Name 'mid slope reason' -Expected 'slope-below-leak-threshold' -Actual $slowRise.Reason

$handles = Get-RecyclingVerdict -PrivateCommitMb @(100, 101, 100, 101, 100, 100) -HandleCount @(900, 950, 1000, 1050, 1100, 1150)
Test-Value -Name 'memory verdict ignores handles' -Expected 'NoGrowth' -Actual $handles.Verdict
Test-CloseTo -Name 'handle slope reported' -Expected 50 -Actual $handles.HandleTrend.Slope

$plateau = Get-RecyclingVerdict -PrivateCommitMb @(100, 140, 180, 220, 222, 223, 224, 225)
Test-Value -Name 'saturating series is a plateau' -Expected $true -Actual $plateau.IsPlateau
Test-Value -Name 'saturating series is not Growth' -Expected 'Inconclusive' -Actual $plateau.Verdict


# --- New-SummaryMarkdown -----------------------------------------------------

$summaryFixture = [pscustomobject]@{
    runId          = '20260101-000000'
    package        = 'Mang.BiliCopilot.Dev_1.0.0.0_x64__abc'
    packageVersion = '1.0.0.0'
    build          = [pscustomobject]@{
        fileVersion       = '1.0.0.0'
        xamlDllVersion    = $null
        appRuntimeVersion = $null
    }
    repository     = [pscustomobject]@{ commit = 'deadbeef'; branch = 'main'; dirty = $false }
    cycles         = 2
    warmupCycles   = 1
    verdict        = [pscustomobject]@{
        Verdict     = 'Growth'
        Reason      = 'linear-growth'
        Message     = 'Private commit grows 10 MB/cycle.'
        Trend       = [pscustomobject]@{ Slope = 10.0; RSquared = 1.0; Delta = 10.0; Count = 2 }
        HandleTrend = $null
    }
    series         = [pscustomobject]@{
        privateCommitMb     = [double[]]@(100, 110)
        handleCount         = [double[]]@(900, 950)
        workingSetPrivateMb = [double[]]@(80, 88)
    }
    exit           = [pscustomobject]@{ exited = $true; exitCode = 0 }
    notes          = @('a note')
    artifacts      = [pscustomobject]@{ runDirectory = 'C:\runs\1'; screenshots = @('a.png') }
}

$markdown = New-SummaryMarkdown -Summary $summaryFixture
Test-Value -Name 'summary is a single string' -Expected $true -Actual ($markdown -is [string])
Test-Value -Name 'summary names the verdict' -Expected $true -Actual ($markdown.Contains('**Verdict: Growth** (linear-growth)'))
Test-Value -Name 'summary lists package version' -Expected $true -Actual ($markdown.Contains('| package version | 1.0.0.0 |'))
Test-Value -Name 'summary renders each cycle row' -Expected $true -Actual ($markdown.Contains('| 2 | 110 | 88 | 950 |'))
Test-Value -Name 'summary reports the slope' -Expected $true -Actual ($markdown.Contains('slope 10 MB/cycle'))
Test-Value -Name 'summary carries run notes' -Expected $true -Actual ($markdown.Contains('- a note'))
Test-Value -Name 'summary points at raw json' -Expected $true -Actual ($markdown.Contains('C:\runs\1\raw.json'))

# --- Summary -----------------------------------------------------------------


Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host "$($script:Failures.Count) of $($script:Assertions) assertions failed." -ForegroundColor Red
    foreach ($failure in $script:Failures) { Write-Host "  - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host "All $($script:Assertions) assertions passed." -ForegroundColor Green
exit 0