#Requires -Version 7.0
<#
.SYNOPSIS
    Measures whether the ItemsRepeater recycling defect is observable in the
    BiliCopilot Dev package: long-list scrolling, page switching and teardown,
    sampled at fixed cycle boundaries.

.DESCRIPTION
    Runs a fixed, repeatable scenario against the installed Mang.BiliCopilot.Dev
    package and records memory / handle / CPU counters at cycle boundaries plus
    fixed screenshots. The growth arithmetic is in GrowthTrend.ps1 (unit tested
    by GrowthTrend.Tests.ps1); this file only drives the app and collects data.

    Safety contract (never relaxed):
      * only the Mang.BiliCopilot.Dev package is ever launched;
      * the executable path is verified against that package's InstallLocation;
      * Mang.BiliCopilot / Mang.BiliCopilot.AotDev processes are never started,
        activated, minimized, closed or otherwise touched;
      * every process this script starts is closed in a finally block;
      * the user's window geometry is restored before the script exits.

    Output lands in artifacts/itemsrepeater-recycling/<runId>/ (gitignored).

.EXAMPLE
    pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -DryRun

.EXAMPLE
    pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -Cycles 6
#>
[CmdletBinding()]
param(
    # Single allowed value: this script must never point at another identity.
    [ValidateSet('Mang.BiliCopilot.Dev')]
    [string]$PackageName = 'Mang.BiliCopilot.Dev',

    [ValidateRange(3, 40)]
    [int]$Cycles = 6,

    [ValidateRange(0, 5)]
    [int]$WarmupCycles = 2,

    [ValidateRange(600, 4000)]
    [int]$WindowWidth = 1440,

    [ValidateRange(400, 2400)]
    [int]$WindowHeight = 900,

    [ValidateRange(1, 6)]
    [int]$WindowX = 80,

    [ValidateRange(1, 6)]
    [int]$WindowY = 80,

    [ValidateRange(0, 400)]
    [int]$WheelNotches = 120,

    [ValidateRange(2, 8)]
    [int]$ScrollCyclesPerPageSwitch = 1,

    # NavigationView rail items are selected by index, not by localized name.
    [ValidateRange(2, 6)]
    [int]$NavItemCount = 2,

    # Opt-in: open and close the detached player window N times to exercise a
    # real XAML tree teardown (main-window page switches do not tear down).
    [ValidateRange(0, 10)]
    [int]$PlayerWindowCycles = 0,

    [ValidateRange(0.1, 0.9)]
    [double]$PlayerCardXPercent = 0.5,

    [ValidateRange(0.1, 0.9)]
    [double]$PlayerCardYPercent = 0.6,

    [string]$ArtifactRoot,

    [switch]$SkipScreenshots,
    [switch]$NoResize,
    [switch]$KeepRunning,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'GrowthTrend.ps1')

$script:Identity        = 'Mang.BiliCopilot.Dev'
$script:ForbiddenIdentities = @('Mang.BiliCopilot', 'Mang.BiliCopilot.AotDev')
$script:ExeName         = 'BiliCopilot.UI.exe'
$script:RepoRoot        = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$script:ArtifactRoot    = if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    Join-Path $script:RepoRoot 'artifacts\itemsrepeater-recycling'
} else {
    [IO.Path]::GetFullPath($ArtifactRoot)
}
$script:RunId           = (Get-Date).ToString('yyyyMMdd-HHmmss')
$script:RunDir          = Join-Path $script:ArtifactRoot $script:RunId
$script:Log             = [System.Collections.Generic.List[object]]::new()
$script:OwnedProcesses  = [System.Collections.Generic.List[int]]::new()
$script:Notes           = [System.Collections.Generic.List[string]]::new()
$script:OriginalWindowRect = $null

if ($PackageName -ne $script:Identity) {
    throw "Refusing to run: package '$PackageName' is not '$($script:Identity)'."
}

# ---------------------------------------------------------------- native interop

