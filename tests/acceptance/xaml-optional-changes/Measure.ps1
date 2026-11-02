#requires -Version 7.0
<#
.SYNOPSIS
  XAML 可选开关 A/B 测量夹具（见同目录 README.md）。

.DESCRIPTION
  对 baseline 与四个 XAML 可选开关分别构建 Release(JIT) 产品包，打包为 Dev 身份后安装，
  交错运行固定次数，记录启动、私有工作集、CPU、空闲 CPU、唤回延迟与窗口截图。
  原始数据写入 artifacts/xaml-optional-changes/，不进入版本库。

.PARAMETER SelfTest
  只运行汇总统计的自检（中位数/MAD/噪声判定），不构建也不测量。
#>
[CmdletBinding()]
param(
    [string[]]$Variants = @(
        'baseline',
        'DefaultStyleOptimizations',
        'IconNoGridOptimization',
        'OptimizeApplyStyles',
        'DeferContextFlyoutInit'),

    [ValidateRange(1, 50)][int]$Runs = 7,

    [ValidateRange(1, 120)][int]$SettleSeconds = 10,

    [ValidateRange(1, 120)][int]$IdleSeconds = 5,

    [ValidateRange(1, 120)][int]$WarmupSeconds = 8,

    [string]$ArtifactsRoot,

    [switch]$SkipProbe,

    [switch]$SelfTest,

    # 不重新测量，只从 artifacts 里的 raw/probe/screenshot 重算 summary（例如修正截图取帧后）。
    [switch]$RegenerateSummary
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$script:PackageName = 'Mang.BiliCopilot.Dev'
$script:ForbiddenPackages = @('Mang.BiliCopilot', 'Mang.BiliCopilot.AotDev')
$script:DevCertificate = Join-Path $script:RepoRoot 'src\Desktop\BiliCopilot.UI\DevSigning.pfx'
$script:MakeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\makeappx.exe'
$script:SignTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe'
$script:Layout = Join-Path $script:RepoRoot 'src\_build\x64\Release\BiliCopilot.UI\bin'
$script:ProbeProject = Join-Path $PSScriptRoot 'Probe\XamlOptionalChangesProbe.csproj'
$script:ProbeExe = Join-Path $PSScriptRoot 'Probe\bin\x64\Release\net11.0-windows10.0.22621.0\win-x64\XamlOptionalChangesProbe.exe'
$script:Artifacts = if ($ArtifactsRoot) { [IO.Path]::GetFullPath($ArtifactsRoot) } else { Join-Path $script:RepoRoot 'artifacts\xaml-optional-changes' }
$script:VersionCounter = 100
# Window discovery and wake detection poll at this interval. Reported latencies
# are rounded to it, and it enters the noise floor: a delta smaller than the
# instrument resolution cannot be reported as an effect.
$script:PollIntervalMs = 5
$script:InstalledVariant = $null
$script:InstalledExe = $null
$script:ProductionSnapshot = @()
$script:RunRecords = [Collections.Generic.List[object]]::new()
$script:ProbeResults = @{}
$script:ProcedureNotes = [Collections.Generic.List[string]]::new()

$script:SwitchOrder = @(
    'baseline',
    'DefaultStyleOptimizations',
    'IconNoGridOptimization',
    'OptimizeApplyStyles',
    'DeferContextFlyoutInit',
    'all')

# The exact switches each variant must put into the generated entry point.
# 'all' is a diagnostic combination (see README), not one of the four verdicts.
$script:VariantSwitches = @{
    baseline                  = @()
    DefaultStyleOptimizations = @('DefaultStyleOptimizations')
    IconNoGridOptimization    = @('IconNoGridOptimization')
    OptimizeApplyStyles       = @('OptimizeApplyStyles')
    DeferContextFlyoutInit    = @('DeferContextFlyoutInit')
    all                       = @('DefaultStyleOptimizations', 'DeferContextFlyoutInit', 'IconNoGridOptimization', 'OptimizeApplyStyles')
}

$script:ProbeExpectations = @{
    baseline = @(
        'before.IsLocked=false',
        'inside.IsLocked=true',
        'before.DefaultStyleOptimizations=false',
        'before.IconNoGridOptimization=false',
        'before.OptimizeApplyStyles=false',
        'before.DeferContextFlyoutInit=false',
        'inside.DefaultStyleOptimizations=false',
        'inside.IconNoGridOptimization=false',
        'inside.OptimizeApplyStyles=false',
        'inside.DeferContextFlyoutInit=false',
        'fontIconShape=Grid',
        'symbolIconShape=Grid',
        'xamlButtonTemplateBeforeTree=true',
        'xamlButtonTemplateAfterTree=true',
        'enableErrorCount=0',
        'windowWidth=1200',
        'windowHeight=800',
        'screenshotMain=ok',
        'menuPopupCaptured=ok')

    DefaultStyleOptimizations = @(
        'before.IsLocked=false',
        'inside.IsLocked=true',
        'before.DefaultStyleOptimizations=false',
        'inside.DefaultStyleOptimizations=true',
        'inside.IconNoGridOptimization=false',
        'inside.OptimizeApplyStyles=false',
        'inside.DeferContextFlyoutInit=false',
        'fontIconShape=Grid',
        'xamlButtonTemplateBeforeTree=true',
        'enableErrorCount=0',
        'screenshotMain=ok',
        # Default text flyouts are not exposed by the ContextFlyout getter on 2.6.4;
        # the probe records the attempt as not observable rather than claiming cover.
        'textBlockFlyoutCaptured=not-observable',
        'richTextFlyoutCaptured=not-observable')

    IconNoGridOptimization = @(
        'before.IsLocked=false',
        'inside.IsLocked=true',
        'inside.IconNoGridOptimization=true',
        'inside.DefaultStyleOptimizations=false',
        'inside.OptimizeApplyStyles=false',
        'inside.DeferContextFlyoutInit=false',
        'fontIconShape=TextBlock',
        'symbolIconShape=Grid',
        'xamlButtonTemplateBeforeTree=true',
        'enableErrorCount=0',
        'screenshotMain=ok',
        # Default text flyouts are not exposed by the ContextFlyout getter on 2.6.4;
        # the probe records the attempt as not observable rather than claiming cover.
        'textBlockFlyoutCaptured=not-observable',
        'richTextFlyoutCaptured=not-observable')

    OptimizeApplyStyles = @(
        'before.IsLocked=false',
        'inside.IsLocked=true',
        'inside.OptimizeApplyStyles=true',
        'inside.DefaultStyleOptimizations=false',
        'inside.IconNoGridOptimization=false',
        'inside.DeferContextFlyoutInit=false',
        'fontIconShape=Grid',
        'xamlButtonTemplateBeforeTree=false',
        'xamlButtonTemplateAfterTree=true',
        'enableErrorCount=0',
        'screenshotMain=ok',
        # Default text flyouts are not exposed by the ContextFlyout getter on 2.6.4;
        # the probe records the attempt as not observable rather than claiming cover.
        'textBlockFlyoutCaptured=not-observable',
        'richTextFlyoutCaptured=not-observable')

    DeferContextFlyoutInit = @(
        'before.IsLocked=false',
        'inside.IsLocked=true',
        'inside.DeferContextFlyoutInit=true',
        'inside.DefaultStyleOptimizations=false',
        'inside.IconNoGridOptimization=false',
        'inside.OptimizeApplyStyles=false',
        'fontIconShape=Grid',
        'xamlButtonTemplateBeforeTree=true',
        'enableErrorCount=0',
        'screenshotMain=ok',
        # Default text flyouts are not exposed by the ContextFlyout getter on 2.6.4;
        # the probe records the attempt as not observable rather than claiming cover.
        'textBlockFlyoutCaptured=not-observable',
        'richTextFlyoutCaptured=not-observable')

    all = @(
        'inside.IsLocked=true',
        'inside.DefaultStyleOptimizations=true',
        'inside.IconNoGridOptimization=true',
        'inside.OptimizeApplyStyles=true',
        'inside.DeferContextFlyoutInit=true',
        'fontIconShape=TextBlock',
        'xamlButtonTemplateBeforeTree=false',
        'enableErrorCount=0',
        'screenshotMain=ok',
        # Default text flyouts are not exposed by the ContextFlyout getter on 2.6.4;
        # the probe records the attempt as not observable rather than claiming cover.
        'textBlockFlyoutCaptured=not-observable',
        'richTextFlyoutCaptured=not-observable')
}

# ---------------------------------------------------------------------------
# 汇总统计（可由 -SelfTest 独立验证）
# ---------------------------------------------------------------------------

function Get-Median {
    param([double[]]$Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [double]::NaN
    }

    $sorted = @($Values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return [double]$sorted[$middle]
    }

    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0
}

function Get-Mad {
    param([double[]]$Values)

    $median = Get-Median $Values
    $deviations = @($Values | ForEach-Object { [math]::Abs($_ - $median) })
    return Get-Median $deviations
}

# 1.4826 * MAD 是正态分布下标准差的稳健估计。
function Get-PooledSigma {
    param([double]$MadA, [double]$MadB)

    $sigmaA = 1.4826 * $MadA
    $sigmaB = 1.4826 * $MadB
    return [math]::Sqrt(($sigmaA * $sigmaA + $sigmaB * $sigmaB) / 2.0)
}

# 超出噪声 = 差值的绝对值同时超过 2 倍合并稳健标准差、基线中位数的 1%
# 与仪器分辨率。分辨率必须进入门限，否则亚分辨率差值会被当成效应。
function Test-BeyondNoise {
    param(
        [double]$BaselineMedian,
        [double]$BaselineMad,
        [double]$VariantMedian,
        [double]$VariantMad,
        [double]$PracticalFloor = 0.01,
        [double]$Resolution = 0.0)

    $delta = [math]::Abs($VariantMedian - $BaselineMedian)
    $threshold = [math]::Max(
        [math]::Max(2.0 * (Get-PooledSigma $BaselineMad $VariantMad), [math]::Abs($BaselineMedian) * $PracticalFloor),
        [math]::Abs($Resolution))
    return ($delta -gt $threshold)
}

function Get-BinomialCoefficient {
    param([int]$N, [int]$K)

    if ($K -lt 0 -or $K -gt $N) { return 0.0 }
    $K = [math]::Min($K, $N - $K)
    $result = 1.0
    for ($i = 1; $i -le $K; $i++) {
        $result = $result * ($N - $K + $i) / $i
    }

    return $result
}

# 精确双侧符号检验：H0 为每轮差值正负各半。零差值不参与（保守做法）。
function Get-SignTestPValue {
    param([int]$Positive, [int]$Negative)

    $n = $Positive + $Negative
    if ($n -eq 0) { return 1.0 }
    $k = [math]::Min($Positive, $Negative)
    $tail = 0.0
    for ($i = 0; $i -le $k; $i++) {
        $tail += Get-BinomialCoefficient -N $n -K $i
    }

    $p = 2.0 * $tail / [math]::Pow(2.0, $n)
    return [math]::Min(1.0, $p)
}

function Get-PairedStatistics {
    <#
    .SYNOPSIS
        Summary and exact sign test for one paired delta series.
    #>
    param([double[]]$Deltas)

    $positive = @($Deltas | Where-Object { $_ -gt 0 }).Count
    $negative = @($Deltas | Where-Object { $_ -lt 0 }).Count
    $zero = @($Deltas | Where-Object { $_ -eq 0 }).Count
    $mean = if ($Deltas.Count -gt 0) { ($Deltas | Measure-Object -Average).Average } else { [double]::NaN }
    return [pscustomobject]@{
        count    = $Deltas.Count
        mean     = [math]::Round($mean, 2)
        median   = [math]::Round((Get-Median @($Deltas)), 2)
        positive = $positive
        negative = $negative
        zero     = $zero
        pValue   = [math]::Round((Get-SignTestPValue -Positive $positive -Negative $negative), 4)
        deltas   = @($Deltas | ForEach-Object { [math]::Round($_, 2) })
    }
}

function Get-PairedComparison {
    <#
    .SYNOPSIS
        Per-round paired deltas between two aligned series.
    #>
    param([double[]]$Baseline, [double[]]$Variant)

    if ($Baseline.Count -ne $Variant.Count) {
        throw "Paired series must have the same length (baseline $($Baseline.Count), variant $($Variant.Count))."
    }

    $deltas = [double[]]@(
        for ($i = 0; $i -lt $Baseline.Count; $i++) { $Variant[$i] - $Baseline[$i] })
    return Get-PairedStatistics -Deltas $deltas
}

function Invoke-SelfTest {
    $cases = [Collections.Generic.List[object]]::new()
    function Add-Case([string]$Name, $Actual, $Expected) {
        $cases.Add([pscustomobject]@{ Name = $Name; Actual = $Actual; Expected = $Expected })
    }

    Add-Case 'median-odd' (Get-Median @(1, 2, 3, 4, 100)) 3
    Add-Case 'median-even' (Get-Median @(1, 2, 3, 4)) 2.5
    Add-Case 'mad-odd' (Get-Mad @(1, 2, 3, 4, 100)) 1
    Add-Case 'mad-even' (Get-Mad @(1, 2, 3, 4)) 1
    Add-Case 'pooled-sigma-equal' ([math]::Round((Get-PooledSigma 1 1), 4)) 1.4826
    Add-Case 'beyond-noise-yes' (Test-BeyondNoise -BaselineMedian 100 -BaselineMad 1 -VariantMedian 104 -VariantMad 1) $true
    Add-Case 'beyond-noise-no-absolute' (Test-BeyondNoise -BaselineMedian 1000 -BaselineMad 1 -VariantMedian 1000.5 -VariantMad 1) $false
    Add-Case 'beyond-noise-no-practical-floor' (Test-BeyondNoise -BaselineMedian 1000 -BaselineMad 0.1 -VariantMedian 1005 -VariantMad 0.1) $false
    Add-Case 'beyond-noise-zero-mad' (Test-BeyondNoise -BaselineMedian 100 -BaselineMad 0 -VariantMedian 101 -VariantMad 0) $false
    Add-Case 'beyond-noise-sub-resolution' (Test-BeyondNoise -BaselineMedian 1000 -BaselineMad 0.1 -VariantMedian 1003 -VariantMad 0.1 -Resolution 5) $false
    Add-Case 'beyond-noise-above-resolution' (Test-BeyondNoise -BaselineMedian 1000 -BaselineMad 0.1 -VariantMedian 1011 -VariantMad 0.1 -Resolution 5) $true
    Add-Case 'sign-test-7-0' (Get-SignTestPValue -Positive 7 -Negative 0) 0.015625
    Add-Case 'sign-test-6-1' (Get-SignTestPValue -Positive 6 -Negative 1) 0.125
    Add-Case 'sign-test-5-2' (Get-SignTestPValue -Positive 5 -Negative 2) 0.453125
    Add-Case 'sign-test-none' (Get-SignTestPValue -Positive 0 -Negative 0) 1.0
    $pairedCase = Get-PairedComparison -Baseline @(100, 100, 100, 100, 100) -Variant @(101, 101, 103, 101, 102)
    Add-Case 'paired-positive-count' $pairedCase.positive 5
    Add-Case 'paired-p' $pairedCase.pValue 0.0625
    Add-Case 'paired-mean' $pairedCase.mean 1.6

    $failed = 0
    foreach ($case in $cases) {
        if ($case.Actual -eq $case.Expected) {
            Write-Output ('PASS {0} = {1}' -f $case.Name, $case.Actual)
        }
        else {
            $failed++
            Write-Output ('FAIL {0}: expected {1}, actual {2}' -f $case.Name, $case.Expected, $case.Actual)
        }
    }

    if ($failed -gt 0) {
        Write-Output "SELFTEST FAILED ($failed)"
        exit 1
    }

    Write-Output 'SELFTEST PASSED'
    exit 0
}

if ($SelfTest) {
    Invoke-SelfTest
}

# ---------------------------------------------------------------------------
# Win32 辅助
# ---------------------------------------------------------------------------

if (-not ('XamlMeasureWin32' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class XamlMeasureWin32
{
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(IntPtr hWnd, StringBuilder text, int max);

    public static IntPtr FindVisibleWindow(uint targetProcessId)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, lparam) =>
        {
            uint owner;
            GetWindowThreadProcessId(hwnd, out owner);
            if (owner != targetProcessId || !IsWindowVisible(hwnd))
            {
                return true;
            }

            var name = new StringBuilder(256);
            GetClassNameW(hwnd, name, 256);
            var cls = name.ToString();
            if (cls.Contains("WinUIDesktopWin32WindowClass") || cls.Contains("Microsoft.UI.Windowing.Window"))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
}

# System.Drawing is loaded on demand; both pixel helpers need the same
# reference set, so it is collected once here.
function Add-DrawingType {
    param([string]$TypeName, [string]$TypeDefinition)

    if ($TypeName -as [type]) { return }
    Add-Type -AssemblyName System.Drawing.Common
    $references = [AppDomain]::CurrentDomain.GetAssemblies() |
        Where-Object { -not $_.IsDynamic -and $_.Location } |
        ForEach-Object { $_.Location } |
        Select-Object -Unique
    Add-Type -ReferencedAssemblies $references -TypeDefinition $TypeDefinition
}

Add-DrawingType -TypeName 'XamlPixelDiff' -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class XamlPixelDiff
{
    // 返回 diff=<像素数>;total=<总像素>;box=<x,y,w,h>;size=<w>x<h>
    public static string Compare(string pathA, string pathB, int threshold)
    {
        using (var a = new Bitmap(pathA))
        using (var b = new Bitmap(pathB))
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return string.Format("size-mismatch;{0}x{1};{2}x{3}", a.Width, a.Height, b.Width, b.Height);
            }

            var rect = new Rectangle(0, 0, a.Width, a.Height);
            var dataA = a.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var dataB = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = dataA.Stride;
                var bytesA = new byte[stride * a.Height];
                var bytesB = new byte[stride * b.Height];
                Marshal.Copy(dataA.Scan0, bytesA, 0, bytesA.Length);
                Marshal.Copy(dataB.Scan0, bytesB, 0, bytesB.Length);

                int diff = 0;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = 0; y < a.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < a.Width; x++)
                    {
                        int offset = row + x * 3;
                        int delta =
                            Math.Abs(bytesA[offset] - bytesB[offset]) +
                            Math.Abs(bytesA[offset + 1] - bytesB[offset + 1]) +
                            Math.Abs(bytesA[offset + 2] - bytesB[offset + 2]);
                        if (delta > threshold * 3)
                        {
                            diff++;
                            if (x < minX) { minX = x; }
                            if (y < minY) { minY = y; }
                            if (x > maxX) { maxX = x; }
                            if (y > maxY) { maxY = y; }
                        }
                    }
                }

                if (diff == 0)
                {
                    return string.Format("diff=0;total={0};box=none;size={1}x{2}", a.Width * a.Height, a.Width, a.Height);
                }

                return string.Format(
                    "diff={0};total={1};box={2},{3},{4},{5};size={6}x{7}",
                    diff,
                    a.Width * a.Height,
                    minX,
                    minY,
                    maxX - minX + 1,
                    maxY - minY + 1,
                    a.Width,
                    a.Height);
            }
            finally
            {
                a.UnlockBits(dataA);
                b.UnlockBits(dataB);
            }
        }
    }
}
'@

