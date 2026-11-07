# Reproduces #168 / #170 on the installed development package:
#   直播 -> 网游  (from the recommend feed, i.e. the first partition detail page)
# Parameters let you run the control case: -Partition 单机游戏 (loads successfully).
param(
  [string]$Feature = '直播',
  [string]$Partition = '网游',
  [switch]$Fresh
)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ErrorActionPreference = 'Stop'

function Get-Elements($root) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsControlElementProperty, $true)
  return $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Select-Item([string]$Name) {
  $p = Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue
  if (-not $p) { throw 'BiliCopilot.UI is not running' }
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  foreach ($e in (Get-Elements $root)) {
    if ($e.Current.Name -eq $Name -and $e.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') {
      try { ($e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); return "listitem:$Name" } catch { }
    }
  }
  foreach ($e in (Get-Elements $root)) {
    if ($e.Current.Name -eq $Name) {
      $parent = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e)
      try { ($parent.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); return "parent:$Name" } catch { }
    }
  }
  return "missing:$Name"
}

if ($Fresh) {
  Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
  Start-Process explorer.exe 'shell:AppsFolder\Mang.BiliCopilot.Dev_y1mf40g4r9srg!App'
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $p = Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue
    if ($p -and $p.MainWindowHandle -ne 0) { break }
  }
  Start-Sleep -Seconds 6
}

"feature:   " + (Select-Item $Feature);   Start-Sleep -Seconds 6
"partition: " + (Select-Item $Partition); Start-Sleep -Seconds 10
$alive = [bool](Get-Process BiliCopilot.UI -ErrorAction SilentlyContinue)
"alive after $Partition : $alive"
Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000; StartTime=(Get-Date).AddMinutes(-3)} -ErrorAction SilentlyContinue |
  Where-Object { $_.Message -match 'BiliCopilot' } |
  ForEach-Object { $_.TimeCreated.ToString('HH:mm:ss') + ' ' + (($_.Message.Split([char]10))[0]) }
