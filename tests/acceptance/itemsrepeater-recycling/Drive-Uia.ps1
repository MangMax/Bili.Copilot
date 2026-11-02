#Requires -Version 5.1
<#
.SYNOPSIS
    UI Automation driver used by Measure.ps1.

.DESCRIPTION
    Runs under Windows PowerShell 5.1 on purpose: that is the in-box interpreter
    that can load the UI Automation client assemblies (UIAutomationClient /
    UIAutomationTypes). pwsh 7 cannot late-bind the UIAutomationCore COM objects
    and has no managed UIA client, so Measure.ps1 shells out to this file.

    Every action prints exactly one JSON object to stdout and exits 0 on success.
    Failures print {"ok":false,"error":"..."} and exit 1, so the caller can fall
    back to synthetic input instead of hanging.

    This file only reads UI Automation data and performs requested UI actions on
    the window handle it is given. It never enumerates or touches other processes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][long]$WindowHandle,
    [Parameter(Mandatory)][ValidateSet('snapshot', 'select', 'scroll', 'scrollinfo')][string]$Action,
    [int]$Index = -1,
    [int]$ScrollCount = 1,
    [ValidateSet('down', 'up')][string]$ScrollDirection = 'down',
    [double]$ScrollPercent = -1
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiaDriveNative {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
}
'@

function Write-Result {
    param([hashtable]$Payload)
    $Payload['ok'] = $true
    Write-Output ($Payload | ConvertTo-Json -Depth 6 -Compress)
    exit 0
}

function Write-Failure {
    param([string]$Message)
    Write-Output (@{ ok = $false; error = $Message } | ConvertTo-Json -Compress)
    exit 1
}

function Get-Rect {
    param([System.Windows.Automation.AutomationElement]$Element)
    try {
        $r = $Element.Current.BoundingRectangle
        if ($r.IsEmpty) { return $null }
        return @{ x = [int]$r.X; y = [int]$r.Y; width = [int]$r.Width; height = [int]$r.Height }
    }
    catch { return $null }
}

function Invoke-CenterClick {
    param([System.Windows.Automation.AutomationElement]$Element)
    $rect = Get-Rect -Element $Element
    if ($null -eq $rect) { return $false }
    $x = $rect.x + [int]($rect.width / 2)
    $y = $rect.y + [int]($rect.height / 2)
    [void][UiaDriveNative]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 60
    [UiaDriveNative]::mouse_event([UiaDriveNative]::LeftDown, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [UiaDriveNative]::mouse_event([UiaDriveNative]::LeftUp, 0, 0, 0, [UIntPtr]::Zero)
    return $true
}

try {
    $window = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$WindowHandle)
    if ($null -eq $window) { Write-Failure 'AutomationElement.FromHandle returned null.' }
    $windowRect = Get-Rect -Element $window

    if ($Action -eq 'snapshot') {
        $items = @()
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem)
        $found = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        for ($i = 0; $i -lt $found.Count; $i++) {
            $element = $found.Item($i)
            $rect = Get-Rect -Element $element
            if ($null -eq $rect) { continue }
            # Navigation items live in a narrow left rail; page list rows are wide.
            if ($null -ne $windowRect) {
                if ($rect.width -gt [int]($windowRect.width * 0.4)) { continue }
                if ($rect.x -gt ($windowRect.x + [int]($windowRect.width * 0.35))) { continue }
            }
            $items += @{
                index        = $items.Count
                name         = [string]$element.Current.Name
                automationId = [string]$element.Current.AutomationId
                className    = [string]$element.Current.ClassName
                rect         = $rect
            }
        }

        Write-Result @{
            windowName = [string]$window.Current.Name
            className  = [string]$window.Current.ClassName
            rect       = $windowRect
            navItems   = $items
        }
    }

    if ($Action -eq 'select') {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem)
        $found = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $candidates = @()
        for ($i = 0; $i -lt $found.Count; $i++) {
            $element = $found.Item($i)
            $rect = Get-Rect -Element $element
            if ($null -eq $rect) { continue }
            if ($null -ne $windowRect) {
                if ($rect.width -gt [int]($windowRect.width * 0.4)) { continue }
                if ($rect.x -gt ($windowRect.x + [int]($windowRect.width * 0.35))) { continue }
            }
            $candidates += , @{ element = $element; rect = $rect; name = [string]$element.Current.Name }
        }

        if ($Index -lt 0 -or $Index -ge $candidates.Count) {
            Write-Failure ("Nav item index {0} is out of range (found {1} candidates)." -f $Index, $candidates.Count)
        }

        $target = $candidates[$Index]
        $mode = 'none'
        $pattern = $null
        if ($target.element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $pattern.Select()
            $mode = 'selection-item'
        }
        elseif ($target.element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke()
            $mode = 'invoke'
        }
        else {
            if (-not (Invoke-CenterClick -Element $target.element)) { Write-Failure 'Nav item has no invokable pattern and no screen bounds.' }
            $mode = 'click'
        }

        Start-Sleep -Milliseconds 400
        Write-Result @{
            index       = $Index
            name        = $target.name
            mode        = $mode
            selectedNow = $(try { $target.element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } catch { $null })
            candidateCount = $candidates.Count
        }
    }

    # scroll / scrollinfo: pick the widest vertically scrollable element in the window.
    $scrollCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty,
        $true)
    $scrollable = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $scrollCondition)
    $best = $null
    $bestArea = 0
    for ($i = 0; $i -lt $scrollable.Count; $i++) {
        $element = $scrollable.Item($i)
        $rect = Get-Rect -Element $element
        if ($null -eq $rect) { continue }
        $area = $rect.width * $rect.height
        if ($area -le $bestArea) { continue }
        try {
            $scrollPattern = $element.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
            if (-not $scrollPattern.Current.VerticallyScrollable) { continue }
        }
        catch { continue }
        $bestArea = $area
        $best = @{ element = $element; rect = $rect }
    }

    if ($null -eq $best) { Write-Failure 'No vertically scrollable element found in the window.' }

    $pattern = $best.element.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    $before = [pscustomobject]@{
        verticalPercent    = $pattern.Current.VerticalScrollPercent
        verticalViewSize   = $pattern.Current.VerticalViewSize
        horizontallyScrollable = $pattern.Current.HorizontallyScrollable
    }

    if ($Action -eq 'scrollinfo') {
        Write-Result @{
            before = $before
            after  = $before
            rect   = $best.rect
            steps  = 0
        }
    }

    if ($Action -eq 'scroll') {
        if ($ScrollPercent -ge 0) {
            $pattern.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $ScrollPercent)
            $steps = 0
        }
        else {
            $amount = if ($ScrollDirection -eq 'down') { [System.Windows.Automation.ScrollAmount]::LargeIncrement } else { [System.Windows.Automation.ScrollAmount]::LargeDecrement }
            $steps = 0
            for ($i = 0; $i -lt $ScrollCount; $i++) {
                $pattern.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount, $amount)
                $steps++
                Start-Sleep -Milliseconds 40
            }
        }

        Start-Sleep -Milliseconds 500
        $after = [pscustomobject]@{
            verticalPercent  = $pattern.Current.VerticalScrollPercent
            verticalViewSize = $pattern.Current.VerticalViewSize
        }
        Write-Result @{
            before = $before
            after  = $after
            rect   = $best.rect
            steps  = $steps
        }
    }

    Write-Failure 'Unhandled action.'
}
catch {
    Write-Failure ($_.Exception.Message)
}