Add-DrawingType -TypeName 'XamlFrameCheck' -TypeDefinition @'
using System;
using System.Drawing;

public static class XamlFrameCheck
{
    // 空白帧：应用内容尚未绘制，只有均匀深色底和标题栏按钮（实测 sd=0、亮像素占比 0）。
    // 用亮度标准差与亮像素占比判断，避免用文件大小这种脆弱阈值。
    public static bool IsBlank(string path)
    {
        using (var bitmap = new Bitmap(path))
        {
            int step = Math.Max(8, Math.Min(bitmap.Width, bitmap.Height) / 64);
            double sum = 0;
            double sumSquares = 0;
            int count = 0;
            int bright = 0;
            for (int y = step; y < bitmap.Height - step; y += step)
            {
                for (int x = step; x < bitmap.Width - step; x += step)
                {
                    var color = bitmap.GetPixel(x, y);
                    double luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
                    sum += luminance;
                    sumSquares += luminance * luminance;
                    count++;
                    if (luminance > 60)
                    {
                        bright++;
                    }
                }
            }

            if (count == 0)
            {
                return true;
            }

            double mean = sum / count;
            double variance = (sumSquares / count) - (mean * mean);
            double deviation = Math.Sqrt(Math.Max(0, variance));
            double brightFraction = (double)bright / count;
            return deviation < 2 || brightFraction < 0.01;
        }
    }
}
'@

