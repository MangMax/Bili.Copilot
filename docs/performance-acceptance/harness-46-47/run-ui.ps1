param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][string]$Layout,
    [Parameter(Mandatory)][string]$PreparedProject,
    [Parameter(Mandatory)][string]$Evidence
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$previousWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = 'acceptance-46-47-t3-5b5b1beb'
$checks = [Collections.Generic.List[object]]::new()
$captures = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $Evidence -Force | Out-Null
function Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments -a $AppPid --json
    if ($LASTEXITCODE -ne 0) { throw "winapp ui $Arguments failed: $output" }
    $output
}
function Check([string]$Name, [scriptblock]$Action) {
    try { & $Action; $checks.Add(@{name=$Name; passed=$true}) }
    catch { $checks.Add(@{name=$Name; passed=$false; detail="$_"}) }
}
function Capture([string]$Name) {
    $path = Join-Path $Evidence "$Name.png"
    # Allow the compositor to present the asserted state before capturing its pixels.
    Start-Sleep -Milliseconds 300
    Ui @('screenshot','-w',$script:ownedHwnd,'--output',$path) | Out-Null
    if (-not (Test-Path $path) -or (Get-Item $path).Length -eq 0) { throw 'Missing screenshot' }
    $captures.Add($path)
    Ui @('inspect','--depth','12') | Set-Content (Join-Path $Evidence "$Name.uia.json") -Encoding utf8
}
function Step([string]$Button, [string]$Case, [string]$Shot) {
    Ui @('invoke',$Button) | Out-Null
    Ui @('wait-for','HarnessStatus','--value',"${Case}:",'--contains','-t','5000') | Out-Null
    if ($Shot) { Capture $Shot }
    $status = Ui @('get-value','HarnessStatus') | ConvertFrom-Json
    if ($status.text -notlike "${Case}: PASS*") { throw $status.text }
}
function Flatten($nodes) { foreach ($n in $nodes) { $n; Flatten $n.children } }
try {
    Ui @('wait-for','HarnessStatus','-t','15000') | Out-Null
    $windows = Ui @('list-windows') | ConvertFrom-Json
    if (-not ($windows | Where-Object title -eq 'Offline acceptance 46 47 — eeb28f2')) { throw 'Owned harness title missing' }
    $script:ownedHwnd = [string]($windows | Where-Object title -eq 'Offline acceptance 46 47 — eeb28f2' | Select-Object -First 1).hwnd
    $windows | ConvertTo-Json -Depth 8 | Set-Content "$Evidence/windows.json"
    Check 'InitialTemplateLoaded' { Step Initial InitialTemplateLoaded '00-initial' }
    Check 'Render' { Step Render Render '01-render' }
    Check 'ProductionCodeClipboard' {
        $tree = Ui @('inspect','--depth','12') | ConvertFrom-Json
        $copy = @(Flatten $tree.windows.elements | Where-Object { $_.name -ceq 'Copy' -and $_.type -eq 'Button' })
        if ($copy.Count -ne 1) { throw "Expected one production Copy button, got $($copy.Count)" }
        Ui @('invoke',$copy[0].selector) | Out-Null
        Step VerifyCodeClipboard ProductionCodeClipboard ''
    }
    Check 'Clear' { Step Clear Clear '02-clear' }
    Check 'Reassign' { Step Reassign Reassign '03-reassign' }
    Check 'Detach' { Step Detach Detach '04-detach' }
    Check 'SameInstanceUnchangedTextAttach' { Step Attach SameInstanceUnchangedTextAttach '05-reattach' }
    Check 'LifecycleBoundaries' { Step Lifecycle LifecycleBoundaries '05b-lifecycle' }
    Check 'FreshReopen' { Step Fresh FreshReopen '06-fresh' }
    Check 'CoalescedSelectableStreaming' {
        Step Stream CoalescedSelectableStreaming '07-stream'
        $expected = (Get-Content "$PreparedProject/fixture.md" -Raw)
        $expected = $expected.Substring(0, [int][Math]::Floor($expected.Length / 2))
        $actual = Ui @('get-value','AIStreamingResponse') | ConvertFrom-Json
        if ($actual.text -cne $expected) { throw 'UIA streaming text differs from fixed ordered chunks' }
        Ui @('get-property','AIStreamingResponse') | Set-Content "$Evidence/streaming-properties.json"
    }
    Check 'FinalFlush' { Step Complete FinalFlush '08-final' }
    Check 'AnswerClipboard' { Step CopyAnswer HarnessCopyAnswer ''; Step VerifyAnswerClipboard AnswerClipboard '' }
    Check 'CancelledLateAfterNewCompletion' { Step CancelLate CancelledLateAfterNewCompletion '09-cancel-late' }
    Copy-Item "$Layout/observations.json" "$Evidence/observations.json"
    Check 'CancelledLateBeforeNewCompletion' {
        $observed = @(Get-Content "$Evidence/observations.json" -Raw | ConvertFrom-Json | Where-Object name -eq 'CancelledLateBeforeNewCompletion')
        if ($observed.Count -ne 1 -or -not $observed[0].passed) { throw 'Old output affected the replacement generating state or text.' }
    }
} finally {
    try {
        $ownedProcess = Get-Process -Id $AppPid -ErrorAction SilentlyContinue
        if ($ownedProcess) {
            Ui @('invoke','Close') | Out-Null
            if (-not $ownedProcess.WaitForExit(5000)) { throw 'Owned process did not exit after Close.' }
        }
        $checks.Add(@{name='CloseOwnedWindow'; passed=$true})
    } catch { $checks.Add(@{name='CloseOwnedWindow';passed=$false;detail="$_"}) }
    try {
        foreach ($entry in (Get-Content "$PreparedProject/source-hashes.json" -Raw | ConvertFrom-Json)) {
            if ((Get-FileHash $entry.Path).Hash -ne $entry.Hash) { throw "Source/manifest changed: $($entry.Path)" }
        }
        $checks.Add(@{name='ProductionSourceAndManifestUnchanged';passed=$true})
    } catch { $checks.Add(@{name='ProductionSourceAndManifestUnchanged';passed=$false;detail="$_"}) }
    try {
        $package = Get-AppxPackage -Name BiliCopilot.Acceptance4647
        if ($package) {
            if ($package.InstallLocation -ne [IO.Path]::GetFullPath($Layout)) { throw 'Refusing to remove a package from another layout.' }
            Remove-AppxPackage -Package $package.PackageFullName
        }
        $checks.Add(@{name='RemoveOwnedTemporaryPackage';passed=$true})
    } catch { $checks.Add(@{name='RemoveOwnedTemporaryPackage';passed=$false;detail="$_"}) }
    $yield = & winapp ui yield --json
    $yieldCode = $LASTEXITCODE
    $yield | Set-Content "$Evidence/desktop-release.json"
    $checks.Add(@{name='YieldDesktop';passed=($yieldCode -eq 0)})
    $env:WINAPP_UI_WORKFLOW_ID = $previousWorkflow
    @{appPid=$AppPid; target='local'; checks=$checks.ToArray(); screenshots=$captures.ToArray()} | ConvertTo-Json -Depth 8 | Set-Content "$Evidence/results.json" -Encoding utf8
}
$checks | ForEach-Object { [pscustomobject]$_ } | Format-Table name,passed,detail -AutoSize
if (@($checks | Where-Object { -not $_.passed }).Count) { exit 1 }
