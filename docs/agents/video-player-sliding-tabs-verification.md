# Video player sliding tabs (#159 / spec #158)

Only the ordinary video's `VideoPlayerSideHeader` uses `SlidingTabBar`. PGC is unchanged.
The reusable control accepts `SelectorBarItem` instances with unique, non-null `Tag`
identities through `SetItems(items, selectedIdentity)`. `Select(identity)` synchronizes
caller state without raising a user event. `SelectionChanged` exposes the selected
identity; the header forwards it to the existing `SelectSectionCommand` once.

The native SelectorBar, item input/selection state machine and automation peer are preserved.
Only the item visual template is scoped to this control; system focus visuals stay enabled.
The single background uses measured item bounds, a 180 ms cubic ease-out and semantic
theme brushes. Layout changes settle immediately. No animation completion callback
writes selection or geometry. Loaded/unloaded subscriptions and queued system-motion
notifications are scoped to the current mount.

## Automated regression boundary

The focused xUnit project links the **production** control and header C# unchanged.
Adapters provide only XAML, animation, dispatcher and model notification surfaces.
They do **not** prove WinUI rendering, keyboard input, colors, DPI or screen-reader output.

```powershell
dotnet test tests/BiliCopilot.SlidingTabs.Tests/BiliCopilot.SlidingTabs.Tests.csproj
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 `
  -p:SolutionDir="$PWD/src/" -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false
