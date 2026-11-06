# Video player sliding tabs (#159 / spec #158)

Only the ordinary video's `VideoPlayerSideHeader` uses `SlidingTabBar`. PGC is unchanged.
The reusable control accepts `SelectorBarItem` instances with unique, non-null `Tag`
identities through `SetItems(items, selectedIdentity)`. `Select(identity)` synchronizes
caller state without raising a user event. `SelectionChanged` exposes the selected
identity; the header forwards it to the existing `SelectSectionCommand` once.

The native SelectorBar template, keyboard handling and automation peer are preserved.
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
