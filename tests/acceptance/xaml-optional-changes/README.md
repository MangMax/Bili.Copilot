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

### Why the pack/sign/install pipeline lives in acceptance tooling

The switches are compile-time (they must be set before `Application.Start`), and the
packaged product — not the unpackaged probe — is the authoritative performance and visual
surface. Measuring it therefore requires building, installing and launching the real app
once per variant. The generated layout manifest still carries the production identity
(`Mang.BiliCopilot`), so the harness patches a copy to the Dev identity, signs it with the
in-repo Dev certificate, and installs only that. Production identities are never launched,
and the harness refuses to run if the resolved target is one of them (see Safety). The
pipeline is gated behind the explicit variant run and does not run in `-SelfTest` or
`-RegenerateSummary`.

## What it proves

1. The MSBuild property reaches the product build. Two independent checks:
   - After every production build, `Measure.ps1` deletes any previous generated
     `obj/.../App.g.i.cs`, finds the regenerated file, parses its
     `XamlOptionalChanges.EnableChange(global::Microsoft.UI.Xaml.Settings.XamlChangeId.X)`
     calls, and asserts they exactly equal the variant (baseline: none; single switch:
     exactly that one; `all`: all four). A stale generated Main or a property that did
     not apply fails the variant.
   - The unpackaged probe records the runtime switch state for its own `--switches`
     argument (`before`/`afterEnable`/`inside`), which proves the SDK API behavior and
     the expected state transitions. It is **not** the packaged build: the packaged link
     is proven by the generated-entry-point assertion above.
2. The SDK defaults: before any `EnableChange` call all four report `IsChangeEnabled=false`
   and `IsLocked=false`; inside `Application.Start` `IsLocked=true`. This is runtime
   evidence, not an inference from "the code never calls it".
3. Behavioral effects per switch: `IconNoGridOptimization` removes the Grid wrapper from
   FontIcon; `OptimizeApplyStyles` makes a XamlReader-created Button's `Template` null
   until it enters the tree. `DeferContextFlyoutInit` is exercised through the default
   `TextControlCommandBarContextFlyout` of a selectable `TextBlock` and a `RichTextBlock`:
   the probe reads `ContextFlyout`, calls `ShowAt` and captures the popup when a flyout is
   exposed. On Windows App SDK 2.6.4 the property getter returns `null` for both controls,
   so the probe records `textBlockFlyoutCaptured=not-observable` /
   `richTextFlyoutCaptured=not-observable`; that row is explicitly **unmeasured**, not a
   claim that pixels are identical.
4. Cost/benefit: startup to first visible window, private working set and process CPU
   after a fixed settle, idle CPU over a fixed window, wake/restore latency of the running
   instance, and a per-run window screenshot, interleaved across variants so ambient load
   is shared. Window discovery and wake detection poll at 5 ms; reported latencies are
   rounded to that interval and the interval is part of the noise gate.
5. Visual regression: probe gallery, programmatic MenuFlyout popup and (when exposed) the
   text flyouts, plus Bili home screenshots, are compared per variant with a pixel diff
   (threshold 8/255 per channel) against baseline.

## Running

```powershell
# Validate the summary math only (median/MAD/noise verdict)
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -SelfTest

# One variant, one run: build + pack + install + measure
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Variants baseline -Runs 1

# Full experiment (baseline + four switches, 7 interleaved runs each)
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Runs 7

# Diagnostic sixth variant: all four switches at once. It is not part of the
# per-switch verdict; it exists to check that the generated entry point carries
# a multi-switch list and that the probe sees all four enabled.
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Variants all -Runs 1

# Recompute summary.json/summary.md from existing raw/probe/screenshot data
# without building, installing or launching anything (e.g. after a report fix).
pwsh -File tests/acceptance/xaml-optional-changes/Measure.ps1 -RegenerateSummary
```

Outputs:

- `artifacts/xaml-optional-changes/probe/<variant>/probe.json` and screenshots
- `artifacts/xaml-optional-changes/raw/<variant>/run-<n>.json`
- `artifacts/xaml-optional-changes/screenshots/<variant>-run<n>.png`
- `artifacts/xaml-optional-changes/summary.json` / `summary.md`

## Statistics

Per metric and variant: median and MAD. Deltas are compared against a pooled robust
sigma (`1.4826 * MAD` per variant, pooled in quadrature); a change is reported as beyond
noise only when `|delta| > max(2 * pooledSigma, 1% of baseline median, resolution)`.
The resolution term is 5 ms for window/wake polling and the recorded precision of the
other counters, so a sub-resolution difference can never be reported as an effect.

The pooled gate is not a significance test and cannot detect a systematic per-round
shift smaller than run-to-run spread. The summary therefore also contains a **paired
analysis**: for each switch and metric it subtracts the baseline run of the *same round*
from the variant run, and reports the mean, median, positive/negative/zero counts and the
exact two-sided sign-test p-value over the non-zero deltas. Use that table (not only the
pooled verdict) when deciding whether a switch has a real cost or benefit.

## Safety

- The harness only ever installs `Mang.BiliCopilot.Dev`. It compares the **resolved**
  `Get-AppxPackage` name (before and after install) with the forbidden list
  `Mang.BiliCopilot` / `Mang.BiliCopilot.AotDev`, and snapshots their installed versions,
  failing if they change. The old check compared the target constant with itself and
  could not fire.
- It only closes processes whose `Path` equals the installed Dev package executable; the
  filter is the only guard, with no unreachable re-check.
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
- Menu coverage opens the MenuFlyout programmatically; it does not simulate user input
  or context-menu invocation. The default text context flyout is read from
  `ContextFlyout` and opened with `ShowAt`; if the getter returns `null` the probe records
  `not-observable` (that is what 2.6.4 does) rather than implying pixel coverage.
- The generated-entry-point assertion proves which switches the XAML compiler emitted for
  the product build. It does not prove that the packaged process actually honored them at
  runtime; that part is the SDK behavior verified by the probe.
- Window size is fixed at 1200x800 physical pixels; the machine runs at 150% scaling
  (DPI 144), so the layout is 800x533 DIP. Layout/DPI are not varied.
- Probe runs are unpackaged. That is enough for the switch state, icon tree, template
  timing, text-flyout observability and gallery, but the packaged product is the
  authoritative visual surface; the packaged link is the generated-entry-point assertion,
  not the probe.
