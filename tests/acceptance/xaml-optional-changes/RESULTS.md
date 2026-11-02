# XAML optional changes — measurement results (#69)

Verdict: **revert all four** switches. None beat baseline beyond run-to-run noise on the fixed
JIT Release build, the paired per-round test finds no consistent shift, and none caused a visual
regression. `EnabledXamlOptionalChanges` is left unset in
`src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj`; no product code was changed.
See README.md for the harness contract and the probe's known limits.

## Fixed inputs

| item | value |
| --- | --- |
| package | `Mang.BiliCopilot.Dev` 2.2610.9.133–161 (one version per install; final installed 2.2610.9.161, OptimizeApplyStyles) |
| build | JIT Release, x64, no product csproj edit; switch passed as `-p:EnabledXamlOptionalChanges=<name>` |
| Windows App SDK | `Microsoft.WindowsAppRuntime.2` 2.6.4.0 |
| OS | Windows 11 10.0.26300 (26H2) |
| repo | `feat/spec65-review-fixes`, integration tip 95d7786a; harness fixes only, product unchanged |
| probe window | 1200x800 physical, light theme, fixed gallery; DPI 144 (150%) |
| product window | 1440x900, recorded per run; the harness restores the user's pre-run rect before exit |
| runs | 7 per variant, interleaved round-robin; first launch after each install discarded |
| timing | first visible window polled at 5 ms; 10 s settle; 5 s idle-CPU window; 2 wake cycles |
| ambient | production `Mang.BiliCopilot` 2.2611.0.0 running throughout (recorded, never touched) |

## Runtime default-state evidence

The probe is **unpackaged** and driven by its own `--switches` argument. Before `Application.Start`
it reports `IsChangeEnabled=false` for all four and `IsLocked=false`; inside the
`Application.Start` callback `IsLocked=true` and all four still `false`. That proves the 2.6.4
defaults are off at runtime, not merely "never called", so disabling is unnecessary. It does not
by itself prove anything about the packaged build.

The packaged-product link is proven separately: for every variant the harness deletes the previous
`obj/.../App.g.i.cs`, rebuilds, and asserts the regenerated entry point contains exactly the
variant's `XamlOptionalChanges.EnableChange(...)` calls (none for baseline, one for a single
switch, four for `all`). This is what would have caught a stale generated Main. This run passed
that assertion for all five variants.

Behavior with each switch enabled alone:

| switch | observation |
| --- | --- |
| DefaultStyleOptimizations | no change in icon tree or XamlReader button template timing |
| IconNoGridOptimization | FontIcon child Grid → TextBlock; SymbolIcon keeps its Grid |
| OptimizeApplyStyles | XamlReader Button.Template is null before entering the tree (true after) |
| DeferContextFlyoutInit | **not observable**: selectable `TextBlock` and `RichTextBlock` report `ContextFlyout=null` on 2.6.4, so the default `TextControlCommandBarContextFlyout` cannot be obtained and opened with `ShowAt`; the probe records `not-observable` for both. The programmatic `MenuFlyout` still opens and is pixel-identical. |

## Measured medians (MAD), 7 runs per variant

| variant | startup ms | private WS MB | CPU@settle s | idle CPU % 1 core | wake ms |
| --- | --- | --- | --- | --- | --- |
| baseline | 1305 (20) | 134.7 (0.5) | 4.03 (0.26) | 0.31 (0.31) | 15 (0) |
| DefaultStyleOptimizations | 1295 (30) | 135.6 (0.6) | 3.98 (0.26) | 0.31 (0.31) | 15 (0) |
| IconNoGridOptimization | 1280 (10) | 134.9 (0.5) | 5.17 (0.91) | 0 (0) | 15 (0) |
| OptimizeApplyStyles | 1275 (10) | 135.7 (0.8) | 4.11 (0.45) | 0.31 (0.31) | 15 (0) |
| DeferContextFlyoutInit | 1380 (90) | 135.3 (0.3) | 4.05 (0.22) | 0.31 (0.31) | 15 (0) |

