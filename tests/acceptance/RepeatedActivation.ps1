param(
    [Parameter(Mandatory)][string]$PackageFamilyName,
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][int]$PrimaryProcessId,
    [Parameter(Mandatory)][long]$MainWindowHandle,
    [Parameter(Mandatory)][string]$ResultPath,
    [ValidateRange(1, 100)][int]$Cycles = 1
)

# Run against an explicitly selected, already launched acceptance package.
# Observe real activation, window restoration and process lifetime; never read app data.
$ErrorActionPreference = 'Stop'
$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ActivationAcceptanceWindow {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
'@
function Get-TestProcesses {
    @(Get-Process -Name BiliCopilot.UI -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $ExecutablePath })
}
$results = [Collections.Generic.List[object]]::new()
try {
    if ((Get-Process -Id $PrimaryProcessId).Path -ne $ExecutablePath) {
        throw 'Primary process does not match the explicit acceptance executable.'
    }
    [uint32]$windowProcessId = 0
    [ActivationAcceptanceWindow]::GetWindowThreadProcessId([IntPtr]$MainWindowHandle, [ref]$windowProcessId) | Out-Null
    if ($windowProcessId -ne $PrimaryProcessId) { throw 'Window does not belong to the selected acceptance process.' }
    $baseline = @(Get-TestProcesses | ForEach-Object Id)
    for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
        # A restored window proves this activation was delivered, even if the
        # secondary exits too quickly to appear in a process enumeration.
        [ActivationAcceptanceWindow]::ShowWindow([IntPtr]$MainWindowHandle, 6) | Out-Null
        $minimizeDeadline = [DateTime]::UtcNow.AddSeconds(5)
        while (-not [ActivationAcceptanceWindow]::IsIconic([IntPtr]$MainWindowHandle)) {
            if ([DateTime]::UtcNow -ge $minimizeDeadline) { throw 'Could not establish the minimized precondition.' }
            Start-Sleep -Milliseconds 100
        }
        Start-Process explorer.exe -ArgumentList "shell:AppsFolder\${PackageFamilyName}!App"
        # Give packaged activation time to start and then require its secondary to leave.
        Start-Sleep -Seconds 2
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $remaining = @(Get-TestProcesses | Where-Object { $_.Id -notin $baseline })
            if (-not $remaining.Count) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
        $primary = Get-Process -Id $PrimaryProcessId
        $restored = [ActivationAcceptanceWindow]::IsWindowVisible([IntPtr]$MainWindowHandle) -and
            -not [ActivationAcceptanceWindow]::IsIconic([IntPtr]$MainWindowHandle)
        $passed = $remaining.Count -eq 0 -and $restored -and $primary.Path -eq $ExecutablePath
        $results.Add([pscustomobject]@{
            name = 'RepeatedActivationRestoresSameWindowAndExitsSecondary'
            cycle = $cycle
            status = $(if ($passed) { 'PASS' } else { 'FAIL' })
            primaryProcessId = $PrimaryProcessId
            mainWindowHandle = $MainWindowHandle
            restored = $restored
            survivingSecondaryIds = @($remaining | ForEach-Object Id)
        })
        if (-not $passed) { throw 'Activation left a secondary process or failed to restore the original window.' }
    }
} finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding utf8
}
$results | ConvertTo-Json -Depth 5