if (-not ('RepeaterAcceptanceNative' -as [type])) {
    Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class RepeaterAcceptanceNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder buffer, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder buffer, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);

    public const uint WM_CLOSE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    private static bool IsAppWindow(IntPtr hWnd, uint processId)
    {
        uint owner;
        GetWindowThreadProcessId(hWnd, out owner);
        if (owner != processId || !IsWindowVisible(hWnd)) { return false; }
        var sb = new StringBuilder(256);
        GetClassNameW(hWnd, sb, sb.Capacity);
        var name = sb.ToString();
        return name.Contains("WinUIDesktopWin32WindowClass") || name.Contains("Microsoft.UI.Windowing.Window");
    }

    public static IntPtr FindMainWindow(uint processId)
    {
        IntPtr best = IntPtr.Zero;
        int bestArea = 0;
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsAppWindow(hWnd, processId)) { return true; }
            RECT rect;
            if (!GetWindowRect(hWnd, out rect)) { return true; }
            int area = (rect.Right - rect.Left) * (rect.Bottom - rect.Top);
            if (area > bestArea) { bestArea = area; best = hWnd; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    public static IntPtr[] FindAppWindows(uint processId)
    {
        var list = new List<IntPtr>();
        EnumWindows((hWnd, lParam) =>
        {
            if (IsAppWindow(hWnd, processId)) { list.Add(hWnd); }
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }

    public static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        GetWindowTextW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetWindowClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassNameW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
'@
}

$script:DpiAwarenessSet = [RepeaterAcceptanceNative]::SetProcessDpiAwarenessContext([IntPtr](-4))

# ------------------------------------------------------------------- utilities

function Add-Log {
    param([string]$Step, [hashtable]$Data = @{})
    $entry = [pscustomobject]@{
        at    = (Get-Date).ToString('o')
        step  = $Step
        data  = [pscustomobject]$Data
    }
    $script:Log.Add($entry)
    Write-Host ("[{0}] {1}" -f (Get-Date).ToString('HH:mm:ss'), $Step)
}

function Get-BytesToMb {
    param([double]$Bytes)
    return [math]::Round($Bytes / 1MB, 2)
}

function Get-AmbientSnapshot {
    $bili = @(Get-Process -Name 'BiliCopilot.UI' -ErrorAction SilentlyContinue)
    $systemCpu = $null
    try {
        $cpu = Get-CimInstance -ClassName Win32_PerfFormattedData_PerfOS_Processor -Filter "Name = '_Total'" -ErrorAction Stop
        $systemCpu = [double]$cpu.PercentProcessorTime
    }
    catch {
        $systemCpu = $null
    }

    return [pscustomobject]@{
        biliProcessCount        = $bili.Count
        biliProcessIds          = @($bili | ForEach-Object { $_.Id })
        biliProcessPaths        = @($bili | ForEach-Object { $_.Path })
        systemCpuPercent        = $systemCpu
        capturedAt              = (Get-Date).ToString('o')
    }
}

function Get-AppMetrics {
    param([System.Diagnostics.Process]$Process)

    $Process.Refresh()
    $perf = $null
    try {
        $perf = Get-CimInstance -ClassName Win32_PerfRawData_PerfProc_Process -Filter "IDProcess = $($Process.Id)" -ErrorAction Stop |
            Select-Object -First 1
    }
    catch {
        $perf = $null
    }

    $workingSetPrivateMb = $null
    if ($null -ne $perf) {
        $workingSetPrivateMb = [math]::Round([double]$perf.WorkingSetPrivate / 1KB, 2)
    }

    return [pscustomobject]@{
        processId            = $Process.Id
        privateCommitMb      = Get-BytesToMb -Bytes $Process.PrivateMemorySize64
        workingSetMb         = Get-BytesToMb -Bytes $Process.WorkingSet64
        workingSetPrivateMb  = $workingSetPrivateMb
        pagedMemoryMb        = Get-BytesToMb -Bytes $Process.PagedMemorySize64
        handleCount          = $Process.HandleCount
        threadCount          = $Process.Threads.Count
        cpuSeconds           = [math]::Round($Process.TotalProcessorTime.TotalSeconds, 2)
        timestamp            = (Get-Date).ToString('o')
    }
}

function Wait-AppSettled {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$MinimumWaitMs = 1500,
        [int]$QuietWindowMs = 1200,
        [int]$TimeoutMs = 40000,
        [double]$CpuDeltaSeconds = 0.15
    )

    Start-Sleep -Milliseconds $MinimumWaitMs
    $started = [DateTime]::UtcNow
    $previous = $Process.TotalProcessorTime.TotalSeconds
    while (([DateTime]::UtcNow - $started).TotalMilliseconds -lt $TimeoutMs) {
        Start-Sleep -Milliseconds $QuietWindowMs
        if ($Process.HasExited) { return $false }
        $current = $Process.TotalProcessorTime.TotalSeconds
        if (($current - $previous) -lt $CpuDeltaSeconds) { return $true }
        $previous = $current
    }

    return $false
}

function Invoke-UiaHelper {
    param(
        [long]$WindowHandle,
        [string]$Action,
        [int]$Index = -1,
        [int]$ScrollCount = 1,
        [string]$ScrollDirection = 'down',
        [double]$ScrollPercent = -1
    )

    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path $powershell)) { return $null }

    $helper = Join-Path $PSScriptRoot 'Drive-Uia.ps1'
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $helper, '-WindowHandle', $WindowHandle, '-Action', $Action)
    if ($Index -ge 0) { $arguments += @('-Index', $Index) }
    if ($Action -eq 'scroll') {
        $arguments += @('-ScrollCount', $ScrollCount, '-ScrollDirection', $ScrollDirection)
        if ($ScrollPercent -ge 0) { $arguments += @('-ScrollPercent', $ScrollPercent) }
    }

    $raw = & $powershell @arguments 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($raw -join ''))) { return $null }
    try {
        return (($raw -join '') | ConvertFrom-Json)
    }
    catch {
        return $null
    }
}