Wake is reported in 5 ms ticks (all medians 15 ms). Startup is reported in 5 ms ticks too.

## Deltas vs baseline

| switch | startup | private WS | CPU@settle | idle CPU | wake |
| --- | --- | --- | --- | --- | --- |
| DefaultStyleOptimizations | −10 ms (−0.8%) | +0.9 MB (+0.7%) | −0.05 s (−1.2%) | 0 pp | 0 ms |
| IconNoGridOptimization | −25 ms (−1.9%) | +0.2 MB (+0.1%) | +1.14 s (+28.3%) | −0.31 pp | 0 ms |
| OptimizeApplyStyles | −30 ms (−2.3%) | +1.0 MB (+0.7%) | +0.08 s (+2.0%) | 0 pp | 0 ms |
| DeferContextFlyoutInit | +75 ms (+5.7%) | +0.6 MB (+0.4%) | +0.02 s (+0.5%) | 0 pp | 0 ms |

Noise rule: a delta counts only when
`|Δ| > max(2 × pooled robust σ, 1% of baseline, instrument resolution)`, with
`σ = 1.4826 × MAD` pooled in quadrature. Resolution is 5 ms for startup/wake, 0.1 MB for private
WS, and 0.01 for CPU/idle. **No delta qualified.** The wake deltas are 0 ms; the 5 ms resolution
is larger than any wake difference that could be observed here, so no wake claim is made.
The 0 pp idle deltas and 0 ms wake deltas are below resolution; `summary.md` marks those rows
`Below resolution = True` instead of treating them as measured zeros.
Observed MAD ranges: startup 10–90 ms, private WS 0.3–0.8 MB, CPU@settle 0.22–0.91 s, idle CPU
0–0.31 pp.

## Paired per-round analysis (variant − baseline, same round)

The pooled gate cannot detect a systematic shift hidden by run-to-run spread, so each variant run
is compared with the baseline run of the same round.

| switch | metric | mean | median | + / − / 0 | exact sign-test p |
| --- | --- | --- | --- | --- | --- |
| DefaultStyleOptimizations | startup ms | −14.29 | −25 | 2 / 5 / 0 | 0.4531 |
| DefaultStyleOptimizations | private WS MB | +0.24 | +0.8 | 4 / 2 / 1 | 0.6875 |
| DefaultStyleOptimizations | CPU@settle s | +0.09 | −0.28 | 3 / 4 / 0 | 1.0 |
| IconNoGridOptimization | startup ms | −19.29 | −25 | 1 / 5 / 1 | 0.2188 |
| IconNoGridOptimization | private WS MB | +0.03 | +0.2 | 5 / 2 / 0 | 0.4531 |
| IconNoGridOptimization | CPU@settle s | +0.26 | +1.05 | 4 / 3 / 0 | 1.0 |
| OptimizeApplyStyles | startup ms | −21.43 | −25 | 2 / 5 / 0 | 0.4531 |
| OptimizeApplyStyles | private WS MB | +0.40 | −0.1 | 3 / 4 / 0 | 1.0 |
| OptimizeApplyStyles | CPU@settle s | −0.07 | −0.09 | 3 / 4 / 0 | 1.0 |
| DeferContextFlyoutInit | startup ms | +82.14 | +45 | 4 / 3 / 0 | 1.0 |
| DeferContextFlyoutInit | private WS MB | −0.20 | +0.6 | 4 / 3 / 0 | 1.0 |
| DeferContextFlyoutInit | CPU@settle s | +0.03 | +0.21 | 4 / 3 / 0 | 1.0 |

Idle CPU and wake also have p = 1.0 for every switch; idle CPU is dominated by ambient load and
wake deltas are below the 5 ms instrument resolution. The smallest p in the whole table is 0.2188
(IconNoGridOptimization startup), so **no paired test contradicts the revert verdict at p < 0.05**.

