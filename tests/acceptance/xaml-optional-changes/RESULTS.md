# XAML optional changes — measurement results (#69)

Verdict: **revert all four** switches. None beat baseline beyond run-to-run noise on the fixed
JIT Release build, and none caused a visual regression. `EnabledXamlOptionalChanges` is left
unset in `src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj`; no product code was changed.
See README.md for the harness contract and the probe's known limits.

## Fixed inputs

| item | value |
| --- | --- |
| package | `Mang.BiliCopilot.Dev` 2.2610.9.103–131 (one version per install; final installed 2.2610.9.131, OptimizeApplyStyles) |
| build | JIT Release, x64, no product csproj edit; switch passed as `-p:EnabledXamlOptionalChanges=<name>` |
| Windows App SDK | `Microsoft.WindowsAppRuntime.2` 2.6.4.0 |
| OS | Windows 11 10.0.26300 (26H2) |
| repo | `feat/spec65-69-xaml-switches`, base b8c52f2b |
| probe window | 1200x800 physical, light theme, fixed gallery; DPI 144 (150%) |
| product window | 1920x1140, recorded per run, never resized |
| runs | 7 per variant, interleaved round-robin; first launch after each install discarded |
| timing | first visible window; 10 s settle; 5 s idle-CPU window; 2 wake cycles |
| ambient | production `Mang.BiliCopilot` 2.2611.0.0 running throughout (≈1.1 GB WS, recorded, never touched) |

## Runtime default-state evidence

Probe before `Application.Start`: `IsChangeEnabled=false` for all four and `IsLocked=false`;
inside the `Application.Start` callback `IsLocked=true` and all four still `false`. The 2.6.4
defaults are off at runtime, not merely "never called", so disabling is unnecessary.

Behavior with each switch enabled alone:

| switch | observation |
| --- | --- |
| DefaultStyleOptimizations | no change in icon tree or XamlReader button template timing |
| IconNoGridOptimization | FontIcon child Grid → TextBlock; SymbolIcon keeps its Grid |
| OptimizeApplyStyles | XamlReader Button.Template is null before entering the tree (true after) |
| DeferContextFlyoutInit | no change in the gallery; programmatic MenuFlyout still opens |

## Measured medians (MAD), 7 runs per variant

| variant | startup ms | private WS MB | CPU@settle s | idle CPU % 1 core | wake ms |
| --- | --- | --- | --- | --- | --- |
| baseline | 1233.8 (20.1) | 139.1 (3.0) | 5.98 (0.68) | 0 (0) | 17.9 (1.9) |
| DefaultStyleOptimizations | 1276.5 (30.3) | 138.9 (1.7) | 4.55 (0.64) | 0.31 (0.31) | 15.7 (0.9) |
| IconNoGridOptimization | 1239.8 (37.4) | 138.5 (0.8) | 4.59 (0.57) | 13.75 (10.63) | 15.0 (2.4) |
| OptimizeApplyStyles | 1269.1 (41.7) | 138.9 (1.7) | 5.33 (0.66) | 6.25 (6.25) | 15.6 (2.0) |
| DeferContextFlyoutInit | 1238.1 (19.9) | 140.6 (0.3) | 6.17 (0.70) | 0 (0) | 16.6 (0.2) |

## Deltas vs baseline

| switch | startup | private WS | CPU@settle | idle CPU | wake |
| --- | --- | --- | --- | --- | --- |
| DefaultStyleOptimizations | +42.7 ms (+3.5%) | −0.2 MB (−0.1%) | −1.43 s (−23.9%) | +0.31 pp | −2.2 ms (−12.3%) |
| IconNoGridOptimization | +6.0 ms (+0.5%) | −0.6 MB (−0.4%) | −1.39 s (−23.2%) | +13.75 pp | −2.9 ms (−16.2%) |
| OptimizeApplyStyles | +35.3 ms (+2.9%) | −0.2 MB (−0.1%) | −0.65 s (−10.9%) | +6.25 pp | −2.3 ms (−12.8%) |
| DeferContextFlyoutInit | +4.3 ms (+0.3%) | +1.5 MB (+1.1%) | +0.19 s (+3.2%) | 0 pp | −1.3 ms (−7.3%) |

Noise rule: a delta counts only when `|Δ| > max(2 × pooled robust σ, 1% of baseline)`, with
`σ = 1.4826 × MAD` pooled in quadrature from the two variants' MADs. **No delta qualified.**
Observed MAD ranges: startup 20–42 ms (1.6–3.4%), private WS 0.3–3.0 MB, CPU@settle 0.57–0.70 s,
idle CPU 0–10.63 pp (too noisy to use), wake 0.2–2.4 ms. The −1.4 s CPU@settle for
DefaultStyleOptimizations/IconNoGrid is ≈1.5σ and is not claimed; more runs would be needed.

## Visual comparison

Probe gallery 0/960,000 differing pixels and MenuFlyout popup 0/58,305 for all four switches.
Bili chrome: top bar 0/211,200 and left navigation 0/195,700 for all four. Only the live
recommendation content differs (707k–728k of 2,188,800 pixels), as expected between launches.
No theme, menu or control-template regression.

The original run had 12/35 product screenshots blank because `PrintWindow` ran immediately after
minimize/restore; the harness now captures before the wake test and detects/skips blank frames,
and the summary was regenerated from representative non-blank frames.

## Verdict

| switch | verdict | reason |
| --- | --- | --- |
| DefaultStyleOptimizations | **revert** | startup +42.7 ms (worse, within noise); CPU@settle −1.43 s is 1.5σ; no visual change |
| IconNoGridOptimization | **revert** | startup +6.0 ms, private WS −0.6 MB; icon tree changes but rendered pixels are identical; within noise |
| OptimizeApplyStyles | **revert** | startup +35.3 ms; template timing changes as documented; within noise |
| DeferContextFlyoutInit | **revert** | startup +4.3 ms, private WS +1.5 MB; within noise |

`EnabledXamlOptionalChanges` stays unset; no switch is enabled on a hunch.

## Known limits

- JIT Release, not Native AOT: absolute numbers are not the shipped AOT numbers; only the
  within-build A/B comparison is meaningful.
- The ambient production instance is not controlled (recorded per run); interleaving only shares
  its load, it cannot remove it.
- TextBox and RichEditBox cannot be instantiated in the probe on this machine: a 40-line WinUI app
  with only a TextBox crashes in `Microsoft.UI.Xaml.dll` 3.3.0 (0xC000027B, stowed COMException
  0x80040111 activating `Windows.ApplicationModel.LimitedAccessFeatures`). RichTextBlock and
  TextBlock work, so text-input visuals are covered only by the Bili home screenshot.
- Synthetic gallery; the MenuFlyout is opened programmatically; one DPI and window size.
- N=7 descriptive statistics (median/MAD), not a significance test.

## Reproduce

```powershell
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -SelfTest
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -Runs 7
pwsh -NoProfile -File tests/acceptance/xaml-optional-changes/Measure.ps1 -RegenerateSummary
```

Raw run JSON, probe JSON, screenshots and `summary.json`/`summary.md` are under
`artifacts/xaml-optional-changes/` (gitignored). This file is the durable summary.