function Get-ScreenshotAudit {
    <#
    .SYNOPSIS
        Counts captured product frames and the blank ones the comparison skips.
    #>
    $directory = Join-Path $script:Artifacts 'screenshots'
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($variant in $Variants) {
        $files = @(Get-ChildItem $directory -Filter ($variant + '-run*.png') -ErrorAction SilentlyContinue | Sort-Object Name)
        $blank = @($files | Where-Object { [XamlFrameCheck]::IsBlank($_.FullName) })
        $rows.Add([pscustomobject]@{
            variant   = $variant
            frames    = $files.Count
            blank     = $blank.Count
            blankRuns = @($blank | ForEach-Object { $_.BaseName })
        })
    }

    return $rows
}

function Copy-RepresentativeScreenshot {
    param([string]$Variant)

    $directory = Join-Path $script:Artifacts 'screenshots'
    if (-not (Test-Path $directory)) {
        return $null
    }

    $destination = Join-Path $directory ('representative\' + $Variant + '.png')
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    foreach ($candidate in @(Get-ChildItem $directory -Filter ($Variant + '-run*.png') | Sort-Object Name)) {
        if (-not [XamlFrameCheck]::IsBlank($candidate.FullName)) {
            Copy-Item -LiteralPath $candidate.FullName -Destination $destination -Force
            return $destination
        }
    }

    return $null
}

function Get-PrivateWorkingSetBytes {
    param([int]$ProcessId)

    $perf = Get-CimInstance Win32_PerfRawData_PerfProc_Process -Filter "IDProcess=$ProcessId" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $perf -and $null -ne $perf.WorkingSetPrivate) {
        return [double]$perf.WorkingSetPrivate
    }

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { return [double]::NaN }
    return [double]$process.PrivateMemorySize64
}

function Get-AmbientSnapshot {
    $systemCpu = Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $processes = @(Get-Process -Name 'BiliCopilot.UI' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path -ne $script:InstalledExe })
    $workingSet = 0.0
    $cpu = 0.0
    foreach ($process in $processes) {
        $workingSet += $process.WorkingSet64 / 1MB
        $cpu += $process.CPU
    }

    return [pscustomobject]@{
        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        systemCpuPercent = if ($null -ne $systemCpu) { [double]$systemCpu.PercentProcessorTime } else { -1 }
        otherBiliProcessCount = $processes.Count
        otherBiliWorkingSetMB = [math]::Round($workingSet, 1)
        otherBiliCpuSeconds = [math]::Round($cpu, 1)
        totalProcessCount = @(Get-Process).Count
    }
}

function Wait-VisibleWindow {
    param([System.Diagnostics.Process]$Process, [int]$TimeoutSeconds = 60)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $handle = [XamlMeasureWin32]::FindVisibleWindow([uint32]$Process.Id)
        if ($handle -ne [IntPtr]::Zero) {
            return $handle
        }

        if ($Process.HasExited) {
            return [IntPtr]::Zero
        }

        Start-Sleep -Milliseconds $script:PollIntervalMs
    }

    return [IntPtr]::Zero
}

