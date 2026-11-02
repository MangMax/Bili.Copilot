# ItemsRepeater recycling — measurement results (#70)

Verdict for the accepted scope: **未触发 (not triggered)** on Windows App SDK 2.6.4 for the long-list
and page-switch loop that BiliCopilot actually runs, and **无法判定 (undetermined)** for the one
in-session teardown path (player window), which this harness cannot measure reliably.

No product code was changed. See README.md for the defect model and the harness contract.

## Fixed inputs

| item | value |
| --- | --- |
| package | `Mang.BiliCopilot.Dev` 2.2610.9.132 (JIT Release, x64, `EnabledXamlOptionalChanges` unset) |
| Windows App SDK runtime | `Microsoft.WindowsAppRuntime.2` 2.6.4.0 (framework package dependency) |
| OS | Windows 10.0.26300 (26H2) |
| repo | `feat/spec65-70-itemsrepeater`, base b8c52f2b |
| window | 1440x900 at (80,80), fixed per run |
| DPI | 144 DPI (150%), recorded per run, never changed |
| content | live feed, same signed-in Dev instance, identical navigation sequence |
| ambient | production `Mang.BiliCopilot` 2.2611.0.0 running throughout (recorded, never touched) |

The harness process is DPI-unaware (`SetProcessDpiAwarenessContext` is refused in pwsh), so
window coordinates are virtualised; the recorded window rect (1440x900) and the screenshots are the
authoritative geometry.

## Run A — replication (no teardown), 8 cycles

`artifacts/itemsrepeater-recycling/20261102-235806/`

| metric | value |
| --- | --- |
| private commit | 200.4, 199.3, 203.1, 200.9, 202.8, 207.5, 200.6, 205.9 MB |
| slope | **0.71 MB/cycle** |
| R^2 | 0.366 |
| total delta | +5.5 MB over 8 cycles |
| handle slope | -21/cycle (no growth) |
| scroll / nav | wheel 8/8, nav 16/16 |

## Run B — primary (no teardown), 8 cycles

`artifacts/itemsrepeater-recycling/20261103-000851/`

| metric | value |
| --- | --- |
| private commit | 194.2, 202.3, 205.5, 201.1, 204.1, 199.4, 203.6, 204.5 MB |
| slope | **0.76 MB/cycle** |
| R^2 | 0.256 |
| total delta | +10.3 MB over 8 cycles |
| private working set | 150.4 -> 160.0 MB |
| handles | 3372 -> 3385, slope -9.5/cycle, R^2 0.14 |
| wheel scroll delivered | 8/8 cycles (84.6-93.9% of the list per pass) |
| navigation | 16/16 selections succeeded |
| UIA helper errors | 0 |
| ambient system CPU | 3% |
| verdict | `NoGrowth / flat` |

Two independent runs land at 0.71 and 0.76 MB/cycle with R^2 of 0.37 and 0.26 — a noisy zero, not a
trend: the 40 MB / 8 MB-per-cycle leak thresholds are nowhere near.

## Run C — teardown probe (player window), 3 cycles / 1 teardown

`artifacts/itemsrepeater-recycling/20261103-001238/`

| step | private commit | handles |
| --- | --- | --- |
| warmup (no teardown) | 211.3 MB | 3385 |
| cycle 1 (player opened + closed) | 709.3 MB | 7312 |
| cycle 2 | 575.0 MB | 8570 |
| cycle 3 | 581.2 MB | 8570 |

Player window opened 1/1 and was removed from the visible window set 1/1 in this run
(3/3 across all attempts). But after the programmatic close:

* the main window's UI Automation subtree came back **empty** (`navItems = 0`) and stayed empty,
  so cycles 2-3 could not scroll or navigate (0/2 nav, no wheel, UIA fallback could not find a
  scrollable element either);
* the app kept ~500 MB of media allocation above the no-player baseline.

So this path measures mpv/media state, not recycled XAML: **无法判定**. It is not evidence of a leak
and not evidence against one.

## Why "not triggered" is the right call for the exercised path

1. The defect requires repeater **destruction** (README §1, condition 3). BiliCopilot's
   `MainFrame` caches page instances forever, so page switching never destroys a repeater; the
   long-list/page-switch loop is the only teardown-free loop the app performs repeatedly, and it is
   flat over 8 cycles in two runs.
2. The runtime carries the fix and the containment change id that gates it is not cataloged as
   disabled in 2.6.4, so the fixed branch is default-on (README §1).
3. The only remaining teardown path (player window) is not measurable with this harness, so the
   honest statement there is "undetermined", not "no leak".

## Reproduce

```powershell
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -Cycles 8 -WarmupCycles 2 -PlayerWindowCycles 0
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -Cycles 3 -WarmupCycles 1 -PlayerWindowCycles 1
```

Raw JSON, per-cycle samples, event logs and screenshots are under
`artifacts/itemsrepeater-recycling/<runId>/` (gitignored). This file is the durable summary.