**CPU@settle, settled explicitly.** The earlier pooled comparison showed −1.43 s for
DefaultStyleOptimizations and −1.39 s for IconNoGridOptimization (about 1.5σ) and explicitly did not
claim it. This run does not reproduce it: DefaultStyleOptimizations has a paired median of −0.28 s
with 3 of 7 rounds negative (p = 1.0), and IconNoGridOptimization is +1.05 s with 4 of 7 positive
(p = 1.0). There is no CPU@settle effect to claim, and the earlier −1.4 s figures are withdrawn.

## Visual comparison

Probe gallery 0/960,000 differing pixels and programmatic MenuFlyout popup 0/58,305 for all four
switches. The default text context flyout is **not observable** on 2.6.4 (see the behavior table),
so no text-flyout frame was captured or compared; this row is not claimed as identical, it is
unmeasured.

Product screenshots: this run captured **35 frames (7 per variant, runs 1–7)** and **0 were blank**;
the audit in `summary.md` lists frames and blank runs per variant, and all five representative
frames were compared. Bili chrome: top bar 0/158,400 and left navigation 0/150,100 for all four.
Only live recommendation content differs (426,003–433,789 of 1,296,000 pixels), as expected between
launches. No theme, menu or control-template regression.

The pre-fix run referenced in review had 12 of 35 product screenshots blank because `PrintWindow`
ran immediately after minimize/restore; its raw frames and per-run identification were not
retained, so those 12 cannot be attributed to specific variants/runs here. The capture now happens
before the wake test and the harness counts blank frames instead of leaving the claim implicit.

## Verdict

| switch | verdict | reason |
| --- | --- | --- |
| DefaultStyleOptimizations | **revert** | startup −10 ms, WS +0.9 MB, CPU −0.05 s; paired p ≥ 0.45; no visual change |
| IconNoGridOptimization | **revert** | startup −25 ms, WS +0.2 MB; CPU +1.14 s pooled but paired p = 1.0; icon tree changes, rendered pixels identical |
| OptimizeApplyStyles | **revert** | startup −30 ms, WS +1.0 MB; template timing changes as documented; pixels identical; paired p = 1.0 |
| DeferContextFlyoutInit | **revert** | startup +75 ms (paired p = 1.0), WS +0.6 MB; text flyout unmeasured, MenuFlyout identical |

`EnabledXamlOptionalChanges` stays unset; no switch is enabled on a hunch.

## Known limits

- JIT Release, not Native AOT: absolute numbers are not the shipped AOT numbers; only the
  within-build A/B comparison is meaningful.
- The ambient production instance is not controlled (recorded per run); interleaving only shares
  its load, it cannot remove it. Idle CPU in particular is dominated by it.
- TextBox and RichEditBox cannot be instantiated in the probe on this machine: a 40-line WinUI app
  with only a TextBox crashes in `Microsoft.UI.Xaml.dll` 3.3.0 (0xC000027B, stowed COMException
  0x80040111 activating `Windows.ApplicationModel.LimitedAccessFeatures`). RichTextBlock and
  TextBlock work, so text-input visuals are covered only by the Bili home screenshot.
- Synthetic gallery; the MenuFlyout is opened programmatically and the default text context
  flyout is not observable on 2.6.4; one DPI and window size.
- N=7 pooled medians/MAD are descriptive; the paired sign test is exact for these 7 rounds but
  tests sign consistency, not effect size, and ambient load is still uncontrolled.

## Reproduce

```powershell
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -SelfTest
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Variants baseline -Runs 1
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Runs 7
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -RegenerateSummary
```

Raw run JSON, probe JSON, screenshots and `summary.json`/`summary.md` are under
`artifacts/xaml-optional-changes/` (gitignored). This file is the durable summary.