function Save-WindowScreenshot {
    param([IntPtr]$WindowHandle, [string]$Path)

    Add-Type -AssemblyName System.Drawing.Common
    if (-not ('XamlMeasureCapture' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class XamlMeasureCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@
    }

    $rect = New-Object XamlMeasureCapture+RECT
    if (-not [XamlMeasureCapture]::GetWindowRect($WindowHandle, [ref]$rect)) {
        return $false
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        return $false
    }

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        $ok = [XamlMeasureCapture]::PrintWindow($WindowHandle, $hdc, 2)
    }
    finally {
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()
    }

    if ($ok) {
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $bitmap.Dispose()
    return $ok
}

function Measure-WakeLatency {
    param([IntPtr]$WindowHandle, [int]$TimeoutMilliseconds = 5000)

    [XamlMeasureWin32]::ShowWindow($WindowHandle, 6) | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    while (-not [XamlMeasureWin32]::IsIconic($WindowHandle)) {
        if ([DateTime]::UtcNow -gt $deadline) {
            return [double]::NaN
        }

        Start-Sleep -Milliseconds 10
    }

    Start-Sleep -Milliseconds 500
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    [XamlMeasureWin32]::ShowWindow($WindowHandle, 9) | Out-Null
    [XamlMeasureWin32]::SetForegroundWindow($WindowHandle) | Out-Null
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ($true) {
        $visible = [XamlMeasureWin32]::IsWindowVisible($WindowHandle)
        $iconic = [XamlMeasureWin32]::IsIconic($WindowHandle)
        if ($visible -and -not $iconic) {
            break
        }

        if ([DateTime]::UtcNow -gt $deadline) {
            return [double]::NaN
        }

        Start-Sleep -Milliseconds 5
    }

    $stopwatch.Stop()
    return [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
}

# ---------------------------------------------------------------------------
# 构建 / 打包 / 安装
# ---------------------------------------------------------------------------

function Assert-Preconditions {
    foreach ($tool in @($script:MakeAppx, $script:SignTool, $script:DevCertificate)) {
        if (-not (Test-Path $tool)) {
            throw "Missing required tool or certificate: $tool"
        }
    }

    if (-not (Test-Path $script:ProbeProject)) {
        throw "Probe project not found: $($script:ProbeProject)"
    }

    $certificate = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
        Where-Object { $_.Subject -eq 'CN=BiliCopilotDev' } |
        Select-Object -First 1
    if ($null -eq $certificate) {
        throw 'Dev signing certificate (CN=BiliCopilotDev) is not trusted in LocalMachine\TrustedPeople.'
    }

    $script:ProductionSnapshot = @(Get-AppxPackage -Name 'Mang.BiliCopilot*' |
        Where-Object { $_.Name -in $script:ForbiddenPackages } |
        Select-Object Name, Version, PackageFullName)

    # Compare the *resolved* package, not the constant target name. The old
    # check compared $script:PackageName with itself and could never fire.
    foreach ($package in @(Get-AppxPackage -Name $script:PackageName -ErrorAction SilentlyContinue)) {
        if ($script:ForbiddenPackages -contains $package.Name) {
            throw "Refusing to run: resolved package '$($package.Name)' is a production identity."
        }
        if ($package.Name -ne $script:PackageName) {
            throw "Refusing to run: resolved package '$($package.Name)' is not '$($script:PackageName)'."
        }
    }
}

function Initialize-VersionCounter {
    # 版本号必须高于已安装的 Dev 包，否则 Add-AppxPackage 会以 0x80073CFB 拒绝更新。
    $installed = Get-AppxPackage -Name $script:PackageName
    if ($null -ne $installed) {
        $script:VersionCounter = [int]([version]$installed.Version).Revision
    }
}

function Close-OrphanDevProcesses {
    $installed = Get-AppxPackage -Name $script:PackageName
    if ($null -eq $installed) {
        return
    }

    $exe = Join-Path $installed.InstallLocation 'BiliCopilot.UI.exe'
    # One filter is the guard: the body cannot see a process outside it.
    $orphans = @(Get-Process -Name 'BiliCopilot.UI' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe })
    foreach ($orphan in $orphans) {
        Write-Output ("closing leftover Dev process {0}" -f $orphan.Id)
        [void]$orphan.CloseMainWindow()
        if (-not $orphan.WaitForExit(10000)) {
            Assert-DevProcess -Process $orphan
            Stop-Process -Id $orphan.Id -Force
        }
    }
}

function ConvertFrom-ProbeJournal {
    param([string]$Variant, [int]$ExitCode, [object]$Json)

    return [pscustomobject]@{
        variant = $Variant
        exitCode = $ExitCode
        passed = [bool]$Json.Passed
        error = $Json.Error
        failureReasons = @($Json.FailureReasons)
        switches = @($Json.Switches)
        observed = $Json.Observed
        expectations = @($Json.Expectations)
        statesBeforeStart = $Json.StatesBeforeStart
        statesInsideStart = $Json.StatesInsideStart
    }
}

function Invoke-Probe {
    param([string]$Variant)

    $expectations = $script:ProbeExpectations[$Variant]
    if ($null -eq $expectations) {
        throw "No probe expectations declared for variant '$Variant'."
    }

    $directory = Join-Path $script:Artifacts ('probe\' + $Variant)
    New-Item -ItemType Directory -Force -Path (Join-Path $directory 'shots') | Out-Null
    $jsonPath = Join-Path $directory 'probe.json'
    $arguments = @(
        '--variant', $Variant,
        '--switches', $(if ($Variant -eq 'baseline') { 'none' } else { $Variant }),
        '--out', $jsonPath,
        '--screenshot-dir', (Join-Path $directory 'shots'))
    foreach ($expectation in $expectations) {
        $arguments += @('--expect', $expectation)
    }

    $process = Start-Process -FilePath $script:ProbeExe -ArgumentList $arguments -Wait -PassThru
    if (-not (Test-Path $jsonPath)) {
        throw "Probe produced no JSON for '$Variant' (exit $($process.ExitCode))."
    }

    $json = Get-Content $jsonPath -Raw | ConvertFrom-Json
    Add-Type -AssemblyName System.Drawing.Common
    foreach ($shot in @('gallery', 'gallery-menu', 'gallery-text-block', 'gallery-rich-text')) {
        $bmpPath = Join-Path $directory ('shots\' + $shot + '.bmp')
        $pngPath = Join-Path $directory ('shots\' + $shot + '.png')
        if (Test-Path $bmpPath) {
            $image = [System.Drawing.Image]::FromFile($bmpPath)
            $image.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $image.Dispose()
            Remove-Item $bmpPath -Force
        }
    }

    $result = ConvertFrom-ProbeJournal -Variant $Variant -ExitCode $process.ExitCode -Json $json
    $script:ProbeResults[$Variant] = $result

    if ($process.ExitCode -ne 0) {
        throw "Probe expectation failed for '$Variant': $($result.failureReasons -join '; ') $($result.error)"
    }

    Write-Output ("probe {0}: states ok, fontIcon={1}, xamlButtonTemplateBeforeTree={2}" -f $Variant, $json.Observed.fontIconShape, $json.Observed.xamlButtonTemplateBeforeTree)
}

function Get-GeneratedEntryPointFiles {
    # The SDK keeps IntermediateOutputPath under the project even when OutDir is
    # redirected to src\_build; check both so this does not depend on which wins.
    $objRoots = @(
        (Join-Path $script:RepoRoot 'src\Desktop\BiliCopilot.UI\obj'),
        (Join-Path $script:RepoRoot 'src\_build\x64\Release\BiliCopilot.UI\obj'))

    $files = [Collections.Generic.List[object]]::new()
    foreach ($objRoot in $objRoots) {
        if (-not (Test-Path $objRoot)) { continue }
        foreach ($file in @(Get-ChildItem $objRoot -Recurse -Filter 'App.g.i.cs' -ErrorAction SilentlyContinue)) {
            $files.Add($file)
        }
    }

    return $files.ToArray()
}

function Assert-GeneratedEntryPoint {
    <#
    .SYNOPSIS
        Proves the MSBuild switch property reached the generated Main.
    .DESCRIPTION
        The product link was previously asserted only by "we passed the property".
        The XAML compiler writes XamlOptionalChanges.EnableChange(...) into
        obj/.../App.g.i.cs; parsing that file is the actual proof, and it catches
        a stale generated entry point left by an earlier variant.
    #>
    param([string]$Variant)

    $expected = @($script:VariantSwitches[$Variant] | Sort-Object)
    $files = @(Get-GeneratedEntryPointFiles)
    if ($files.Count -eq 0) {
        throw "No generated App.g.i.cs under the intermediate directory for '$Variant': the switch property did not reach the XAML compiler."
    }

    foreach ($file in $files) {
        $text = Get-Content $file.FullName -Raw
        $calls = [regex]::Matches(
            $text,
            'XamlOptionalChanges\.(EnableChange|DisableChange)\(\s*global::Microsoft\.UI\.Xaml\.Settings\.XamlChangeId\.(\w+)\s*\)')
        $enabled = @($calls | Where-Object { $_.Groups[1].Value -eq 'EnableChange' } |
            ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique)
        $disabled = @($calls | Where-Object { $_.Groups[1].Value -eq 'DisableChange' } |
            ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique)

        $actualText = if ($enabled.Count -eq 0) { '<none>' } else { $enabled -join ',' }
        $expectedText = if ($expected.Count -eq 0) { '<none>' } else { $expected -join ',' }
        if ($disabled.Count -gt 0) {
            throw "Generated entry point for '$Variant' disables unexpected change(s) '$($disabled -join ',')': $($file.FullName)"
        }

        if (($enabled -join ',') -ne ($expected -join ',')) {
            throw "Generated entry point for '$Variant' enables '$actualText', expected '$expectedText'. Stale generated Main or the MSBuild property did not apply: $($file.FullName)"
        }

        Write-Output ("generated main {0}: EnableChange({1}) in {2}" -f $Variant, $actualText, $file.FullName)
    }
}

function Invoke-ProductionBuild {
    param([string]$Variant)

    if (-not $script:VariantSwitches.ContainsKey($Variant)) {
        throw "No switch mapping declared for variant '$Variant'."
    }

    if ($Variant -eq 'baseline') {
        $switchValue = $null
    }
    elseif ($Variant -eq 'all') {
        $switchValue = ($script:VariantSwitches['all'] | ForEach-Object { $_ }) -join '%2C'
    }
    else {
        $switchValue = $Variant
    }
    $arguments = @(
        'build',
        (Join-Path $script:RepoRoot 'src\Desktop\BiliCopilot.UI\BiliCopilot.UI.csproj'),
        '-c', 'Release',
        '-p:Platform=x64',
        '-p:PublishAot=false',
        '-p:GenerateAppxPackageOnBuild=false',
        # 复用 MSBuild 节点时，属性切换到第二个变体后曾出现构建空转（无 CPU 等待）。
        # 关闭节点复用与共享编译，单次构建约 2 分钟，行为可复现。
        '-nodeReuse:false',
        '-p:UseSharedCompilation=false',
        "-p:SolutionDir=$($script:RepoRoot)\src\")
    if ($null -ne $switchValue) {
        $arguments += "-p:EnabledXamlOptionalChanges=$switchValue"
    }

    $log = Join-Path $script:Artifacts ('logs\build-' + $Variant + '.log')
    New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null

    # Force the XAML compiler to regenerate its entry point so the assertion
    # below inspects this build, never an earlier variant's file.
    foreach ($generated in Get-GeneratedEntryPointFiles) {
        Remove-Item -LiteralPath $generated.FullName -Force
    }

    Write-Output ("build {0}: {1}" -f $Variant, ($arguments -join ' '))
    $output = & dotnet @arguments 2>&1
    $output | Set-Content -LiteralPath $log -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for '$Variant' (exit $LASTEXITCODE). See $log."
    }

    $manifest = Join-Path $script:Layout 'AppxManifest.xml'
    if (-not (Test-Path $manifest)) {
        throw "Build produced no AppxManifest.xml in $($script:Layout)."
    }

    Assert-GeneratedEntryPoint -Variant $Variant
}

function New-DevPackage {
    param([string]$Variant)

    $script:VersionCounter++
    $version = "2.2610.9.$($script:VersionCounter)"
    $manifestPath = Join-Path $script:Layout 'AppxManifest.xml'
    $original = Get-Content $manifestPath -Raw
    $identity = '<Identity Name="Mang.BiliCopilot.Dev" Publisher="CN=BiliCopilotDev" Version="' + $version + '" ProcessorArchitecture="x64" />'
    $patched = [regex]::Replace($original, '<Identity\s+[^>]*?/>', $identity, 1)
    if ($patched -notmatch 'Name="Mang\.BiliCopilot\.Dev"' -or $patched -match 'Name="Mang\.BiliCopilot"') {
        throw 'Refusing to pack: patched manifest identity is not Mang.BiliCopilot.Dev.'
    }

    $packageDirectory = Join-Path $script:Artifacts 'packages'
    New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
    $msix = Join-Path $packageDirectory ("$Variant-$version.msix")
    try {
        [IO.File]::WriteAllText($manifestPath, $patched)
        & $script:MakeAppx pack /d $script:Layout /p $msix /o | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "makeappx failed (exit $LASTEXITCODE)."
        }

        & $script:SignTool sign /fd SHA256 /f $script:DevCertificate /p '' $msix | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed (exit $LASTEXITCODE)."
        }
    }
    finally {
        [IO.File]::WriteAllText($manifestPath, $original)
    }

    return [pscustomobject]@{ Variant = $Variant; Version = $version; Path = $msix }
}

function Install-DevPackage {
    param([string]$Variant)

    $package = New-DevPackage -Variant $Variant
    Write-Output ("install {0} version {1}" -f $Variant, $package.Version)
    Add-AppxPackage -Path $package.Path -ForceUpdateFromAnyVersion -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name $script:PackageName
    if ($null -eq $installed) {
        throw "Package $($script:PackageName) is not installed after Add-AppxPackage."
    }

    if ($script:ForbiddenPackages -contains $installed.Name -or $installed.Name -ne $script:PackageName) {
        throw "Refusing to continue: resolved installed package '$($installed.Name)' is not '$($script:PackageName)'."
    }

    if ($installed.Version -ne $package.Version) {
        throw "Installed version $($installed.Version) does not match packed version $($package.Version)."
    }

    $exe = Join-Path $installed.InstallLocation 'BiliCopilot.UI.exe'
    if (-not (Test-Path $exe)) {
        throw "Installed package has no BiliCopilot.UI.exe: $exe"
    }

    $script:InstalledVariant = $Variant
    $script:InstalledExe = $exe
    return [pscustomobject]@{ Variant = $Variant; Version = $package.Version; Exe = $exe; InstallLocation = $installed.InstallLocation }
}

function Assert-DevProcess {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process -or $Process.HasExited) {
        throw 'Process already exited; cannot verify its path.'
    }

    $path = $Process.Path
    if ($path -ne $script:InstalledExe) {
        throw "Refusing to touch process $($Process.Id): path '$path' is not the installed Dev package executable."
    }
}

function Stop-DevApp {
    param([System.Diagnostics.Process]$Process, [IntPtr]$WindowHandle)

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    Assert-DevProcess -Process $Process
    if ($WindowHandle -ne [IntPtr]::Zero) {
        [XamlMeasureWin32]::ShowWindow($WindowHandle, 5) | Out-Null
    }

    [void]$Process.CloseMainWindow()
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not $Process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
        $Process.Refresh()
    }

    if (-not $Process.HasExited) {
        Assert-DevProcess -Process $Process
        Stop-Process -Id $Process.Id -Force
    }
}

