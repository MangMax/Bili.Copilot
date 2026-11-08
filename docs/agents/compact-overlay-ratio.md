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
   activation and the presenter untouched while DWM stops displaying it.
2. Apply the target presenter once.
3. Compact target: read the OS compact baseline (presenter size at the current
   origin), compute the complete target from the remembered origin, the known
   media form and the work area, apply it, wait for two composition frames where
   the presenter and bounds match, and recover the origin only if the final frame
   is entirely outside the work area.
4. Ordinary target: apply the complete ordinary rectangle (and the maximized
   state) and wait for the same composed result.
5. Only then set the stable mode and uncloak. No geometry, presenter or persistence
   write happens after the window is visible again.

`WaitForPresenterAsync`/`WaitForBoundsAsync` use `DwmFlush` composition
boundaries, not a fixed delay. They verify the presenter kind and the actual
window rectangle for two consecutive frames; the bound is a small frame budget,
not a 500 ms layout assumption. `SetPresenter` is synchronous in the Windows App
SDK, but that does not mean DWM has composed the last frame, so the confirmation
stays.

A newer request increments a generation and cancels the previous transition's
token. A superseded transition cannot uncloak, show, move or persist for the newer
one; the newest transition inherits an existing cloak and is the only one allowed
to remove it. Closing cancels the transition and never touches the window again.
A failure in the latest transition still uncloaks and records the actual presenter
as stable.

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
- A transition never writes either key.

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

This probe is not the packaged media app. It does not prove playback, real
decoder timing, multi-monitor removal, mixed DPI, focus restoration or maximized
transitions. Those remain part of the Windows acceptance checklist.

## Verification

Automated coverage (this change): 1114 UI tests, 25 popular-card tests, 291
bili-kernel tests and 42 search-recovery tests passed; the default Release/x64
WinUI build has 0 warnings and 0 errors, and the blocking WinUI analyzer build has
0 WUI warnings (239 note-level entries, the repository baseline). Tests cover both
directions, rapid reverse transitions, a superseded completion, close during a
transition, initially hidden windows, partial/full off-screen recovery, negative
coordinates, maximized ordinary restore, disabled/enabled ratio, old media
callbacks, and the absence of geometry writes while visible.

Real-app checks still required with the packaged app and real media:

- Landscape, portrait (including rotation metadata), square, non-square pixels and
  non-standard aspect ratios on each internal decode mode.
- Output changes and delayed dimensions while playing and paused; switch videos,
  parts and quality rapidly, then close during pending transitions.
- Toggle compact -> ordinary -> compact quickly, both directions repeatedly, and
  confirm no visible pre-move, edge flash or post-show correction.
- Toggle off, resize manually, toggle on; two player windows; normal restart.
- Negative monitor coordinates, monitor removal, mixed DPI, focus/activation,
  maximized ordinary restore, and visible controls/letterboxing in compact mode.