function Invoke-WheelScroll {
    param(
        [IntPtr]$WindowHandle,
        [int]$Notches,
        [ValidateSet('down', 'up')][string]$Direction,
        [int]$CenterX,
        [int]$CenterY
    )

    if ($Notches -le 0) { return }
    $origin = New-Object RepeaterAcceptanceNative+POINT
    [void][RepeaterAcceptanceNative]::GetCursorPos([ref]$origin)
    try {
        [void][RepeaterAcceptanceNative]::SetForegroundWindow($WindowHandle)
        Start-Sleep -Milliseconds 120
        [void][RepeaterAcceptanceNative]::SetCursorPos($CenterX, $CenterY)
        Start-Sleep -Milliseconds 60
        $delta = if ($Direction -eq 'down') { -120 } else { 120 }
        for ($i = 0; $i -lt $Notches; $i++) {
            [RepeaterAcceptanceNative]::mouse_event([RepeaterAcceptanceNative]::MOUSEEVENTF_WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 22
        }
    }
    finally {
        [void][RepeaterAcceptanceNative]::SetCursorPos($origin.X, $origin.Y)
    }
}

function Save-WindowScreenshot {
    param([IntPtr]$WindowHandle, [string]$Path)

    try {
        if (-not ('System.Drawing.Bitmap' -as [type])) { Add-Type -AssemblyName System.Drawing.Common }
        $rect = New-Object RepeaterAcceptanceNative+RECT
        if (-not [RepeaterAcceptanceNative]::GetWindowRect($WindowHandle, [ref]$rect)) { return $false }
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        if ($width -le 0 -or $height -le 0) { return $false }

        $bitmap = New-Object System.Drawing.Bitmap($width, $height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $hdc = $graphics.GetHdc()
            try {
                [void][RepeaterAcceptanceNative]::PrintWindow($WindowHandle, $hdc, 2)
            }
            finally {
                $graphics.ReleaseHdc($hdc)
            }
        }
        finally {
            $graphics.Dispose()
        }

        $directory = Split-Path -Parent $Path
        if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        return $true
    }
    catch {
        Add-Log -Step 'screenshot-failed' -Data @{ path = $Path; error = $_.Exception.Message }
        return $false
    }
}

function Close-OwnedApp {
    param([System.Diagnostics.Process]$Process, [int]$TimeoutMs = 25000)

    if ($null -eq $Process) { return }
    try {
        if ($Process.HasExited) { return }
        Add-Log -Step 'close-main-window' -Data @{ processId = $Process.Id }
        [void]$Process.CloseMainWindow()
    }
    catch {
        Add-Log -Step 'close-main-window-failed' -Data @{ error = $_.Exception.Message }
    }

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            if ($Process.HasExited) { return }
            $Process.WaitForExit(500) | Out-Null
        }
        catch { return }
    }

    try {
        if (-not $Process.HasExited) {
            Add-Log -Step 'force-stop-owned-process' -Data @{ processId = $Process.Id }
            Stop-Process -Id $Process.Id -Force -ErrorAction Stop
            $Process.WaitForExit(5000) | Out-Null
        }
    }
    catch {
        Add-Log -Step 'force-stop-failed' -Data @{ error = $_.Exception.Message }
    }
}

