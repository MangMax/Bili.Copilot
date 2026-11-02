# XAML optional changes acceptance tooling

A/B measurement for the four Windows App SDK 2.6.4 XAML optional changes on this
repo's Release/JIT build:

- `DefaultStyleOptimizations`
- `IconNoGridOptimization`
- `OptimizeApplyStyles`
- `DeferContextFlyoutInit`

The switches must be configured before `Application.Start`. The production app gets
them through the MSBuild property `EnabledXamlOptionalChanges` (comma list, escaped
as `%2C` on the dotnet CLI); the XAML compiler emits
`XamlOptionalChanges.EnableChange(...)` calls before `Application.Start`.

## Layout

- `Probe/` — small unpackaged WinUI 3 probe (`net11.0-windows10.0.22621.0`,
  `WindowsPackageType=None`, `DISABLE_XAML_GENERATED_MAIN`). It enables zero, one or all
  switches before `Application.Start`, records `IsChangeEnabled`/`IsLocked` before and
  inside the callback, the FontIcon/SymbolIcon visual-tree shape, the XamlReader-created
  Button's `Template` before entering the tree, renders a fixed 1200x800 gallery
  (icons, buttons, programmatic `MenuFlyout`, rich text, theme surface) and captures a
  `PrintWindow` screenshot of the window and of the open flyout popup. It exits non-zero
  when a `--expect name=value` expectation fails.
- `Measure.ps1` — builds the product per variant with the property, patches the generated
  layout manifest to the Dev identity (`Mang.BiliCopilot.Dev`, `CN=BiliCopilotDev`,
  version `2.2610.9.<n>`), packs, signs with `DevSigning.pfx`, installs, runs the probe,
  then performs interleaved measured runs and writes raw/summary data under
  `artifacts/xaml-optional-changes/` (gitignored).

## What it proves

1. The MSBuild property reaches the app: the probe records the same switch state from
   inside the packaged Dev build via its own probe matrix, and every production variant
   is built from that property.
2. The SDK defaults: before any `EnableChange` call all four report `IsChangeEnabled=false`
   and `IsLocked=false`; inside `Application.Start` `IsLocked=true`. This is runtime
   evidence, not an inference from "the code never calls it".
3. Behavioral effects per switch: `IconNoGridOptimization` removes the Grid wrapper from
   FontIcon; `OptimizeApplyStyles` makes a XamlReader-created Button's `Template` null
   until it enters the tree; the other two show no observable change in this gallery.
4. Cost/benefit: startup to first visible window, private working set and process CPU
   after a fixed settle, idle CPU over a fixed window, wake/restore latency of the running
   instance, and a per-run window screenshot, interleaved across variants so ambient load
   is shared.
5. Visual regression: probe gallery and Bili home screenshots are compared per variant
   with a pixel diff (threshold 8/255 per channel) against baseline.

## Running

```powershell
# Validate the summary math only (median/MAD/noise verdict)
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -SelfTest

# One variant, one run: build + pack + install + measure
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Variants baseline -Runs 1

# Full experiment (baseline + four switches, 7 interleaved runs each)
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Runs 7
```

Outputs:

- `artifacts/xaml-optional-changes/probe/<variant>/probe.json` and screenshots
- `artifacts/xaml-optional-changes/raw/<variant>/run-<n>.json`
- `artifacts/xaml-optional-changes/screenshots/<variant>-run<n>.png`
- `artifacts/xaml-optional-changes/summary.json` / `summary.md`

## Statistics

Per metric and variant: median and MAD. Deltas are compared against a pooled robust
sigma (`1.4826 * MAD` per variant, pooled in quadrature); a change is reported as beyond
noise only when `|delta| > max(2 * pooledSigma, 1% of baseline median)`. The 1% floor
keeps sub-percent wiggles from being reported as wins. These are descriptive statistics
for N=7, not a significance test.

## Safety

- The harness only ever installs `Mang.BiliCopilot.Dev`. It refuses to run if the target
  identity is `Mang.BiliCopilot` or `Mang.BiliCopilot.AotDev`, snapshots their installed
  versions and fails if they change.
- It only closes processes whose `Path` equals the installed Dev package executable.
- Installing a new Dev version replaces the Dev package in place; its signed-in app data
  is preserved. Each install bumps the version so the update is accepted.

## Known limits

- **JIT Release**, not Native AOT: absolute startup/memory numbers are not the shipped
  AOT numbers; only the within-build A/B comparison is meaningful.
- A production BiliCopilot instance may be running (the user's video). The harness never
  touches it, records its process count/CPU/memory in every run, and interleaves variants,
  but cannot hold ambient load constant.
- The gallery is synthetic. `TextBox` and `RichEditBox` cannot be instantiated in this
  probe on this machine: a 40-line WinUI app with only a `TextBox` crashes in
  `Microsoft.UI.Xaml.dll` 3.3.0 (0xC000027B, stowed `COMException 0x80040111` while
  activating `Windows.ApplicationModel.LimitedAccessFeatures`); `RichTextBlock` and
  `TextBlock` are fine. Text-input visual coverage therefore comes from the production
  Bili home screenshot (its search box is a TextBox), not from the probe.
- Menu coverage opens the flyout programmatically; it does not simulate user input or
  context-menu invocation.
- Window size is fixed at 1200x800 physical pixels; the machine runs at 150% scaling
  (DPI 144), so the layout is 800x533 DIP. Layout/DPI are not varied.
- Probe runs are unpackaged. That is enough for the switch state, icon tree, template
  timing and gallery, but the packaged product is the authoritative visual surface.
