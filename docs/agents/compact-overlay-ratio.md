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

File events invalidate the media generation; application source switches suspend
sampling before stop/initialization. Loaded playlist entry IDs reject the old
file, and IDs are rechecked around a repeated dimensions sample. Disposal stops
the timer and detaches backend events before the client is disposed.

## Window and setting behavior

`AutoCompactOverlayRatio` is a separate global, default-on local setting, using
the existing settings store and a shared preference instance. It never reads or
writes position-persistence keys. Turning it off does not restore old sizes.
Turning it on adapts all current stable compact windows with known dimensions.

The ratio coordinator uses physical-pixel client bounds and available work area.
The adapter accounts for the actual non-client frame, preserves the top-left
anchor and current longer client edge where possible, and scales down to fit.
After `MoveAndResize`, it accepts the presenter's size constraints and only
corrects off-screen position; there is no resize correction loop. The existing
video renderer remains responsible for aspect-preserving rendering/letterboxing;
this feature does not set panscan, zoom, cropping, or aspect override options.

Presenter changes disable adaptation until the latest `PresenterChangeSession`
settles. A superseded transition or closed window cannot apply geometry.
The integrated #156 placement session finishes position restoration before
enabling ratio adaptation at the end of the same presenter transition. Position
persistence remains owned by #156.

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