```

| Requirement | Evidence |
| --- | --- |
| Initial/external selection, no command echo | `InitialSelectionAndModelChangesDoNotEchoUserCommands` |
| First measured placement | `FirstLayoutPositionsBackgroundAtActualSelectedBoundsWithoutAnimation` |
| Delayed item measurement | `UnmeasuredSelectionWaitsForLayoutBeforeShowingBackground` |
| Rapid selection, width changes, resize | `RapidSelectionRetargetsOneBackgroundAndResizeSettlesToNewBounds` |
| Rebuilt/empty list and stale selection | `RebuiltEmptyAndUnselectedItemsNeverRetainOldBackgroundOrEmitCommands` |
| Unload cleanup and single subscription on remount | `UnloadStopsAnimationAndRemountSubscribesOnlyOnce` |
| Motion disabled and root/scale changes | `DisabledMotionAndScaleChangesPositionImmediately` |
| Mid-animation preference changes and stale queued events | `DisablingMotionSettlesCurrentAnimationAndIgnoresQueuedEventsFromPreviousMount` |
| Actual header command wiring, replacement and remount | `HeaderSynchronizesContentOnceAndReattachesToCurrentModelOnly` |

## Native Windows acceptance procedure

Use a packaged build of this branch and an ordinary video with multiple available
sections. Record the video identifier, app revision, Windows scale and theme with
each screenshot/recording. Do not infer acceptance from the adapter tests.

1. Open the sidebar. Verify actual titles/order and initial content match the selected
   tab. Capture the complete sidebar, including the content below the selector.
2. Click each available section, then rapidly alternate three sections. The final
   background and content must match the last selection. Record a short video.
3. Tab into the selector; use arrow keys and Space/Enter as supported by SelectorBar.
   Verify visible focus and every overflowed tab can be reached. Inspect the UIA
   `VideoPlayerSectionN` elements for their actual names and selection state.
4. Switch videos with different section sets, then close/reopen the player and sidebar.
   Confirm no old background, duplicated switching or exceptions.
5. Repeat in light, dark and Windows contrast themes, including switching while open.
   Check normal, hover, pressed, disabled, selected and keyboard-focus states.
6. Repeat at Windows display scales 100%, 150% and 200%; resize the player. For long
   title/narrow reusable-control validation, use a separate test host with long item
   text and a 220-DIP container. Horizontal scrolling and keyboard navigation must
   expose all items; compare the background to the selected item's actual bounds.
7. Disable Windows animation effects while a transition is active, then change tabs.
   Placement must be immediate. Restore the user's original setting afterwards.

Native checks must be recorded separately from the automated results. In particular,
an app launch or readable UIA tree alone does not prove selection/content agreement.

## Implementation-session result (2026-11-06)

- Adapter regression suite: 9 passed, 0 failed, 0 skipped; TRX counters parsed.
- Release/x64 normal build with the blocking analyzer: passed, 0 warnings/errors.
- Windows Sandbox is unavailable: WinApp reports the feature must be enabled.
- Local WinApp packaged launch was attempted. Building referenced resolver libraries
  with the packaging flag failed because they have no application manifests. Using
  the already built references allowed the application MSIX to build (one environment
  warning: missing `mspdbcmf.exe`, so no symbols package).
- Activation then failed because the release package identity is already installed.
  The installed application was **not** removed or replaced. A second attempt using
  a repository-external manifest with identity `Mang.BiliCopilot.Spec159` failed
  registration with `0x80073CF0` / `0x80070003` (manifest path not found).
- Consequently, **native UI acceptance remains unverified**: no screenshot/recording
  of this implementation, no real input/content comparison, no UIA selection check,
  and no light/dark/contrast, 100%/150%/200% DPI or motion-preference visual acceptance.
  The procedure above is the remaining acceptance checklist, not a passed test report.


## Review repair and native host evidence (2026-11-06)

Base: `72d79928` (including the explicit Dark dictionary), verified as HEAD before
editing and as an ancestor of this repair. No checkout was created.

The installed `Microsoft.WindowsAppSDK.WinUI/2.6.4` generic template confirms that
`SelectedPointerOver` and `SelectedPressed` use the same foreground and animation;
neither consumes the proposed pointer border resources. Resource overrides therefore
cannot distinguish them. The repair uses a small item-only visual template with the
native `CombinedStates` and `DisabledStates` names. The SelectorBar template and C#
input/selection code are unchanged. Hover and pressed now target the visible feedback
border; pressed also changes text/icon foreground. High Contrast uses system
Window/WindowText/Highlight/HighlightText brushes. Selection uses the shared moving
background and accent text, without changing font metrics. Disabled text and native
system focus visuals remain separate from selection.

A real packaged host also exposed a startup failure: the existing `BasedOn` lookup
for `DefaultSelectorBarItemStyle` was unresolved at runtime. Removing the dependency
on that internal style key allowed the linked production XAML to render. The host
was created with `winapp new`, pinned to WindowsAppSDK 2.6.4, and links the production
SlidingTabBar XAML/C# directly. It is NOT the ordinary video player; its content is a
label updated from the control's public selection event.

Evidence root on the implementation machine:
`C:/Users/hp/AppData/Local/Temp/spec159-fix-evidence/`.
Host source: `C:/Users/hp/AppData/Local/Temp/spec159-native-host/`.
Launch: `winapp run <host-directory> --arch x64 --detach --json`.
The batch scripts contain the captured local PID/HWND; refresh them from launch and
`winapp ui list-windows` before replay. Do not reuse them against another process.

| Check | Actual result / evidence |
| --- | --- |
| Adapter regression | Passed: 9, Failed: 0, Skipped: 0; `regression.trx` counters parsed. Same command above plus `--logger "trx;LogFileName=regression.trx" --results-directory <evidence-root>`. |
| Final normal Release/x64 build | Exit 0, 0 warnings, 0 errors; default blocking analyzer unchanged. Log: `C:/Users/hp/AppData/Local/Temp/spec159-fix-final2-build.log`. First clean build emitted one existing unused protobuf import warning in the external kernel; final incremental compilation was clean. |
| Actual application launch | Parent's supported Debug launch also failed: `Mang.BiliCopilot.Dev` is already installed as a non-development package. See `C:/Users/hp/AppData/Local/Temp/spec159-parent-launch.log`. Neither Dev nor Release installation was replaced. |
| Final native host batch | **6 passed, 2 failed**; `final/native-results.json`, `ui-tests-final.ps1`. This is not full acceptance. |
| Native mouse / selection / host label | Passed before remount. `final/dark-selected.png`, `final/selection.json`; the filenames inherit the script labels, but this particular selected screenshot is actually Light. |
| Native names, selected, disabled | UIA tree and property reads expose SelectorBarItem names, IsSelected and IsEnabled; `final/final-tree.json`. Narrator speech itself was not tested. |
| Theme and focus rendering | Visually inspected all final PNGs. `final/initial.png` shows Dark; `final/light-selected-hover.png` shows Light; `final/keyboard-focus.png` shows the native focus outline. Both use 150% actual display scale. |
| Hover / pressed | Final hover visibly changes the selected fill. `interaction.mp4` and its timestamped frames show a held selected press changing foreground at ~983 ms versus hover at ~463 ms on the earlier semibold iteration. That recording predates the final accent-text change and is not final motion acceptance. `final/selected-pressed.png` was captured after the gesture lock released, so it is NOT pressed-state proof. |
| Empty list | `final/empty.png` shows no floating selection background. |
| Narrow/long keyboard flow | Final automated sequence timed out before selecting Long3; it reached Comments instead. On the previous iteration, separate focus/arrow/Space operations reached AI (`narrow-selected-final.png`). Keep the final failure open; do not infer complete keyboard reachability from the earlier success. |
| Immediate remount | **Failed** after Empty �� Rebuild �� Remount �� click Comments. The host removes and reinserts the same control in one event callback. UIA reported Comments selected while the host label remained `Content: Collection`; see `final/diagnostic.png` and `final/selection.json`. The adapter remount test does not cover the actual native event ordering. |

Remaining acceptance blockers: investigate the two final native failures; verify actual
ordinary-video section/content agreement and video replacement; verify smooth rendered
motion/rapid interruption on the final revision, animations disabled and changed during
transition, High Contrast, 100% and 200% display scale, and window resizing. System
`UISettings.AnimationsEnabled` was read as True; no OS motion/theme/DPI settings were
changed. The 10-second recording achieved about 20 fps (67% of requested 30 fps), so it
must not be presented as timing-accurate proof of a 180-ms transition.

The native testing skill's two fix-and-rerun limit was reached; the remaining failures
are reported rather than silently marked passed. No issue/PR was closed or pushed.

## Focused native diagnosis and lifecycle repair (2026-11-06)

Prepared checkout `t3-3560a7e9` started at `f073b511256503d8ec3af5f8a21296db0034bca5`,
verified against `integration/spec-158`. The isolated host now links this checkout's
production XAML/C#. Installed Dev/Release packages were not modified.

Evidence root: `C:/Users/hp/AppData/Local/Temp/spec159-focused-evidence/`.
`native-trace.txt` records native/public selection, focused AutomationId, lifecycle,
IsLoaded and subscription state. Copies of `MainPage.xaml.cs`, `MainPage.xaml`, and
`SlidingTabsAcceptance.csproj` preserve the instrumented host. Diagnostics stay
outside production. The runnable host remains at the previous temporary host path.

### Causes established before fixes

- **Remount is a production defect.** At 23:10:02.674 Loaded arrived while the old
  `_attached=true`, so attachment was skipped. At 23:10:02.685 the old Unloaded
  arrived with `IsLoaded=true` and detached selection handling. At 23:10:03.242
  native selection became Comments, with no public event and content still Collection.
  Native immediate remove/insert can deliver Loaded before the previous Unloaded;
  the original adapter only exercised Unloaded followed by Loaded.
- **Keyboard failure is a test input assumption.** At 23:10:21.649 Right reached
  Long0 but did not move after UIA programmatic focus. Space activated it; the two
  remaining Rights reached Long2. An unmodified stock SelectorBar reproduced this
  at 23:11:33.153 (Plain0). Activating with Space BEFORE three Rights reaches Long3
  and updates host content to AI. Actual Tab entry also passed: focus Remount,
  `tab tab left left left`, then assert Long0 focus and its host content
  (`tab-entry.json`, trace at 23:13:43). No keyboard/template workaround was added.

The lifecycle fix releases old subscriptions and animation, then reattaches if
IsLoaded already indicates a live tree. It advances the mount generation, retaining
protection against queued old motion-preference callbacks. Genuine unload still
cleans up and hides the indicator.

### Verification

- `DeferredUnloadAfterImmediateReinsertKeepsContentSynchronizedOnce`: RED on old
  source (9 passed / 1 failed, `red.trx`), GREEN with the fix. The public content
  assertion caught the defect. Also checks exactly one notification, visible
  indicator, and no notification/hidden indicator after genuine unload.
- `dotnet test tests/BiliCopilot.SlidingTabs.Tests/BiliCopilot.SlidingTabs.Tests.csproj`
  plus TRX output: **Passed: 10, Failed: 0, Skipped: 0**, `green.trx`. Existing
  regressions retain ordinary remount, header command wiring and stale callbacks.
- `ui-tests.ps1 -AppPid <fresh local host PID>`: **4 passed, 0 failed**,
  `native-results.json`. Stock SelectorBar focus comparison; activated long-title
  keyboard traversal with focus, selection, viewport and host content assertions;
  five Empty/Rebuild/immediate remount/click cycles; keyboard content update after
  remount. The script checks native exit codes and assertion timeouts.
- Fixed trace at 23:12:55.627 and 23:12:57.442 captures delayed Unloaded with
  `IsLoaded=true`, now `_attached=true`; each subsequent Comments selection raises
  exactly one public event. The remaining three cycles show normal event ordering.
- Inspected every final PNG: `long-keyboard.png`, `remount.png`,
  `remount-keyboard.png`. AI is fully visible and focused in the narrow viewport;
  Comments/AI agree with host content after remount. Light, actual scale 1.5.
  The preceding long title is intentionally scrolled partially outside the viewport.
- Normal Release/x64 build using the command above: exit 0, **0 errors**, no WinUI
  analyzer warnings. One existing external protobuf unused-import warning in
  `pgc/gateway/vega/v1.proto`; `build.log`. Analyzer gates unchanged. Host build has
  four existing generated sample ViewModel warnings (MVVMTK0045/WUI3001).

These results resolve the two focused failures, not full #159 acceptance. Actual
player launch remains blocked by installed non-development Dev/Release identities.
Real ordinary-video content and video replacement, final rendered motion/rapid
interruption, OS animations disabled/dynamic changes, High Contrast, 100%/200% DPI,
and resizing still require the parent verification pass. No OS preferences changed.
The isolated host content label is not proof of real video content. No further
visual redesign, issue closing, PR push or monitoring was performed.

## Hover clarification (2026-11-06)

User feedback supersedes the earlier hover-fill acceptance: selected and unselected
pointer-over states now change text/icon foreground only. They do not fill or outline
the item, so the shared moving selection background remains visible throughout its
transition. Pressed and keyboard-focus feedback are unchanged. Earlier screenshots
of hover fills describe the superseded implementation.