function Invoke-WarmupLaunch {
    param([string]$Variant)

    Write-Output ("warmup {0}: discard run after install" -f $Variant)
    $process = Start-Process -FilePath $script:InstalledExe -PassThru
    $handle = Wait-VisibleWindow -Process $process -TimeoutSeconds 60
    if ($handle -eq [IntPtr]::Zero) {
        Stop-DevApp -Process $process -WindowHandle $handle
        throw 'Warmup launch produced no visible window.'
    }

    Start-Sleep -Seconds $WarmupSeconds
    Stop-DevApp -Process $process -WindowHandle $handle
}

function Invoke-MeasuredRun {
    param([string]$Variant, [int]$Round)

    $ambient = Get-AmbientSnapshot
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $script:InstalledExe -PassThru
    $handle = Wait-VisibleWindow -Process $process -TimeoutSeconds 60
    $stopwatch.Stop()
    if ($handle -eq [IntPtr]::Zero) {
        Stop-DevApp -Process $process -WindowHandle $handle
        throw "Measured run produced no visible window (variant $Variant, round $Round)."
    }

    # Report at the poll resolution instead of a false 0.1 ms precision.
    $startupRawMs = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
    $startupMs = [math]::Round($startupRawMs / $script:PollIntervalMs) * $script:PollIntervalMs
    $process.Refresh()
    if ($process.HasExited) {
        throw "App exited before settle (variant $Variant, round $Round)."
    }

    Start-Sleep -Seconds $SettleSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "App exited during settle (variant $Variant, round $Round)."
    }

    $privateWorkingSet = Get-PrivateWorkingSetBytes -ProcessId $process.Id
    $cpuAfterSettle = [double]$process.CPU
    $cpuStart = $cpuAfterSettle
    Start-Sleep -Seconds $IdleSeconds
    $process.Refresh()
    $cpuEnd = [double]$process.CPU
    $idleCpuPercent = [math]::Round((($cpuEnd - $cpuStart) / $IdleSeconds) * 100.0, 2)

    # 截图放在最小化/还原之前：还原后立刻 PrintWindow 偶尔会拿到空白帧。
    Start-Sleep -Milliseconds 300
    $screenshot = Join-Path $script:Artifacts ('screenshots\' + $Variant + '-run' + $Round + '.png')
    New-Item -ItemType Directory -Force -Path (Split-Path $screenshot) | Out-Null
    $captured = Save-WindowScreenshot -WindowHandle $handle -Path $screenshot

    $wake1 = Measure-WakeLatency -WindowHandle $handle
    $wake2 = Measure-WakeLatency -WindowHandle $handle
    $wakeValues = @($wake1, $wake2) | Where-Object { -not [double]::IsNaN($_) }
    $wakeRawMedian = if ($wakeValues.Count -gt 0) { Get-Median @($wakeValues) } else { [double]::NaN }
    # Same instrument resolution as window discovery: report whole poll ticks.
    $wakeMedian = if ([double]::IsNaN($wakeRawMedian)) { [double]::NaN } else { [math]::Round($wakeRawMedian / $script:PollIntervalMs) * $script:PollIntervalMs }

    $rect = New-Object XamlMeasureCapture+RECT
    $windowSize = 'unknown'
    if ([XamlMeasureCapture]::GetWindowRect($handle, [ref]$rect)) {
        $windowSize = "$($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top)"
    }

    $process.Refresh()
    $processWorkingSetMB = [math]::Round($process.WorkingSet64 / 1MB, 1)
    $cpuSeconds = [math]::Round($process.CPU, 1)
    Stop-DevApp -Process $process -WindowHandle $handle

    return [pscustomobject]@{
        variant = $Variant
        round = $Round
        packageVersion = (Get-AppxPackage -Name $script:PackageName).Version
        startedAtUtc = $ambient.capturedAtUtc
        startupMs = $startupMs
        startupRawMs = $startupRawMs
        startupResolutionMs = $script:PollIntervalMs
        settleSeconds = $SettleSeconds
        privateWorkingSetMB = [math]::Round($privateWorkingSet / 1MB, 1)
        processWorkingSetMB = $processWorkingSetMB
        cpuSecondsAfterSettle = [math]::Round($cpuAfterSettle, 2)
        cpuSecondsTotal = $cpuSeconds
        idleWindowSeconds = $IdleSeconds
        idleCpuPercentOfOneCore = $idleCpuPercent
        wakeLatencyMs = @([math]::Round($wake1, 1), [math]::Round($wake2, 1))
        wakeLatencyMedianMs = [math]::Round($wakeMedian, 1)
        wakeLatencyRawMedianMs = [math]::Round($wakeRawMedian, 1)
        wakeResolutionMs = $script:PollIntervalMs
        windowSize = $windowSize
        screenshot = if ($captured) { $screenshot } else { $null }
        ambient = $ambient
    }
}

