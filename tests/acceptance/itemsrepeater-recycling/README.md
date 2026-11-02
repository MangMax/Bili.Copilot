# ItemsRepeater recycling acceptance (ticket #70)

Evidence harness for the Windows App SDK fix

> **ItemTemplateWrapper_RecyclePoolLeak** — "Fixed a memory leak in `ItemsRepeater` where recycled
> elements were never garbage collected due to a reference cycle through the `RecyclePool`, which
> could also cause crashes in `InvalidateChildrenMeasure`."
> — [Windows App SDK v1.8.9 release notes](https://github.com/microsoft/WindowsAppSDK/releases/tag/v1.8.9)

This folder answers one question with data: **does BiliCopilot actually trigger that defect on the
build it ships today?** It does not change product code. If the answer is "not triggered", no code
change is justified (ticket acceptance criterion 4).

---

## 1. The defect, precisely

The fix changed `ItemTemplateWrapper` (the object an `ItemsRepeater` keeps for its `ItemTemplate`)
from holding a plain strong `winrt::DataTemplate` reference to a tracked one:

* pre-fix — [winui3/release/1.8.8 `ItemTemplateWrapper.h`](https://github.com/microsoft/microsoft-ui-xaml/blob/winui3/release/1.8.8/src/controls/dev/Repeater/ItemTemplateWrapper.h):
  `winrt::DataTemplate m_dataTemplate{ nullptr };`
* post-fix — [winui3/release/1.8.9 `ItemTemplateWrapper.h`](https://github.com/microsoft/microsoft-ui-xaml/blob/winui3/release/1.8.9/src/controls/dev/Repeater/ItemTemplateWrapper.h)
  adds `EnableTracking()` and `std::optional<tracker_ref<winrt::DataTemplate>> m_trackedDataTemplate`
  with this comment:

```
  // Enables reference tracking for m_dataTemplate using the provided owner's
  // ITrackerHandleManager. This makes the DataTemplate visible to the XAML
  // reference tracker, allowing cycle detection and breaking for:
  //   ItemsRepeater -> ItemTemplateWrapper -> DataTemplate -> RecyclePool -> m_owner -> ItemsRepeater
  // Same pattern as RecyclePool::ElementInfo and ViewManager::PinnedElementInfo.
```

`RecyclePool::ElementInfo` already used `tracker_ref` for the element and the owning `Panel`
([`RecyclePool.h`](https://github.com/microsoft/microsoft-ui-xaml/blob/winui3/release/1.8.9/src/controls/dev/Repeater/RecyclePool.h)),
and the pool is stored on the `DataTemplate` itself (`PoolInstanceProperty`). The **untracked strong
edge** `ItemTemplateWrapper -> DataTemplate` made the whole cycle look externally rooted, so the XAML
reference tracker never broke it: the `DataTemplate`, its `RecyclePool`, every recycled element and
the `Panel`/`ItemsRepeater` that owns them all stayed alive after the repeater was gone. A stale
`Panel` owner in a shared pool is also what surfaced as `InvalidateChildrenMeasure` crashes.

### Trigger conditions (all four are required)

1. An `ItemsRepeater` (or a control that wraps one) whose `ItemTemplate` is a **`DataTemplate`**
   (a `DataTemplateSelector` does not form the cycle — see the 2.6.4 source comment in
   `ItemsRepeater::OnItemTemplateChanged`).
2. At least one element actually **recycled** into the pool, i.e. the list scrolled/realized past
   the viewport at least once.
3. The repeater instance is **destroyed** (page/window unloaded, control removed) while the cycle
   exists. While the repeater stays alive, the cycle is harmless.
4. Nothing app-side clears the pool or drops the template before teardown.

### Is the fix active in Windows App SDK 2.6.4?

Yes, as far as static evidence goes:

* 2.6.4 carries the tracked implementation
  ([`winui3/release/2.6.4 ItemTemplateWrapper.h`](https://github.com/microsoft/microsoft-ui-xaml/blob/winui3/release/2.6.4/src/controls/dev/Repeater/ItemTemplateWrapper.h)).
* The call site is gated by the Windows App SDK *containment* switch
  `IsChangeEnabled<WINAPPSDK_CHANGEID_61574373>()` in `ItemsRepeater::OnItemTemplateChanged`.
  Containment is an **opt-out** mechanism: the catalog lists change ids per introducing release and
  only changes *newer than the runtime patch level* (or explicitly disabled by the app) end up in
  `disabledChanges`
  ([Containment v2 plan](https://github.com/microsoft/WindowsAppSDK/blob/main/specs/Compatibility/WASDK-ContainmentV2-Plan.md)).
  The ItemsRepeater change id is **not in the 2.6.4 catalog**
  ([`RuntimeCompatibilityOptions.cpp` @ v2.6.4](https://github.com/microsoft/WindowsAppSDK/blob/v2.6.4/dev/RuntimeCompatibilityOptions/RuntimeCompatibilityOptions.cpp)),
  and the repo sets neither `WindowsAppSDKDisabledChanges` nor a runtime patch level, so the
  fixed branch is taken by default.
* Therefore an old-vs-new A/B is **not required** to interpret this run: a 2.6.4 run measures the
  fixed runtime. What still needs measuring is whether Bili's own object graph makes the leak
  *reachable* and *visible*.

## 2. What BiliCopilot does that matches

Every `AppRepeater` in the app forwards a `DataTemplate` into an inner
`AppItemsRepeater.ItemTemplate`, so condition 1 holds everywhere:

```
Richasy.WinUIKernel.Share/Base/Repeater/AppRepeater.xaml
  <local:AppItemsRepeater x:Name="Repeater"
      ItemTemplate="{x:Bind GetItemTemplate(ItemContainerTemplate), Mode=OneWay}"
      VerticalCacheLength="20" />
```

There are 87 `AppRepeater` elements across 39 XAML files (`rg -c "<repeater:AppRepeater" src`), and
`BiliCopilot.UI/Styles/Repeater.xaml` sets `RepeaterCachePolicy.VerticalCacheLength` = 0.5 on top of
the built-in cache, so several recycle slots exist per repeater.

The important nuance is **teardown** (condition 3):

* `MainFrame.NavigateTo` (`src/Desktop/BiliCopilot.UI/Controls/MainFrame.xaml.cs`) **never
  removes** a page from `RootGrid`; it only flips `Visibility`. Navigating between pages
  therefore does *not* destroy an `ItemsRepeater`. Page switching is covered as a **control
  scenario** — it must not grow.
* The player window is created per playback (`PlayerWindow` via
  `PlayerViewModel.ShowPlayerWindow`) and destroyed on close (`PlayerViewModel` ->
  `Window.Close()` -> `DesktopWindowXamlSource.Dispose`). That is the one repeatable real
  teardown inside a single session, and `PlayerOverlay.xaml` /
  `OverlayTransportControls.xaml` contain `ItemsRepeater`s.

Scenario matrix used by `Measure.ps1`:

| scenario | what it exercises | switch |
| --- | --- | --- |
| long list (wheel down/up, UI Automation fallback) | recycle churn, realize/recycle | always on (`-WheelNotches`) |
| page switch A -> B -> A | visibility switch of cached pages (no teardown) | always on (`-NavItemCount`) |
| player window open/close | real XAML tree teardown | opt-in `-PlayerWindowCycles N` |
| app close | final teardown | always on |

## 3. Running it

Prerequisites:

1. Build + install the **Dev** package (`Mang.BiliCopilot.Dev`) exactly as documented in the
   spec #65 handoff; the script refuses any other identity.
2. A signed-in Dev instance (the run does not read credentials, it only uses existing app data).
3. `pwsh` 7 for `Measure.ps1`; Windows PowerShell 5.1 only for `Drive-Uia.ps1` (in-box; pwsh 7
   cannot late-bind the UI Automation COM objects, so the UIA actions are delegated).

```powershell
# preflight only: resolves the package, build fingerprint, ambient processes; launches nothing
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -DryRun

# the acceptance run that can actually detect the defect (real teardown every cycle)
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -Cycles 6 -WarmupCycles 2 -PlayerWindowCycles 3

# control run: long list + page switch only (no teardown); should be flat
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/Measure.ps1 -Cycles 6

# trend arithmetic only (no app, no UI)
pwsh -NoProfile -File tests/acceptance/itemsrepeater-recycling/GrowthTrend.Tests.ps1
```

Useful parameters: `-WindowWidth`/`-WindowHeight`/`-WindowX`/`-WindowY` (fixed geometry),
`-WheelNotches` (scroll distance per cycle), `-NavItemCount` (rail items rotated),
`-SkipScreenshots`, `-NoResize`, `-KeepRunning`, `-ArtifactRoot`.

## 4. What is measured, and how growth is judged

Sampled at every cycle boundary (after scroll + page switch + optional teardown, once the process is
CPU-quiet):

* `PrivateMemorySize64` (private commit, MB) — **primary**,
* `WorkingSetPrivate` (private working set, MB) — secondary,
* handle count, thread count, CPU seconds,
* ambient BiliCopilot processes and total CPU percent (a production instance may be running and is
  never touched),
* UI Automation `VerticalScrollPercent` before/after the wheel pass (proof the scroll landed),
* PNG screenshots at `baseline`, `cycle-N-after-scroll`, `cycle-N-after-switch`.

Warmup cycles (`-WarmupCycles`, default 2) are recorded but excluded from the verdict — the first
cycles pay JIT tiering, XAML template loading, image decode caches and feed loading.

`Get-RecyclingVerdict` and the report renderer (in `GrowthTrend.ps1`, unit tested by
`GrowthTrend.Tests.ps1`) apply, in order:

| rule | verdict |
| --- | --- |
| fewer than 4 measured cycles | `Inconclusive` |
| slope <= 2 MB/cycle | `NoGrowth` |
| total growth < 40 MB | `NoGrowth` |
| R^2 < 0.8 | `Inconclusive` (too noisy to call) |
| heading slope < 0.5 x overall slope | `Inconclusive` (warm-up plateau, not a linear leak) |
| slope >= 8 MB/cycle | `Growth` |
| otherwise | `Inconclusive` |

Mapping to the ticket's verdict words:

| harness verdict | ticket verdict |
| --- | --- |
| `Growth` | **触发** — defect-shaped growth is observable |
| `NoGrowth` | **未触发** — fixed runtime plus teardown coverage present |
| `Inconclusive` | **无法判定** — report slope / R^2 / screenshots, change no code |

Handle count never changes the verdict: this defect leaks native XAML objects, not handles. A handle
slope is reported because a *different* defect would show up there.

## 5. Artifacts

```
artifacts/itemsrepeater-recycling/<runId>/     (gitignored: .gitignore line "artifacts/")
  raw.json        full samples, cycle records, event log, preflight
  summary.json    verdict + trend + series
  summary.md      human-readable report (also printed)
  screenshots/    baseline.png, cycle-N-after-scroll.png, cycle-N-after-switch.png
```

## 6. Limits — read before quoting a number

* **Content is not frozen.** The feed is live Bilibili data fetched by the signed-in Dev instance.
  Mitigations: identical navigation sequence, identical geometry, warmup cycles, per-cycle deltas
  measured *within* one run, screenshots for later inspection. Absolute memory numbers are not
  comparable across days; the **slope within a run** is the signal.
* **No pre-fix binary.** The pre-fix 1.8.251106002 bits exist only in the NuGet cache, never as an
  installed build of this app, so the harness measures the fixed runtime and judges reachability and
  growth instead of running a true A/B.
* **Page switching cannot show this leak** because `MainFrame` caches page instances; it is
  measured as a control, not as evidence of the defect.
* **Player-window cycles are best effort.** The card click uses configurable window-relative points
  (`-PlayerCardXPercent`/`-PlayerCardYPercent`, default 0.5/0.6) plus five fallback points; if no second window appears the
  cycle is marked failed in `raw.json` and the run continues; check `playerWindow.opened` before
  trusting a teardown-based verdict.
* **DPI is recorded, not changed.** The script never alters the user's scaling; it records
  `GetDpiForWindow` and the window rect so a different DPI is visible in the artifacts.
* **No forced GC.** The app exposes no GC trigger, so freed-but-not-trimmed working set can lag;
  private commit is the primary counter for that reason.
* **Ambient load is recorded, not controlled.** A production `Mang.BiliCopilot` instance may be
  running; every sample carries process counts and system CPU so a noisy run can be discarded.

## 7. Safety contract

* Only `Mang.BiliCopilot.Dev` is launched — the parameter has a single-value `ValidateSet`,
  and the exe path is resolved from that package's `InstallLocation` and verified before launch.
* `Mang.BiliCopilot` / `Mang.BiliCopilot.AotDev` processes are only *observed* (ids/paths
  recorded in every sample) and never activated, minimized or closed.
* A second Dev instance is refused (preflight throws if one is already running).
* Everything the script starts is closed in a `finally` block, with a bounded `CloseMainWindow`
  and a last-resort `Stop-Process` on **that PID only**.
* Window geometry is restored before exit; the user's DPI/scaling is never touched.
* A `.measure.lock` file in the artifact root prevents two concurrent runs.
