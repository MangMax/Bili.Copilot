# CompactOverlay ratio (#157)

## Backend evidence

The pinned `Richasy.MpvKernel.Core` 0.0.2-preview2 package identifies source commit
`c1ef0f403097129c3b6db4c466cc4147054471d2` in its NuGet repository metadata.
Its [MpvClient](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.cs)
exposes the native handle, and its
[events](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.Events.cs)
expose file loading/loaded/end, but do not publish video dimensions.

`PlayerViewModel.InitializeInternalAsync` uses this client for Auto, D3D11,
D3D12, NVDEC, Vulkan, Software and custom-config decoding. The Windows
`MediaPlayer` instance is only for SMTC; it is not an additional playback backend.
External players never produce these internal media samples.

`MpvMediaDisplaySource` is the only native adapter for this feature. It reads
`video-out-params/dw`, `dh`, and `rotate` through the pinned native API.
The [mpv property contract](https://mpv.io/manual/master/#property-list) defines
dw/dh as aspect-corrected output dimensions after filtering, with remaining
rotation reported separately. These are not storage dimensions, cover size,
OSD dimensions, or window dimensions. Invalid/unavailable properties mean wait.
A 500 ms UI timer provides changes even while paused because the pinned wrapper
has no output-size event. Equal ratios do not resize again.

File end and subsequent loading events invalidate the media generation; the first
loading event after application initialization preserves its prepared API hint.
Application source switches suspend sampling before stop/initialization. Loaded playlist entry IDs reject the old
file. The [mpv playlist property contract](https://mpv.io/manual/stable/#property-list)
provides `playlist-playing-pos` (the playing slot, or -1 during idle/transitions)
and `playlist/N/id` (the entry ID unique for the core lifetime). There is no
`playlist-entry-id` property; `playlist_entry_id` is an event field. The adapter
checks both the slot and ID during resolution, and rechecks the ID around a
repeated dimensions sample. Reordering preserves identity; reusing a slot for a
different entry does not. Suspension retains the last loaded ID if the playing
properties are already unavailable, so a delayed old loaded event stays rejected.
Disposal stops
the timer and detaches backend events before the client is disposed.

## Early selected-stream dimensions

`VideoConnectorViewModel._part` / `PgcConnectorViewModel._episode` initialize the
corresponding resolver. The ordinary video REST play request uses that part's
CID; PGC uses the current episode's CID and episode ID. Both REST response paths
map `PlayerInformation.Dash.Video[].Width/Height` into `DashSegmentInformation`.
After the existing quality and codec selection, the resolver uses **that same
selected video segment** for both the URL and dimensions. No first-part lookup,
cover dimensions or `MediaAspectRatio` enum is involved; no kernel changes are
required.

`PlayerViewModel` supplies a generation-captured metadata publisher to the
resolver. `GetSourceAsync` captures it before awaiting the API, then awaits its
UI dispatch before returning `MpvMediaSource` to the player for loading. The
publisher forwards the hint to the existing window ratio coordinator. Zero or
invalid dimensions keep waiting. The first load preserves the hint so an equal
runtime ratio does not undo manual sizing during startup. The first valid mpv
output sample takes priority even when its ratio equals the hint; later API
hints cannot overwrite it. Suspend, file end, later loads and disposal invalidate
queued/late hints. The global preference and duplicate-ratio behavior remain
owned by the same coordinator.

The DASH model exposes no rotation or sample-aspect-ratio contract. API width and
height are therefore provisional, not authoritative display output: no rotation
is guessed. mpv's aspect-corrected output and remaining rotation correct the hint.
Missing API dimensions, audio-only sources and backend-driven reloads without a
fresh application initialization continue to wait for runtime output. The unused
gRPC adapter currently creates zero dimensions and would likewise wait if that
API path were selected in future.

## Window coordination and setting behavior

### Root cause of the old flicker

The old implementation had several independent writers for one presenter change:
the placement session moved the still-ordinary-size window to the remembered compact
origin *before* `SetPresenter`, the presenter switch restored/resized the window,
the ratio session immediately resized it again, and a fixed 500 ms completion moved it
again after the switch. Because the pre-move also changed the OS's remembered
overlapped geometry, leaving compact first restored a large window at the compact
origin and only moved it back on the delayed correction. The hide/show patch added
after that attempt still showed the window inside the presenter batch, so it could
reveal a frame before the final geometry was composed.

### One owner

`PlayerWindowPresentation` (pure, in `Toolkits/Pure`) is the only owner of the
logical mode, the target geometry, visual protection and persistence timing. The
WinUI adapter (`PlayerWindow.Presentation.cs`) is the only code that calls
`SetPresenter`, `MoveAndResize`, DWM cloak or `Show`. External code only requests
a mode (`RequestModeAsync`) or reports a media form; it never compensates with
`Move`/`Resize`.

The three logical modes are explicit: `Ordinary`, `CompactOverlay` and
`FullScreen`. A transition captures the stable source first:

- leaving ordinary: the exact physical ordinary rectangle and the maximized state
  (when maximized, the restore rectangle comes from the saved ordinary geometry);
- leaving compact: the final stable compact origin is persisted *before* the
  presenter changes;
- leaving full screen: no geometry snapshot, the ordinary snapshot/state is the
  target.

### Transition sequence

1. If the window is visible and the transition involves compact overlay, cloak it
   with `DwmSetWindowAttribute(DWMWA_CLOAK)`; this leaves `AppWindow.IsVisible`,
   activation and the presenter untouched while DWM stops displaying it. When DWM
   rejects the attribute the adapter falls back to `Hide` and restores the
   foreground state on reveal.
2. Apply the target presenter once.
3. Compact target: read the OS compact baseline (presenter size at the current
   origin), compute the complete target from the remembered origin, the current
   media form and the work area, apply it, wait until it settles, and repeat with
   the latest form until the confirmed frame already is the target. A media-form
   change that arrives during a wait is picked up by the next iteration, so the
   newest input owns the final frame (there is no separate dirty flag).
4. Ordinary target: apply the complete ordinary rectangle, wait until it settles,
   then (when the ordinary state is maximized) maximize and wait for the maximized
   frame again. Only the final maximized frame is ever exposed.
5. Only then set the stable mode and reveal. No geometry, presenter or persistence
   write happens after the window is visible again.

`WaitForLayoutAsync` is one completion criterion for every phase. After each
composition tick the surface reads the presenter, the maximized state and the
actual rectangle; two consecutive identical samples that match the request confirm
it. `DwmFlush` is only the sampling boundary, never proof that the layout
finished, and when it fails the adapter paces the next observation, logs the
degraded confirmation and still requires the observed state to prove the result.
A stable sample that keeps a different frame (minimum size, a display clamp, the
maximized work area) is accepted only at the end of the phase budget and reported
as `Constrained` with the real frame; a sample that never matches the presenter or
the maximized state is `TimedOut`, not success. A timed-out or throwing transition
reveals the window, logs the failure, reports `Failed` with a diagnostic and
leaves no stable mode behind, so the same target can be requested again and runs
instead of being coalesced as a no-op. One phase is bounded by 400 ms and the whole
transition by 1200 ms, so protection can never become permanent.

A newer request increments a generation and cancels the previous transition's
token. A superseded transition cannot uncloak, show, move or persist for the newer
one; the newest transition inherits an existing cloak and is the only one allowed
to remove it. Closing cancels the transition and never touches the window again:
the settle loop re-checks the window and the token after every tick before it
reads a single property, so a destroyed window is never observed.

Reveal is verified, not assumed. Show does not clear an application cloak
(measured below), so the adapter clears `DWMWA_CLOAK` again, reads
`DWMWA_CLOAKED` and reports success only when the application cloak bit is clear.
It retries the attribute call a bounded number of times, and when it cannot verify
it keeps the protection and reports the failure; the coordinator then reports
`Failed` and the next request retries the reveal. A protection that could not be
applied at all is reported as a degraded completion (the mode still switches, the
result carries the diagnostic).

### Initial show

`PlayerViewModel.ShowPlayerWindow` prepares the UI and requests the default
presenter before the first `Show`. The coordinator applies the mode while the
window is still hidden, then shows it once at the final geometry. A later
`Show` only presents the current stable geometry; it no longer calls an ordinary
`MoveAndResize` that could race a compact transition or overwrite a user move.

### Persistence timing

- Compact origin: written only while the compact presenter is stable, before
  leaving compact and on close. A partially off-screen origin is preserved; only a
  fully invisible origin is recovered, using the final fitted size.
- Ordinary geometry: written only while the ordinary presenter is stable. While
  compact or full screen the ordinary keys are untouched, so the mini geometry
  cannot overwrite them. The maximized flag is independent.
- A transition writes only the compact origin it has just confirmed; the
  ordinary keys are never written by a transition.

### Ratio and position are values

`CompactOverlayRatio` holds the media generation, the selected form and the
global preference; it raises `Changed` and has no window side effects.
`CompactOverlayLayout` is pure geometry: two forms (landscape 16:9, portrait 9:16),
longer-client-edge preservation, work-area scaling, and the "recover only a fully
hidden window" policy. The coordinator asks for values and applies the result.
The video renderer still owns aspect-preserving rendering/letterboxing; this
feature does not set panscan, zoom, cropping, or aspect override options.

The old `PlayerWindowPlacementSession`, `PresenterChangeSession`,
`CompactOverlayRatioSession` (the version that changed the window), the separate
`CompactOverlayPlacement` helper, `WindowPlacementMode`, `RatioWindowBounds`,
`PlacementBounds`, `ApplyNow`, `SetCompact`, `ChangePresenterAsync`,
`IsPresenterChanging`, the 500 ms delay and the `_entering*` flags are gone. A
repository scan for those symbols returns no hits.

### Adaptive playback controls

The control has three presentation modes, selected from the existing player mode
and actual client size: `Normal`, `CompactLandscape`, and `CompactPortrait`.
Normal mode retains the original width profiles; compact landscape uses the
existing narrow band. Compact portrait uses a bottom transport row (previous,
rewind, play/pause, forward, next), a bottom-aligned right accessory column, and
progress below the transport row. Navigation availability remains a business rule.
The information card and danmaku settings are omitted in portrait; fullscreen and
exit compact remain in the overflow menu.

XAML visual states own all orientation, alignment, preferred popup dimensions and
animation directions. Leaving a state restores its original declarations; no
imperative per-button grid mutation or saved flyout-default dictionary remains.
Portrait volume contains a title, the same mute command/icon behavior as the
normal control, and a vertical slider. Portrait speed places compact presets on
the left and its vertical slider on the right. Landscape speed restores the
original six centered presets above a horizontal slider. Popup content has viewport
maximums and vertical scrolling, independently of its preferred state dimensions.

Visibility binds directly to the existing `IsControlsVisible`. Each surface uses
the original Toolkit implicit show/hide animations: playback and progress enter
from below in every state; accessories enter from the right only in portrait.
The custom Composition batch/visibility controller and `OverlayFlyoutLayout` were
removed. `XamlRoot.Changed` and player mode notifications update the visual state
even when the controls are hidden. Pointer wake-up checks use the current mode.

Settings popups share an opaque themed surface and 8 DIP padding. Motion is
selected separately by the mode's presenter style: the normal/compact landscape
styles enter from below; `PortraitSettingsPresenter` and `PortraitMenuPresenter`
enter from the right. `PlayerFlyoutMotion` only animates the presenter's loaded
visual, without managing popup visibility, timers or window geometry. It uses
WinUI `ControlFastAnimationDuration` (167 ms) and `ControlFasterAnimationDuration`
(83 ms), the Fluent direct-entrance curve `(0,0,0,1)`, and linear opacity. System
animation preference is respected; unloading stops/reset animations. Native
placement animations and the older EntranceThemeTransition are not layered on top.
Reference: https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing

Danmaku settings use the same Flyout lifecycle as speed/subtitles. The overflow
menu clears its system backdrop and uses a solid surface. Portrait volume uses
the slider template's 32 DIP width to center its track. Speed presets retain a
border and 4 DIP inset, with 4 DIP column gaps in landscape and row gaps in portrait;
unused rows/columns have no gaps. The portrait slider matches the complete six-row
frame (222 DIP). Preset clicks update exactly one selection immediately, without
the former 300 ms delay or deselect-all/reselect sequence; a rejected playback
request restores actual player state. The stock ToggleButton's brief brush feedback
is retained, without a custom moving selection indicator.

Pure tests cover mode selection, returning to normal width profiles and pointer
regions. They do not establish actual XAML rendering or animation correctness;
packaged UI evidence belongs in `artifacts/portrait-rewrite/report.md`.

## Native evidence

Official API shape (Windows App SDK 2.6.5, this machine):

- `CompactOverlayPresenter` is an always-on-top fixed-size presenter with a 16:9
  default; `InitialSize` selects Small/Medium/Large, and
  [`AppWindow.Resize`/`MoveAndResize` can override the size](https://learn.microsoft.com/en-us/windows/apps/develop/ui-input/manage-app-windows).
  [`SetPresenter`](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.setpresenter)
  applies the presenter; `AppWindow.Changed` reports the presenter/position/size
  changes.
- [`DWMWA_CLOAK`](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
  makes a window "not visible to the user"; the window is still composed by DWM.
  `DWMWA_CLOAKED` reports the cloak reason.
- [`DwmFlush`](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)
  waits for the next composition. It is the frame boundary used by the adapter.

A minimal WinUI 3 probe (plain `Window` + `AppWindow`, not the packaged app) ran
on Windows 11 at 150% DPI with two displays. It toggled an ordinary 1200x800 window
and a compact target through seven variants while recording, at `DwmFlush`
boundaries, `GetWindowRect`, `IsWindowVisible`, `DWMWA_CLOAKED`, `AppWindow.Changed`
and a GDI screen frame for visible-state evidence:

- `SetPresenter(CompactOverlay)` resized the window **in place** to 640x360; it did
  not move it. `SetPresenter(Overlapped)` restored the previous overlapped
  rectangle in the same call. This confirms that moving the ordinary window to the
  compact origin before the switch is what polluted the OS restore rectangle.
- With the cloak protection, every presenter/size/position change happened while
  `DWMWA_CLOAKED = 1`; the window stayed `AppWindow.IsVisible = true`; uncloaking
  showed the final compact/ordinary frame. No intermediate visible frame appeared
  in the frame log; a saved screen frame showed the final compact geometry.
- `Hide()/Show()` also hid the intermediate geometry, but changed
  `AppWindow.IsVisible` and activation. Cloak is used instead because it preserves
  both and keeps DWM composition alive.
- `DWMWA_TRANSITIONS_FORCEDISABLED` made no timing difference in this probe; it is
  not used. No OS presenter animation was observed here; the composition
  confirmation remains for systems/DPI combinations where the last frame is not
  synchronous.
- The frame log cannot exclude a transient shorter than the 60 Hz capture
  interval by pixels alone; the `AppWindow.Changed` timeline and the cloak state
  at every change are the complementary evidence.

A second Win32 probe (a plain `CreateWindowEx` window, per-monitor DPI aware,
3840×2160 display at 150%, work area 3840×2088) measured the protection and
maximization contract used by the new sequence:

- `DwmSetWindowAttribute(DWMWA_CLOAK, 1)` returned `S_OK`, `DWMWA_CLOAKED` read
  `0x1` (the application bit) on the same frame, and `IsWindowVisible` stayed 1.
- `ShowWindow(SW_HIDE)` followed by `ShowWindow(SW_SHOW)` left
  `DWMWA_CLOAKED = 0x1`: Show does not clear the cloak. Only another
  `DwmSetWindowAttribute(DWMWA_CLOAK, 0)` returned `S_OK` and cleared the
  application bit (`0x0`), with the rectangle unchanged.
- Maximizing the cloaked window reported `zoom = 1` and the maximized frame
  `-11,-11,3862×2110` at the first `DwmFlush` already. The maximized frame is
  unrelated to the restore rectangle, and restoring returned the saved
  `300,300,640×360` frame. That is why the ordinary path confirms the restore
  frame first and the maximized state/frame separately, and why the maximized
  rectangle is never persisted as the ordinary snapshot.
- `MoveWindow` while cloaked reported the requested rectangle on the first frame;
  the geometry did not animate, so two equal samples describe the composed state
  rather than waiting out an animation.

This probe is not the packaged media app. It does not prove playback, real
decoder timing, multi-monitor removal, mixed DPI, focus restoration or maximized
transitions. Those remain part of the Windows acceptance checklist.

## Verification

Automated coverage (this change): the full UI suite passes (1134 tests, 0 failed,
0 skipped; 13 of them are new for the completion contract), and the default
Release/x64 WinUI build has 0 warnings and 0 errors with the blocking WinUI
analyzer enabled.

New coverage for the completion contract:

- `WindowLayoutSettlerTests`: a stable but different frame is `Constrained` only at
  the deadline; two equal matching frames confirm; changing frames time out; a
  stable frame that never matches the presenter times out; the maximized
  requirement applies only to the ordinary presenter; cancellation before the
  first frame and cancellation/close during a tick never read the window; a
  composition boundary that never arrives still ends at the deadline.
- `PlayerWindowPresentationTests`: the maximized restore reveals only after the
  maximize wait; a settlement timeout reveals, logs, reports `Failed`, does not
  persist, and the same target retries; a constrained frame is accepted as the real
  geometry with a diagnostic and is not re-applied; a failed reveal keeps
  protection, reports `Failed` and retries; an initial show failure is reported and
  a later show presents the window; a media-form change during the refit converges
  to the latest form; an unprotectable window still switches mode but reports the
  degraded protection.

Earlier runs also covered both directions, rapid reverse transitions, a superseded
completion, close during a transition, initially hidden windows, partial/full
off-screen recovery, negative coordinates, maximized ordinary restore,
disabled/enabled ratio, old media callbacks, and the absence of geometry writes
while visible.

Adaptive control decision coverage: `OverlayControlLayoutTests`. The revised UI
suite passes 1150 tests (0 failed, 0 skipped). Historical rail screenshots in
`artifacts/portrait-parent` describe the superseded layout, not this rewrite.

Real-app checks still required with the packaged app and real media:

- Portrait compact overlay: every rail button reachable, scrolling when the height
  is short, flyouts opening to the left, progress drag and auto-hide while over the
  rail, and the restored band after leaving the compact overlay or resizing.
- Landscape, portrait (including rotation metadata), square, non-square pixels and
  non-standard aspect ratios on each internal decode mode.
- Output changes and delayed dimensions while playing and paused; switch videos,
  parts and quality rapidly, then close during pending transitions.
- Toggle compact -> ordinary -> compact quickly, both directions repeatedly, and
  confirm no visible pre-move, edge flash or post-show correction.
- Toggle off, resize manually, toggle on; two player windows; normal restart.
- Negative monitor coordinates, monitor removal, mixed DPI, focus/activation,
  maximized ordinary restore, and visible controls/letterboxing in compact mode.