# ---------------------------------------------------------------------------
# 汇总
# ---------------------------------------------------------------------------

$script:Metrics = @(
    # Resolution is the smallest real difference the instrument can express:
    # window/wake polling runs at PollIntervalMs, other counters are recorded at
    # the listed precision. It is part of the noise gate.
    @{ Name = 'startupMs'; Label = '启动到首帧 (ms)'; Unit = 'ms'; Resolution = $script:PollIntervalMs },
    @{ Name = 'privateWorkingSetMB'; Label = '私有工作集 (MB)'; Unit = 'MB'; Resolution = 0.1 },
    @{ Name = 'cpuSecondsAfterSettle'; Label = '稳定后进程 CPU (s)'; Unit = 's'; Resolution = 0.01 },
    @{ Name = 'idleCpuPercentOfOneCore'; Label = '空闲 CPU (单核 %)'; Unit = '%'; Resolution = 0.01 },
    @{ Name = 'wakeLatencyMedianMs'; Label = '唤回延迟中位数 (ms)'; Unit = 'ms'; Resolution = $script:PollIntervalMs })

function New-Summary {
    $byVariant = @{}
    foreach ($variant in $Variants) {
        $records = @($script:RunRecords | Where-Object { $_.variant -eq $variant })
        $metrics = [ordered]@{}
        foreach ($metric in $script:Metrics) {
            $values = @($records | ForEach-Object { [double]$_.($metric.Name) } | Where-Object { -not [double]::IsNaN($_) })
            $metrics[$metric.Name] = [pscustomobject]@{
                count = $values.Count
                median = [math]::Round((Get-Median @($values)), 2)
                mad = [math]::Round((Get-Mad @($values)), 2)
                min = if ($values.Count -gt 0) { [math]::Round(($values | Measure-Object -Minimum).Minimum, 2) } else { $null }
                max = if ($values.Count -gt 0) { [math]::Round(($values | Measure-Object -Maximum).Maximum, 2) } else { $null }
            }
        }

        $byVariant[$variant] = [pscustomobject]@{
            records = $records.Count
            metrics = $metrics
        }
    }

    $comparisons = [Collections.Generic.List[object]]::new()
    $baseline = $byVariant['baseline']
    foreach ($variant in $Variants) {
        if ($variant -eq 'baseline') { continue }
        foreach ($metric in $script:Metrics) {
            $baseMetric = $baseline.metrics[$metric.Name]
            $variantMetric = $byVariant[$variant].metrics[$metric.Name]
            $delta = $variantMetric.median - $baseMetric.median
            $comparisons.Add([pscustomobject]@{
                variant = $variant
                metric = $metric.Name
                label = $metric.Label
                unit = $metric.Unit
                baselineMedian = $baseMetric.median
                variantMedian = $variantMetric.median
                delta = [math]::Round($delta, 2)
                deltaPercent = if ($baseMetric.median -ne 0) { [math]::Round(($delta / $baseMetric.median) * 100.0, 1) } else { $null }
                pooledSigma = [math]::Round((Get-PooledSigma $baseMetric.mad $variantMetric.mad), 2)
                resolution = $metric.Resolution
                beyondNoise = (Test-BeyondNoise -BaselineMedian $baseMetric.median -BaselineMad $baseMetric.mad -VariantMedian $variantMetric.median -VariantMad $variantMetric.mad -Resolution $metric.Resolution)
            })
        }
    }

    # 配对分析：同一轮的变体值减基线值。池化的 2σ 门限无法判断系统性
    # 偏移（例如每个变体轮次都低 1 秒），精确符号检验可以。
    $paired = [Collections.Generic.List[object]]::new()
    $baselineRecords = @($script:RunRecords | Where-Object { $_.variant -eq 'baseline' })
    foreach ($variant in $Variants) {
        if ($variant -eq 'baseline') { continue }
        $variantRecords = @($script:RunRecords | Where-Object { $_.variant -eq $variant })
        foreach ($metric in $script:Metrics) {
            $deltas = [Collections.Generic.List[double]]::new()
            foreach ($record in $variantRecords) {
                $match = $baselineRecords | Where-Object { $_.round -eq $record.round } | Select-Object -First 1
                if ($null -eq $match) { continue }
                $variantValue = [double]$record.($metric.Name)
                $baselineValue = [double]$match.($metric.Name)
                if ([double]::IsNaN($variantValue) -or [double]::IsNaN($baselineValue)) { continue }
                $deltas.Add($variantValue - $baselineValue)
            }

            if ($deltas.Count -eq 0) { continue }
            $comparison = Get-PairedStatistics -Deltas ([double[]]$deltas)
            $paired.Add([pscustomobject]@{
                variant      = $variant
                metric       = $metric.Name
                label        = $metric.Label
                unit         = $metric.Unit
                resolution   = $metric.Resolution
                count        = $comparison.count
                mean         = $comparison.mean
                median       = $comparison.median
                positive     = $comparison.positive
                negative     = $comparison.negative
                zero         = $comparison.zero
                pValue       = $comparison.pValue
                deltas       = $comparison.deltas
                belowResolution = ([math]::Abs($comparison.median) -lt $metric.Resolution)
            })
        }
    }

    $noiseFloor = [ordered]@{}
    foreach ($metric in $script:Metrics) {
        $mads = @($Variants | ForEach-Object { $byVariant[$_].metrics[$metric.Name].mad })
        $noiseFloor[$metric.Name] = [math]::Round((Get-Median @($mads)), 2)
    }

    return [pscustomobject]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        packageName = $script:PackageName
        runsPerVariant = $Runs
        settleSeconds = $SettleSeconds
        idleSeconds = $IdleSeconds
        variants = $byVariant
        comparisons = $comparisons
        paired = $paired
        screenshotAudit = @(Get-ScreenshotAudit)
        noiseFloorMad = $noiseFloor
        pollIntervalMs = $script:PollIntervalMs
        probes = $script:ProbeResults
        notes = $script:ProcedureNotes
    }
}