function Invoke-PlayerWindowCycle {
    <#
    .SYNOPSIS
        Opens and closes the detached player window once.

    .DESCRIPTION
        The main window's MainFrame never destroys page instances, so navigating
        between pages does not tear down an ItemsRepeater. Opening the player
        window creates a second XAML tree and closing it destroys that tree, which
        is the only repeatable teardown path inside one app session.

        Best effort: if no second window appears (for example because the card
        opened in-place), the cycle is recorded as failed and the run continues.
    #>
    param(
        [System.Diagnostics.Process]$Process,
        [IntPtr]$Window,
        [int]$Left,
        [int]$Top,
        [int]$Right,
        [int]$Bottom
    )

    $before = @([RepeaterAcceptanceNative]::FindAppWindows([uint32]$Process.Id))
    $x = $Left + [int](($Right - $Left) * $PlayerCardXPercent)
    $y = $Top + [int](($Bottom - $Top) * $PlayerCardYPercent)

    [void][RepeaterAcceptanceNative]::SetForegroundWindow($Window)
    Start-Sleep -Milliseconds 150
    [void][RepeaterAcceptanceNative]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 100
    for ($click = 0; $click -lt 2; $click++) {
        [RepeaterAcceptanceNative]::mouse_event([RepeaterAcceptanceNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 40
        [RepeaterAcceptanceNative]::mouse_event([RepeaterAcceptanceNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 90
    }

    $opened = [IntPtr]::Zero
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline) {
        $now = @([RepeaterAcceptanceNative]::FindAppWindows([uint32]$Process.Id))
        $new = @($now | Where-Object { $before -notcontains $_ })
        if ($new.Count -gt 0) { $opened = $new[0]; break }
        if ($Process.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }

    if ($opened -eq [IntPtr]::Zero) {
        Add-Log -Step 'player-window-missing' -Data @{ point = @($x, $y) }
        return [pscustomobject]@{ opened = $false; closed = $false; point = @($x, $y) }
    }

    Add-Log -Step 'player-window-opened' -Data @{ handle = $opened.ToInt64() }
    $null = Wait-AppSettled -Process $Process -MinimumWaitMs 3000 -QuietWindowMs 1500 -TimeoutMs 30000 -CpuDeltaSeconds 0.4
    [void][RepeaterAcceptanceNative]::PostMessageW($opened, [RepeaterAcceptanceNative]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)

    $closed = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline) {
        $now = @([RepeaterAcceptanceNative]::FindAppWindows([uint32]$Process.Id))
        if ($now -notcontains $opened) { $closed = $true; break }
        Start-Sleep -Milliseconds 200
    }

    Add-Log -Step 'player-window-closed' -Data @{ handle = $opened.ToInt64(); closed = $closed }
    $null = Wait-AppSettled -Process $Process -MinimumWaitMs 1200 -QuietWindowMs 1000 -TimeoutMs 20000 -CpuDeltaSeconds 0.25
    return [pscustomobject]@{ opened = $true; closed = $closed; point = @($x, $y); handle = $opened.ToInt64() }
}

# ------------------------------------------------------------------ preflight

function Resolve-DevPackage {
    $package = Get-AppxPackage -Name $script:Identity -ErrorAction SilentlyContinue
    if ($null -eq $package) {
        throw "Package '$($script:Identity)' is not installed. Install the Dev build first (see README)."
    }
    if ($package.Name -ne $script:Identity) {
        throw "Resolved package '$($package.Name)' is not '$($script:Identity)'."
    }

    # Exact identity comparison only: 'Mang.BiliCopilot.Dev' legitimately starts
    # with 'Mang.BiliCopilot', so a prefix match here would reject the Dev package.
    foreach ($forbidden in $script:ForbiddenIdentities) {
        if ($package.Name -ieq $forbidden) {
            throw "Resolved package matches forbidden identity '$forbidden'."
        }
    }

    $exe = Join-Path $package.InstallLocation $script:ExeName
    if (-not (Test-Path $exe)) {
        throw "Executable not found at '$exe'."
    }

    $dependencies = @()
    try {
        $dependencies = @($package.Dependencies | ForEach-Object {
            [pscustomobject]@{ name = $_.Name; version = $_.Version.ToString() }
        })
    }
    catch {
        $dependencies = @()
    }

    return [pscustomobject]@{
        name            = $package.Name
        fullName        = $package.PackageFullName
        version         = $package.Version.ToString()
        familyName      = $package.PackageFamilyName
        installLocation = $package.InstallLocation
        executable      = $exe
        dependencies    = $dependencies
    }
}

function Assert-SafeLaunchState {
    param([string]$Executable)

    $all = @(Get-Process -Name 'BiliCopilot.UI' -ErrorAction SilentlyContinue)
    $foreign = @($all | Where-Object { $_.Path -and ($_.Path -ne $Executable) })
    $ours = @($all | Where-Object { $_.Path -eq $Executable })

    if ($ours.Count -gt 0) {
        throw "A '$($script:Identity)' process is already running (id $($ours[0].Id)). Close it before measuring."
    }

    return [pscustomobject]@{
        foreignCount = $foreign.Count
        foreignIds   = @($foreign | ForEach-Object { $_.Id })
        foreignPaths = @($foreign | ForEach-Object { $_.Path })
    }
}

function Get-BuildFingerprint {
    param([string]$Executable, [object]$Package)

    $directory = Split-Path -Parent $Executable
    $xamlDll = Join-Path $directory 'Microsoft.ui.xaml.dll'
    $runtimeDll = Join-Path $directory 'Microsoft.WindowsAppRuntime.dll'

    $fileInfo = Get-Item $Executable

    # A framework-dependent package does not carry the Windows App SDK natives;
    # the framework package dependency is the actual XAML runtime in use.
    $dependencies = @()
    if ($null -ne $Package -and $null -ne $Package.PSObject.Properties['dependencies']) {
        $dependencies = @($Package.dependencies)
    }

    $xamlFramework = @($dependencies | Where-Object { $_.name -like 'Microsoft.WindowsAppRuntime*' })

    $xamlVersion = if (Test-Path $xamlDll) { (Get-Item $xamlDll).VersionInfo.FileVersion } else { $null }
    $runtimeVersion = if (Test-Path $runtimeDll) { (Get-Item $runtimeDll).VersionInfo.FileVersion } else { $null }

    return [pscustomobject]@{
        executable          = $Executable
        executableWritten   = $fileInfo.LastWriteTimeUtc.ToString('o')
        executableSize      = $fileInfo.Length
        fileVersion         = $fileInfo.VersionInfo.FileVersion
        xamlDllVersion      = $xamlVersion
        appRuntimeVersion   = $runtimeVersion
        frameworkDependencies = $dependencies
        windowsAppRuntime   = $xamlFramework
    }
}

function Get-RepositoryFingerprint {
    $commit = (git -C $script:RepoRoot rev-parse HEAD 2>$null)
    $branch = (git -C $script:RepoRoot rev-parse --abbrev-ref HEAD 2>$null)
    $dirty = @(git -C $script:RepoRoot status --porcelain 2>$null)
    return [pscustomobject]@{
        commit = if ($commit) { $commit.Trim() } else { $null }
        branch = if ($branch) { $branch.Trim() } else { $null }
        dirty  = $dirty.Count -gt 0
    }
}

$package = Resolve-DevPackage
$safety = Assert-SafeLaunchState -Executable $package.executable
$build = Get-BuildFingerprint -Executable $package.executable -Package $package
$repository = Get-RepositoryFingerprint

$preflight = [pscustomobject]@{
    package     = $package
    build       = $build
    repository  = $repository
    foreignBili = $safety
    dpiAwarenessApplied = [bool]$script:DpiAwarenessSet
    powershellVersion = $PSVersionTable.PSVersion.ToString()
    osVersion   = [Environment]::OSVersion.VersionString
    windowPlan  = [pscustomobject]@{ width = $WindowWidth; height = $WindowHeight; x = $WindowX; y = $WindowY }
    cycles      = $Cycles
    warmupCycles = $WarmupCycles
    wheelNotches = $WheelNotches
    playerWindowCycles = $PlayerWindowCycles
}

Add-Log -Step 'preflight' -Data @{
    package = $package.fullName
    version = $package.version
    exe     = $package.executable
    foreignBiliProcesses = $safety.foreignCount
}

if ($DryRun) {
    $preflight | ConvertTo-Json -Depth 6
    Write-Host 'Dry run: preflight passed, nothing was launched.'
    return
}

if (-not (Test-Path $script:RunDir)) {
    New-Item -ItemType Directory -Path $script:RunDir -Force | Out-Null
}
$screenshotDir = Join-Path $script:RunDir 'screenshots'
if (-not $SkipScreenshots -and -not (Test-Path $screenshotDir)) {
    New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
}

$lockPath = Join-Path $script:ArtifactRoot '.measure.lock'
$lockStream = $null
if (-not (Test-Path $script:ArtifactRoot)) { New-Item -ItemType Directory -Path $script:ArtifactRoot -Force | Out-Null }
try {
    $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
}
catch {
    throw "Another measurement is running (lock file '$lockPath'). Remove it only if you are sure no run is active."
}

$process = $null
$samples = [System.Collections.Generic.List[object]]::new()
$cyclesRecord = [System.Collections.Generic.List[object]]::new()
$screenshots = [System.Collections.Generic.List[string]]::new()
$verdict = $null

try {
    Add-Log -Step 'launch' -Data @{ executable = $package.executable }
    $process = Start-Process -FilePath $package.executable -PassThru
    $script:OwnedProcesses.Add($process.Id)

    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $window = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw "The app exited during startup (exit code $($process.ExitCode))." }
        $window = [RepeaterAcceptanceNative]::FindMainWindow([uint32]$process.Id)
        if ($window -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    }
    if ($window -eq [IntPtr]::Zero) { throw 'The app window did not appear within 120 s.' }
    Add-Log -Step 'window-found' -Data @{ handle = $window.ToInt64(); title = [RepeaterAcceptanceNative]::GetWindowTitle($window) }

    Start-Sleep -Milliseconds 2500
    $window = [RepeaterAcceptanceNative]::FindMainWindow([uint32]$process.Id)
    $settled = Wait-AppSettled -Process $process
    Add-Log -Step 'startup-settled' -Data @{ settled = $settled }

    $rect = New-Object RepeaterAcceptanceNative+RECT
    [void][RepeaterAcceptanceNative]::GetWindowRect($window, [ref]$rect)
    $script:OriginalWindowRect = [pscustomobject]@{ left = $rect.Left; top = $rect.Top; right = $rect.Right; bottom = $rect.Bottom }

    if (-not $NoResize) {
        [void][RepeaterAcceptanceNative]::ShowWindow($window, 9)
        Start-Sleep -Milliseconds 300
        [void][RepeaterAcceptanceNative]::SetWindowPos(
            $window, [IntPtr]::Zero, $WindowX, $WindowY, $WindowWidth, $WindowHeight,
            ([RepeaterAcceptanceNative]::SWP_NOZORDER -bor [RepeaterAcceptanceNative]::SWP_NOACTIVATE))
        Start-Sleep -Milliseconds 800
        [void][RepeaterAcceptanceNative]::GetWindowRect($window, [ref]$rect)
        Add-Log -Step 'window-resized' -Data @{ width = ($rect.Right - $rect.Left); height = ($rect.Bottom - $rect.Top) }
    }

    $dpi = [RepeaterAcceptanceNative]::GetDpiForWindow($window)
    Add-Log -Step 'dpi' -Data @{ dpi = $dpi; scale = [math]::Round($dpi / 96.0, 3) }

    $uiaSnapshot = Invoke-UiaHelper -WindowHandle $window.ToInt64() -Action 'snapshot'
    if ($null -ne $uiaSnapshot) {
        Add-Log -Step 'uia-snapshot' -Data @{ windowName = $uiaSnapshot.windowName; navItems = @($uiaSnapshot.navItems).Count }
    }
    else {
        $script:Notes.Add('UI Automation snapshot failed; navigation will fall back to synthetic clicks.')
        Add-Log -Step 'uia-snapshot-failed'
    }

    [void][RepeaterAcceptanceNative]::GetWindowRect($window, [ref]$rect)
    $centerX = $rect.Left + [int](($rect.Right - $rect.Left) * 0.55)
    $centerY = $rect.Top + [int](($rect.Bottom - $rect.Top) * 0.55)

    if (-not $SkipScreenshots) {
        $path = Join-Path $screenshotDir 'baseline.png'
        if (Save-WindowScreenshot -WindowHandle $window -Path $path) { $screenshots.Add($path) }
    }

    $cycleCount = $WarmupCycles + $Cycles
    for ($cycle = 1; $cycle -le $cycleCount; $cycle++) {
        $isWarmup = $cycle -le $WarmupCycles
        $measuredCycle = if ($isWarmup) { $null } else { $cycle - $WarmupCycles }
        $label = if ($isWarmup) { "warmup-$cycle" } else { "cycle-$measuredCycle" }

        if (-not $isWarmup) { $samples.Add((Get-AppMetrics -Process $process | Add-Member -NotePropertyName phase -NotePropertyValue "$label-start" -PassThru)) }

        # 1. long-list scroll down/up, which is what drives ItemsRepeater recycling.
        $scrollBefore = Invoke-UiaHelper -WindowHandle $window.ToInt64() -Action 'scrollinfo'
        for ($pass = 0; $pass -lt $ScrollCyclesPerPageSwitch; $pass++) {
            Invoke-WheelScroll -WindowHandle $window -Notches $WheelNotches -Direction 'down' -CenterX $centerX -CenterY $centerY
            Start-Sleep -Milliseconds 400
            Invoke-WheelScroll -WindowHandle $window -Notches $WheelNotches -Direction 'up' -CenterX $centerX -CenterY $centerY
            Start-Sleep -Milliseconds 400
        }
        $scrollAfter = Invoke-UiaHelper -WindowHandle $window.ToInt64() -Action 'scrollinfo'
        $null = Wait-AppSettled -Process $process -MinimumWaitMs 600 -QuietWindowMs 600 -TimeoutMs 15000 -CpuDeltaSeconds 0.2

        if (-not $SkipScreenshots) {
            $path = Join-Path $screenshotDir "$label-after-scroll.png"
            if (Save-WindowScreenshot -WindowHandle $window -Path $path) { $screenshots.Add($path) }
        }

        $scrollDelta = $null
        if ($null -ne $scrollBefore -and $null -ne $scrollAfter) {
            $scrollDelta = [double]$scrollAfter.before.verticalPercent - [double]$scrollBefore.before.verticalPercent
        }

        # 2. page switch and back (MainFrame keeps page instances; this exercises
        #    unload/reload of the repeater's realize pipeline, not teardown).
        $navResults = @()
        for ($nav = 1; $nav -lt $NavItemCount; $nav++) {
            $navResults += Invoke-UiaHelper -WindowHandle $window.ToInt64() -Action 'select' -Index $nav
            $null = Wait-AppSettled -Process $process -MinimumWaitMs 700 -QuietWindowMs 700 -TimeoutMs 20000
        }
        $navResults += Invoke-UiaHelper -WindowHandle $window.ToInt64() -Action 'select' -Index 0
        $null = Wait-AppSettled -Process $process -MinimumWaitMs 900 -QuietWindowMs 700 -TimeoutMs 20000

        if (-not $SkipScreenshots) {
            $path = Join-Path $screenshotDir "$label-after-switch.png"
            if (Save-WindowScreenshot -WindowHandle $window -Path $path) { $screenshots.Add($path) }
        }

        # 3. optional real teardown: open and close the detached player window.
        $playerResult = $null
        if (-not $isWarmup -and $PlayerWindowCycles -gt 0 -and $measuredCycle -le $PlayerWindowCycles) {
            $currentRect = New-Object RepeaterAcceptanceNative+RECT
            [void][RepeaterAcceptanceNative]::GetWindowRect($window, [ref]$currentRect)
            $playerResult = Invoke-PlayerWindowCycle -Process $process -Window $window -Left $currentRect.Left -Top $currentRect.Top -Right $currentRect.Right -Bottom $currentRect.Bottom
        }

        $sample = Get-AppMetrics -Process $process
        $record = [pscustomobject]@{
            cycle            = $measuredCycle
            label            = $label
            warmup           = $isWarmup
            privateCommitMb  = $sample.privateCommitMb
            workingSetMb     = $sample.workingSetMb
            workingSetPrivateMb = $sample.workingSetPrivateMb
            handleCount      = $sample.handleCount
            threadCount      = $sample.threadCount
            cpuSeconds       = $sample.cpuSeconds
            scrollPercentDelta = $scrollDelta
            navResults       = $navResults
            playerWindow     = $playerResult
            timestamp        = $sample.timestamp
            ambient          = Get-AmbientSnapshot
        }
        $cyclesRecord.Add($record)
        if (-not $isWarmup) { $samples.Add($sample) }

        Add-Log -Step "cycle-complete" -Data @{
            cycle = $measuredCycle
            warmup = $isWarmup
            privateCommitMb = $sample.privateCommitMb
            handles = $sample.handleCount
            scrollPercentDelta = $scrollDelta
        }
    }

    $postCycles = Get-AppMetrics -Process $process
    Add-Log -Step 'cycles-complete' -Data @{ privateCommitMb = $postCycles.privateCommitMb }

    if ($null -ne $script:OriginalWindowRect -and -not $NoResize -and -not $process.HasExited) {
        [void][RepeaterAcceptanceNative]::SetWindowPos(
            $window, [IntPtr]::Zero,
            $script:OriginalWindowRect.left, $script:OriginalWindowRect.top,
            $script:OriginalWindowRect.right - $script:OriginalWindowRect.left,
            $script:OriginalWindowRect.bottom - $script:OriginalWindowRect.top,
            ([RepeaterAcceptanceNative]::SWP_NOZORDER -bor [RepeaterAcceptanceNative]::SWP_NOACTIVATE))
        Start-Sleep -Milliseconds 400
    }

    if (-not $KeepRunning) {
        Close-OwnedApp -Process $process -TimeoutMs 25000
    }

    $exitRecord = [pscustomobject]@{
        exited        = $process.HasExited
        exitCode      = if ($process.HasExited) { $process.ExitCode } else { $null }
        remainingWindow = ($window -ne [IntPtr]::Zero) -and [RepeaterAcceptanceNative]::IsWindowVisible($window)
    }

    $measured = @($cyclesRecord | Where-Object { -not $_.warmup })

    $scrollDeltas = @($measured | ForEach-Object { $_.scrollPercentDelta } | Where-Object { $null -ne $_ })
    if (($scrollDeltas.Count -eq 0) -or (@($scrollDeltas | Where-Object { [math]::Abs([double]$_) -gt 0.5 }).Count -eq 0)) {
        $script:Notes.Add('No cycle showed a UI Automation scroll-percent change: wheel input may not have reached the list, so recycle churn is unverified.')
    }

    $playerResults = @($measured | ForEach-Object { $_.playerWindow } | Where-Object { $null -ne $_ })
    if ($playerResults.Count -gt 0 -and @($playerResults | Where-Object { -not $_.opened }).Count -gt 0) {
        $script:Notes.Add('At least one player-window cycle did not open a second window; teardown coverage is incomplete for those cycles.')
    }
    $privateSeries = [double[]]@($measured | ForEach-Object { [double]$_.privateCommitMb })
    $handleSeries = [double[]]@($measured | ForEach-Object { [double]$_.handleCount })

    $verdict = Get-RecyclingVerdict -PrivateCommitMb $privateSeries -HandleCount $handleSeries

    $summary = [pscustomobject]@{
        runId            = $script:RunId
        package          = $package.fullName
        packageVersion   = $package.version
        build            = $build
        repository       = $repository
        cycles           = $Cycles
        warmupCycles     = $WarmupCycles
        verdict          = $verdict
        series           = [pscustomobject]@{
            privateCommitMb = $privateSeries
            handleCount     = $handleSeries
            workingSetPrivateMb = [double[]]@($measured | ForEach-Object { if ($null -eq $_.workingSetPrivateMb) { 0.0 } else { [double]$_.workingSetPrivateMb } })
        }
        exit             = $exitRecord
        notes            = @($script:Notes)
        artifacts        = [pscustomobject]@{
            runDirectory = $script:RunDir
            screenshots  = @($screenshots)
        }
    }

    $raw = [pscustomobject]@{
        preflight = $preflight
        summary   = $summary
        cycles    = @($cyclesRecord)
        samples   = @($samples)
        log       = @($script:Log)
    }

    $raw | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:RunDir 'raw.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:RunDir 'summary.json') -Encoding utf8

    $markdown = New-SummaryMarkdown -Summary $summary
    Set-Content -LiteralPath (Join-Path $script:RunDir 'summary.md') -Value $markdown -Encoding utf8

    Write-Host ''
    Write-Host $markdown
    Write-Host ''
    Write-Host "Artifacts: $($script:RunDir)"
    $summary | ConvertTo-Json -Depth 6
}
finally {
    if ($null -ne $process -and -not $KeepRunning) {
        Close-OwnedApp -Process $process
    }

    # Never lose the event log when a run throws before the artifacts are written.
    try {
        if (Test-Path $script:RunDir) {
            $partialPath = Join-Path $script:RunDir 'run-log.json'
            if (-not (Test-Path $partialPath)) {
                [pscustomobject]@{
                    runId    = $script:RunId
                    notes    = @($script:Notes)
                    cycles   = @($cyclesRecord)
                    log      = @($script:Log)
                    complete = $false
                } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $partialPath -Encoding utf8
            }
        }
    }
    catch {
        Write-Warning "Could not persist the run log: $($_.Exception.Message)"
    }
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
        Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    }
}
