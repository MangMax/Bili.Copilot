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

## Window and setting behavior

`AutoCompactOverlayRatio` is a separate global, default-on local setting, using
the existing settings store and a shared preference instance. It never reads or
writes position-persistence keys. Turning it off does not restore old sizes.
Turning it on adapts all current stable compact windows with known dimensions.

The ratio coordinator uses physical-pixel client bounds and available work area.
The adapter accounts for the actual non-client frame, preserves the top-left
anchor and current longer client edge where possible, and scales down to fit.
After `MoveAndResize`, it accepts the presenter's size constraints and only
corrects a fully hidden position; there is no resize correction loop. The existing
video renderer remains responsible for aspect-preserving rendering/letterboxing;
this feature does not set panscan, zoom, cropping, or aspect override options.

Compact windows only have two forms: landscape 16:9 and portrait 9:16. Every source
ratio is mapped onto one of them — square or wider content uses the landscape form,
anything taller uses the portrait form — while the current longer client edge is
kept. Square, 4:3 and other arbitrary ratios therefore never produce a near-square
window, which keeps the overlay controls in a layout that was actually designed;
the renderer letterboxes the remaining difference. A source that maps to the same
form as the current one does not resize again, so a manual size survives it.

Position policy (also used by the #156 placement session): a remembered compact
position is restored as-is while any part of the window is visible, including a
deliberate overhang past a screen edge. Only a window that would be entirely
outside its work area (monitor removed, stale coordinates, or a size the OS
constrained to another display) is pulled back inside, and that recovery uses the
window's final physical size rather than the size captured before ratio fitting.
This is what keeps a portrait mini window flush against an edge: clamping with the
pre-fitting landscape width used to leave a gap as wide as the size difference.

Entering compact overlay repositions the window in the same synchronous batch as
the presenter switch, so the compact window never appears at the ordinary window's
location and then jumps to the remembered one. In that same batch, once the
presenter reports compact, the known ratio is fitted immediately
(`CompactOverlayRatioSession.ApplyNow`) instead of waiting for the 500 ms settle;
otherwise a portrait video would first show the presenter's default landscape
mini size and then snap to portrait. The placement session re-checks the position
after the presenter settles, since crossing monitors can change the OS-constrained
size.

Leaving compact overlay restores the remembered ordinary window geometry in the
same batch as the presenter switch, using the existing #156 ordinary settings
(`PlayerWindowPositionLeft/Top`, `PlayerWindowWidth/Height`, maximized state).
A compact toggle therefore returns to exactly the size and place the ordinary
window had before, instead of keeping the mini size or position. Ordinary settings
are never written while compact, so the compact geometry cannot overwrite them.

Presenter changes disable adaptation until the latest `PresenterChangeSession`
settles. A superseded transition or closed window cannot apply geometry.
Position persistence remains owned by #156.

## Verification limits and Windows checklist

Automated tests exercise the production coordinator, shared preference, and real
native adapter code with controlled backend/timer substitutes. They do not prove
Windows presenter behavior or real decoder notifications. The worktree does not
contain a libmpv DLL; no real app playback or GUI acceptance was performed.

Before final acceptance, use the packaged app and real media to check:

- Landscape, portrait (including rotation metadata), square, non-square pixels,
  and non-standard aspect ratios on each available internal decode mode.
- Output changes and delayed dimensions while playing and paused; switch videos,
  parts and quality rapidly, then close during pending transitions.
- Toggle off, resize manually, toggle on; repeat with two player windows and
  after a normal application restart.
- CompactOverlay minimum/maximum sizes, letterboxing with the default renderer,
  visible controls, negative monitor coordinates, monitor removal and mixed DPI.
- With #156 integrated, restored position precedes ratio fitting, off-screen
  correction is saved stably, and normal-window geometry remains independent.