function Save-CroppedRegion {
    param([string]$Source, [string]$Destination, [int]$X, [int]$Y, [int]$Width, [int]$Height)

    Add-Type -AssemblyName System.Drawing.Common
    $image = [System.Drawing.Image]::FromFile($Source)
    try {
        if ($X + $Width -gt $image.Width) { $Width = $image.Width - $X }
        if ($Y + $Height -gt $image.Height) { $Height = $image.Height - $Y }
        if ($Width -le 0 -or $Height -le 0) { return $false }
        $crop = New-Object System.Drawing.Bitmap($Width, $Height)
        $graphics = [System.Drawing.Graphics]::FromImage($crop)
        try {
            $sourceRect = New-Object System.Drawing.Rectangle($X, $Y, $Width, $Height)
            $destinationRect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)
            $graphics.DrawImage($image, $destinationRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            $crop.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $crop.Dispose()
        }
    }
    finally {
        $image.Dispose()
    }

    return $true
}

function New-VisualComparison {
    param([string]$BaselineVariant = 'baseline')

    $comparisons = [Collections.Generic.List[object]]::new()
    $baselineProbe = Join-Path $script:Artifacts ('probe\' + $BaselineVariant + '\shots')
    foreach ($variant in $Variants) {
        if ($variant -eq $BaselineVariant) { continue }
        $variantProbe = Join-Path $script:Artifacts ('probe\' + $variant + '\shots')
        foreach ($shot in @('gallery', 'gallery-menu', 'gallery-text-block', 'gallery-rich-text')) {
            $left = Join-Path $baselineProbe ($shot + '.png')
            $right = Join-Path $variantProbe ($shot + '.png')
            if ((Test-Path $left) -and (Test-Path $right)) {
                $result = [XamlPixelDiff]::Compare($left, $right, 8)
                $comparisons.Add([pscustomobject]@{
                    kind = 'probe-' + $shot
                    variant = $variant
                    baseline = $BaselineVariant
                    result = $result
                })
            }
        }

        # This copies the first non-blank frame to representative/<variant>.png.
        $baselineShot = Copy-RepresentativeScreenshot -Variant $BaselineVariant
        $variantShot = Copy-RepresentativeScreenshot -Variant $variant
        if ($null -ne $baselineShot -and $null -ne $variantShot) {
            $comparisons.Add([pscustomobject]@{
                kind = 'bili-home-full'
                variant = $variant
                baseline = $BaselineVariant
                result = [XamlPixelDiff]::Compare($baselineShot, $variantShot, 8)
            })

            # 首页推荐内容每次刷新都会变；另外比较稳定的外壳：顶栏与左侧导航。
            $cropDirectory = Join-Path $script:Artifacts 'screenshots\diff'
            New-Item -ItemType Directory -Force -Path $cropDirectory | Out-Null
            foreach ($region in @(
                @{ Name = 'chrome-top'; X = 0; Y = 0; Width = 1920; Height = 110 },
                @{ Name = 'chrome-left'; X = 0; Y = 110; Width = 190; Height = 1030 })) {
                $baseCrop = Join-Path $cropDirectory ($BaselineVariant + '-' + $region.Name + '.png')
                $variantCrop = Join-Path $cropDirectory ($variant + '-' + $region.Name + '.png')
                $savedBase = Save-CroppedRegion -Source $baselineShot -Destination $baseCrop -X $region.X -Y $region.Y -Width $region.Width -Height $region.Height
                $savedVariant = Save-CroppedRegion -Source $variantShot -Destination $variantCrop -X $region.X -Y $region.Y -Width $region.Width -Height $region.Height
                if ($savedBase -and $savedVariant) {
                    $comparisons.Add([pscustomobject]@{
                        kind = 'bili-' + $region.Name
                        variant = $variant
                        baseline = $BaselineVariant
                        result = [XamlPixelDiff]::Compare($baseCrop, $variantCrop, 8)
                    })
                }
            }
        }
    }

    return $comparisons
}

function Write-SummaryMarkdown {
    param([object]$Summary, [object[]]$VisualComparisons)

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# XAML optional changes: measured summary')
    $lines.Add('')
    $lines.Add("Generated: $($Summary.generatedAtUtc)  ")
    $lines.Add("Package: $($Summary.packageName)  ")
    $lines.Add("Runs per variant: $($Summary.runsPerVariant)  ")
    $lines.Add("Settle: $($Summary.settleSeconds) s, idle window: $($Summary.idleSeconds) s")
    $lines.Add('')
    $lines.Add('## Medians (MAD)')
    $lines.Add('')
    $header = '| Metric | ' + (($Variants | ForEach-Object { $_ }) -join ' | ') + ' |'
    $separator = '|---|' + (($Variants | ForEach-Object { '---' }) -join '|') + '|'
    $lines.Add($header)
    $lines.Add($separator)
    foreach ($metric in $script:Metrics) {
        $cells = foreach ($variant in $Variants) {
            $value = $Summary.variants[$variant].metrics[$metric.Name]
            ('{0} (MAD {1})' -f $value.median, $value.mad)
        }
        $lines.Add('| ' + $metric.Label + ' | ' + ($cells -join ' | ') + ' |')
    }

    $lines.Add('')
    $lines.Add('## Deltas vs baseline')
    $lines.Add('')
    $lines.Add('| Switch | Metric | Baseline | Variant | Delta | Delta % | Resolution | Beyond noise |')
    $lines.Add('|---|---|---|---|---|---|---|---|')
    foreach ($comparison in $Summary.comparisons) {
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |' -f
            $comparison.variant,
            $comparison.label,
            $comparison.baselineMedian,
            $comparison.variantMedian,
            $comparison.delta,
            $comparison.deltaPercent,
            $comparison.resolution,
            $comparison.beyondNoise))
    }

    $lines.Add('')
    $lines.Add('Noise gate: |delta| > max(2 x pooled robust sigma, 1% of baseline, instrument resolution).')
    $lines.Add("Instrument resolution: window/wake polling $($Summary.pollIntervalMs) ms; counters are recorded at the resolution listed above.")
    $lines.Add('')
    $lines.Add('## Paired per-round deltas (variant - baseline, same round)')
    $lines.Add('')
    $lines.Add('| Switch | Metric | n | Mean | Median | + / - / 0 | Exact sign-test p | Below resolution |')
    $lines.Add('|---|---|---|---|---|---|---|---|')
    foreach ($comparison in $Summary.paired) {
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} | {5} / {6} / {7} | {8} | {9} |' -f
            $comparison.variant,
            $comparison.label,
            $comparison.count,
            $comparison.mean,
            $comparison.median,
            $comparison.positive,
            $comparison.negative,
            $comparison.zero,
            $comparison.pValue,
            $comparison.belowResolution))
    }

    $lines.Add('')
    $lines.Add('## Product screenshots')
    $lines.Add('')
    $lines.Add('| Variant | Frames | Blank skipped | Blank runs |')
    $lines.Add('|---|---|---|---|')
    foreach ($row in $Summary.screenshotAudit) {
        $runsText = if ($row.blankRuns.Count -eq 0) { '-' } else { $row.blankRuns -join ', ' }
        $lines.Add(('| {0} | {1} | {2} | {3} |' -f $row.variant, $row.frames, $row.blank, $runsText))
    }

    $lines.Add('')
    $lines.Add('Only the first non-blank frame per variant is used for the product comparison;')
    $lines.Add('blank frames are counted here and skipped, never silently compared.')
    $lines.Add('')
    $lines.Add('## Visual comparisons (diff pixels / total)')
    $lines.Add('')
    foreach ($comparison in $VisualComparisons) {
        $lines.Add(('- {0} {1}: {2}' -f $comparison.kind, $comparison.variant, $comparison.result))
    }

    $lines.Add('')
    $lines.Add('## Probe evidence')
    $lines.Add('')
    foreach ($variant in $Variants) {
        $probe = $Summary.probes[$variant]
        if ($null -eq $probe) { continue }
        $lines.Add(('- {0}: passed={1}, switches={2}, fontIcon={3}, symbolIcon={4}, xamlButtonTemplateBeforeTree={5}' -f
            $variant,
            $probe.passed,
            ($probe.switches -join ','),
            $probe.observed.fontIconShape,
            $probe.observed.symbolIconShape,
            $probe.observed.xamlButtonTemplateBeforeTree))
    }

    return ($lines -join [Environment]::NewLine)
}

# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------

if ($RegenerateSummary) {
    foreach ($variant in $Variants) {
        $rawDirectory = Join-Path $script:Artifacts ('raw\' + $variant)
        if (Test-Path $rawDirectory) {
            foreach ($file in @(Get-ChildItem $rawDirectory -Filter 'run-*.json' | Sort-Object Name)) {
                $script:RunRecords.Add((Get-Content $file.FullName -Raw | ConvertFrom-Json))
            }
        }

        $probePath = Join-Path $script:Artifacts ('probe\' + $variant + '\probe.json')
        if (Test-Path $probePath) {
            $json = Get-Content $probePath -Raw | ConvertFrom-Json
            $script:ProbeResults[$variant] = ConvertFrom-ProbeJournal -Variant $variant -ExitCode 0 -Json $json
        }
    }

    if ($script:RunRecords.Count -eq 0) {
        throw "No raw run records under $($script:Artifacts)."
    }

    $summary = New-Summary
    $visual = New-VisualComparison
    $summary | Add-Member -NotePropertyName visualComparisons -NotePropertyValue $visual -Force
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:Artifacts 'summary.json') -Encoding utf8
    $markdown = Write-SummaryMarkdown -Summary $summary -VisualComparisons $visual
    Set-Content -LiteralPath (Join-Path $script:Artifacts 'summary.md') -Value $markdown -Encoding utf8
    Write-Output "regenerated summary at $($script:Artifacts)"
    exit 0
}

Assert-Preconditions
Initialize-VersionCounter
Close-OrphanDevProcesses
New-Item -ItemType Directory -Force -Path $script:Artifacts | Out-Null

foreach ($variant in $Variants) {
    if ($variant -notin $script:SwitchOrder) {
        throw "Unknown variant '$variant'. Known: $($script:SwitchOrder -join ', ')."
    }
}

if (-not $SkipProbe) {
    Write-Output 'Building probe...'
    & dotnet build $script:ProbeProject -c Release -v:q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Probe build failed.'
    }
}

foreach ($variant in $Variants) {
    Invoke-ProductionBuild -Variant $variant
}

if (-not $SkipProbe) {
    foreach ($variant in $Variants) {
        Invoke-Probe -Variant $variant
    }
}

# 交错顺序：每一轮把上一轮最后一个变体放在最前，避免边界处额外重装。
for ($round = 1; $round -le $Runs; $round++) {
    $offset = ($Variants.Count - (($round - 1) % $Variants.Count)) % $Variants.Count
    $order = for ($index = 0; $index -lt $Variants.Count; $index++) {
        $Variants[($index + $offset) % $Variants.Count]
    }

    foreach ($variant in $order) {
        if ($script:InstalledVariant -ne $variant) {
            Install-DevPackage -Variant $variant | Out-Null
            Invoke-WarmupLaunch -Variant $variant
        }

        $record = Invoke-MeasuredRun -Variant $variant -Round $round
        $script:RunRecords.Add($record)
        $rawDirectory = Join-Path $script:Artifacts ('raw\' + $variant)
        New-Item -ItemType Directory -Force -Path $rawDirectory | Out-Null
        $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rawDirectory ("run-$round.json")) -Encoding utf8
        Write-Output ("run {0} {1}: startup={2} ms, privateWS={3} MB, idleCPU={4}% one core, wake={5} ms" -f
            $variant, $round, $record.startupMs, $record.privateWorkingSetMB, $record.idleCpuPercentOfOneCore, $record.wakeLatencyMedianMs)
    }
}

$summary = New-Summary
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:Artifacts 'summary.json') -Encoding utf8

if (-not $SkipProbe) {
    $visual = New-VisualComparison
    $summary | Add-Member -NotePropertyName visualComparisons -NotePropertyValue $visual -Force
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:Artifacts 'summary.json') -Encoding utf8
}
else {
    $visual = @()
}

$summaryMarkdown = Write-SummaryMarkdown -Summary $summary -VisualComparisons $visual
Set-Content -LiteralPath (Join-Path $script:Artifacts 'summary.md') -Value $summaryMarkdown -Encoding utf8

$finalProduction = @(Get-AppxPackage -Name 'Mang.BiliCopilot*' |
    Where-Object { $_.Name -in $script:ForbiddenPackages } |
    Select-Object Name, Version, PackageFullName)
foreach ($before in $script:ProductionSnapshot) {
    $after = $finalProduction | Where-Object { $_.Name -eq $before.Name } | Select-Object -First 1
    if ($null -eq $after -or $after.Version -ne $before.Version) {
        throw "Production package $($before.Name) changed during the experiment (was $($before.Version))."
    }
}

Write-Output ("done: installed {0} {1}; summary at {2}" -f $script:InstalledVariant, (Get-AppxPackage -Name $script:PackageName).Version, $script:Artifacts)
