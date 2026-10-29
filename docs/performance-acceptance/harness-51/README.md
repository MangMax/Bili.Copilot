# Offline real-control experiment for #51

Run only with exclusive ownership of the unlocked desktop. No API/authentication is used. This harness compiles the unchanged production AppSearchBox XAML/code-behind, SearchBoxViewModel partials, item VM, selector, converter and locale extension. It references the actual Richasy.WinUIKernel.Share 0.0.12-preview3 base classes and real bili-kernel ISearchService/models at the integration gitlink. It does not simulate the lifecycle base or copy search logic.

The composition root supplies one real VM, a manually completed search-service proxy, fixture settings/localization, and a **navigation recorder**, not real pages. App-global hot-search styles/converters are minimal fixtures; hot search is outside this experiment. TimeProvider is the real system provider. Response completion deliberately ignores cancellation. Requests, token cancellation, completion, TextChanged reason, composition events, collection/popup/lifecycle state and one Rendering notification per changed state are logged with a monotonic clock. Rendering is a UI render callback, not a GPU presentation measurement.

Use already installed SDK, cached packages/runtime and winapp; no installs or system-policy changes:

```powershell
$source=(Get-Location).Path
$temp=Join-Path $env:TEMP 'bili51-new-owned-run' # must not exist
$layout=Join-Path $env:TEMP 'bili51-new-owned-layout'
& docs/performance-acceptance/harness-51/prepare.ps1 -SourceRoot $source -Destination $temp
dotnet build "$temp/Harness.csproj" -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
if($LASTEXITCODE){throw 'Build failed'}
$launch=winapp run "$temp/Harness.csproj" --no-build --arch x64 -p Platform=x64 --output-appx-directory $layout --detach --json | ConvertFrom-Json
if($launch.Error -or -not $launch.ProcessId){throw 'Launch failed'}
# Fresh process: smallest regression, expected exit 1 at cd7811f.
& docs/performance-acceptance/harness-51/repro-unload.ps1 -AppPid $launch.ProcessId -Layout $layout -Evidence ./repro-new
# Preserve exit and results before cleanup, including expected production failures.
& docs/performance-acceptance/harness-51/cleanup.ps1 -AppPid $launch.ProcessId -Layout $layout -Evidence ./repro-new
```

Never reuse the package name if another owner is using it. `run.ps1` is the initial seven-case suite, `boundaries.ps1` adds distinct timing/lifecycle/region cases; each expects a fresh process and initially Chinese IME mode, and switches only the focused input with Shift. `followup.ps1` continues the terminal IME popup from the boundary suite and preserves failing production assertions. If that preceding suite already submitted the IME suggestion, do not run that follow-up. Do not blindly rerun a suite in dirty state. Input-mode mismatch must be reported as a failed precondition, not hidden by retries.

UIA is used for actual input, tree inspection and capture. The rapid-five-event test uses guarded Win32 SendInput virtual keys so CLI startup cannot create gaps beyond 250ms. IME evidence uses virtual keys through the installed IME, never UIA SetValue. A named pipe controls only fixture response completion, detach/attach, region setup and observation markers, avoiding the focus theft of UIA Invoke on a response button. Each wait is bounded at five seconds. The 850ms timer is an explicit negative-observation window covering both 250ms debounce and 600ms submission suppression; it is held strongly by the host.

All screenshot outputs need visual review. The runner's boolean results do not automatically approve pixels or authenticated navigation. Read [ui-51.md](../ui-51.md) for the production failure, all retained raw failures and evidence limits.
