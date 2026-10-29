param(
    [Parameter(Mandatory)][string]$PackageFamilyName,
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][int]$PrimaryProcessId,
    [Parameter(Mandatory)][long]$MainWindowHandle,
    [Parameter(Mandatory)][string]$ResultPath,
    [int]$Cycles = 1
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
    $baseline = @(Get-TestProcesses | ForEach-Object Id)
    for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
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
