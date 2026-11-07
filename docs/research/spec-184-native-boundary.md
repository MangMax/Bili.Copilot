# #184: native observability boundary assessment

Result: **no production recovery implemented**. The current public native API and
the inspected logs do not provide a validated, attributable terminal main-input
failure signal. This extends, rather than repeats, the
[cache-only rejection](spec-184-detector-gate.md): a private adapter is feasible,
but preserving log prefixes or reading more native cache fields does not supply
the missing distinction. No dependency upgrade, native build, fork, public app
interface change, or timing/fps workaround was made.

Base: `cf1599475bcb30fa0d686dd7c2771f25751a96fe`, integration branch
`integration/spec-182-playback-freeze`. Full #182/#184 bodies and comments were
read via `gh`. This follows the ticket's explicit instruction to return concrete
boundary evidence if the detector gate fails. It does not complete #184 or #182.

## Existing wrapper permits a private native adapter

The installed NuGet packages and `src/Directory.Packages.props` agree on
`0.0.2-preview2`. The installed Core nuspec identifies source commit
`c1ef0f403097129c3b6db4c466cc4147054471d2`; the installed assembly XML documentation
also lists the following APIs. Inspection covered the pinned implementation, not
only `MpvCacheStateEventArgs`:

- `MpvClient.Handle` is public. `MpvNative.GetProperty`/`GetPropertyString` can read
  arbitrary native property names; the wrapper's property enum is not the limit.
- `MpvNative.CreateClient` and `CreateWeakClient` create an independent event queue
  on the same player core. `RequestLogMessages`, `WaitEvent`, and
  `MpvEventLogMessage.Prefix` already exist. An app-private adapter could preserve
  prefixes without upgrading the wrapper. It must own/destroy that child handle
  and never compete with the wrapper's `WaitEvent` consumer on the original handle.
- `MpvClient.Events.cs` drops `Prefix` before calling `ILogger`, forwarding only
  level and text. A logger decorator cannot recover discarded attribution.
  Its `ErrorOccurred` text mapping does not recognize the reproduced terminal
  transfer failure. `ReachFileEnd` is whole-file, not per-video-input termination.

Sources: [client](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.cs),
[native client API](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Interop/MpvNative.Client.cs),
[event adapter](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.Events.cs).

This removes a possible wrapper-only blocker. It does **not** establish that the
native information retrieved through that adapter is sufficient.

## Native properties and state actually inspected

The real DLL is unchanged: mpv `v0.41.0-1386-g2137d21b2`, embedded FFmpeg
`N-128281-ge19fadf1d`. Hashes are in the
[compact evidence](spec-182/native-boundary-summary.json). Each run records the
actual `property-list` and samples native properties directly through libmpv.

| Candidate | Native implementation and measured limitation |
| --- | --- |
| `demuxer-cache-state` including `eof`, `ts-per-stream`, `debug-ts-last`, `reader-pts`, `total-bytes` | More fields than the wrapper event, but still no terminal reason or last-packet duration. `ts-per-stream` describes streams of the main demuxer, not external audio health. End timestamps derive from queue `last_ts`; byte totals describe buffered packet allocations, not complete resource receipt. |
| `file-size`, `stream-end`, `stream-pos` | Size is primary resource size; `stream-pos` comes from the packet offset returned to the reader, not bytes received or consumed from HTTP. Healthy sparse video ends with size 18,323 and position 9,428. A valid trailing `free` atom increases size to 1,066,907 while position stays 9,428 and decoded video is identical. A byte-gap threshold is not completeness. |
| `track-list`, `current-tracks/video` | Identify selected main video (`external=false`), codec and `demux-duration`, but expose neither expected/received sample count nor terminal error/completion reason. Sparse duration correctly reports 20 seconds; that alone cannot tell whether the packet at PTS 10 lasts 10 seconds. |
| `video-frame-info`, estimated frame counts/fps | Frame info exposes picture type, flags and timecodes, not packet duration/presentation end. Estimates are not sample-table completeness. No fps cutoff was used. |
| `video-status`, `video-eof`, `current-tracks/video/eof`, `video-frame-info/duration` | No supported property with the required semantics: the first two are absent from the actual property list; all four probes return unavailable in every sample. |
| `eof-reached` / end-file | Global EOF is the conjunction of audio and video status. Audio continues during the failure, so this does not report the video failure at onset. |

These conclusions also follow from pinned native source:
[property getters and registration](https://github.com/mpv-player/mpv/blob/2137d21b2/player/command.c)
(`mp_property_stream_pos`, `mp_property_video_frame_info`,
`mp_property_demuxer_cache_state`, `mp_property_eof_reached`), and
[demux state](https://github.com/mpv-player/mpv/blob/2137d21b2/demux/demux.c)
(packet-position assignment and `demux_get_reader_state`).

### Exact native video EOF is not final-frame presentation end

The native state exists privately as `MPContext.video_status`, but simply exposing
it would not solve this case. `player/video.c` sets `logical_eof` when no more
decoded frames can arrive. After queueing the last frame, if an audio chain exists,
it can set `STATUS_EOF` immediately (`logical_eof && !num_next_frames && ao_chain`).
That path does not wait for the last packet's presentation duration. The separate
draining path does use `last_frame_duration = pkt_duration / video_speed`, but
neither that duration nor a guaranteed presentation-complete event is public.
[Pinned video state implementation](https://github.com/mpv-player/mpv/blob/2137d21b2/player/video.c#L1344).

Real-DLL confirmation: the healthy sparse video plus failing audio logs
`video EOF (status=4)` at wall time 10.276 s, while the nearest sampled playback
position is 0.099973. Its two actual MP4 samples are PTS 0 and 10, each duration
10. Later at position 10.543275 the final frame is still valid. Thus even an
exact native EOF log/state, combined with an apparent duration gap, is insufficient.
`vf` filter input/output EOF occurs even earlier because decoding runs ahead.
The `video EOF reached` message from the draining path is not emitted for every
transition; requiring it would also miss the original fault in these runs.

Instrumentation control: repeating the late-audio failure with `--option vf=`
(no diagnostic video filter) still logs native `STATUS_EOF` at wall time 10.181,
nearest position 0.000223, duration 20. The video bytes match the sparse baseline,
and the video HTTP response is complete. Thus the early state transition is not
introduced by `showinfo`. This extra run has no frame-checksum oracle and is used
only to verify the state/log observation. In the filtered run, `showinfo` itself
reports `duration_time:0` for both sparse frames, so its diagnostic text cannot
substitute for their independently verified 10-second MP4 sample durations.

### Raw log prefix does not attribute the failed input

Both primary-video truncation and external-audio truncation produce:

- prefix `curl`, text `transfer failed: Transferred a partial file`;
- prefix `ffmpeg/demuxer`, text naming MP4 `stream 0` and `partial file`.

`stream 0` is local to each separately opened container, not the selected mpv
video track ID. Both inputs use the same generic component prefixes. Main and
external sources are independently opened with `demux_open_url`; the failure
messages carry no media-session/source identity. Concurrent log proximity to an
opening message does not provide reliable correlation.
[External input opening](https://github.com/mpv-player/mpv/blob/2137d21b2/player/loadfile.c#L962).

The mandatory negative control was strengthened: deliver 75% of audio before
truncating it, leaving enough audio to progress past PTS 10.5, with completely
healthy sparse video. At wall time 20.756 s, position 10.543275, all conditions of
the previously rejected cache predicate hold, global EOF is false, and a terminal
`curl` failure has already occurred. The main video's one HTTP response was
complete; only the six audio replies were truncated. Adding a latched generic
terminal error, even with its raw prefix and exact video EOF, would still admit a
false recovery. The assessment computes this **counterfactual match** from real
samples; it does not implement or execute a new reopen detector.

## Real-player matrix and executable assertions

Null sinks, software decode and `showinfo` remain as in #183. These prove real
decoder output, not WinUI display/audio-device behavior. No automatic reopening
is enabled in this matrix, so absence of reopening is not a repair acceptance claim.

| Case | Observed evidence |
| --- | --- |
| Original fault, server restored at 12 s | 235 video frames, last PTS 7.8; audio passes 23.5; no video recovery. Original progression oracle remains red. |
| Persistent outage | Same 235-frame termination; no later video requests. No recovery or bounded UI failure behavior is claimed. |
| Healthy sparse | Exactly two distinct frames; durations independently read from MP4 `stts` are `[10,10]`; natural global end. |
| Healthy variable-duration | Four distinct frames at PTS 0,1,3,13; verified sample durations `[1,2,10,1]`; duration 14; natural global end. |
| Legitimate short video | 240 frames through PTS 7.966667, primary duration 8, separate audio passes 23.5. |
| Natural end | 720 frames through PTS 23.966667 and global EOF. |
| Fault paused at 5 s | Position stable after pause; failure log/cache EOF alone do not imply an active freeze. |
| Audio-only failure + healthy sparse | Both valid video samples decoded; six audio truncations and zero video truncations; generic-error/cache/native-EOF combination has a false-positive match. |
| Healthy sparse + trailing MP4 padding | Same two frames and packet offset; complete HTTP response despite file-size minus packet-offset gap exceeding 1 MiB. |

[`assess-boundary.py`](../../scripts/diagnostics/playback-freeze/assess-boundary.py)
asserts each row, verifies the actual generated MP4 sample tables, checks absent
properties, and checks the shared log prefix and false-positive match. It emits
only synthetic aggregates; media and raw logs stay private.

Reproduction uses `repro.py` with `--inspect-boundary` and the DLL/FFmpeg paths
from #183. Use a fresh private root; subdirectories below are case names consumed
by `assess-boundary.py`:

| Directory | Additional arguments |
| --- | --- |
| `fault-debug` | `--seconds 27` |
| `sparse` | `--scenario control --video-rate 1/10 --seconds 36` |
| `variable` | `--scenario control --variable-video --seconds 27` |
| `short` | `--scenario control --video-seconds 8 --seconds 27` |
| `natural` | `--scenario control --seconds 27` |
| `pause` | `--action pause --action-at 5 --seconds 12` |
| `unavailable` | `--release-at 999 --seconds 27` |
| `audio-sparse-late-debug` | `--fault-track audio --fault-fraction 0.75 --video-rate 1/10 --seconds 36` |
| `padded` | `--scenario control --video-rate 1/10 --trailing-free-bytes 1048576 --seconds 36` |
| `audio-sparse-no-filter` | `--fault-track audio --fault-fraction 0.75 --video-rate 1/10 --seconds 36 --option vf=` |

Seven summarized control runs used verbose logging; the final
`fault-debug` and late-audio run use debug logging to observe numeric native EOF.
The current `--inspect-boundary` requests debug, so repeating the table also
captures that state for the controls. Missing debug messages in the older runs
are not interpreted as absence of native EOF. An exploratory trace-level run
lost startup logs under its high volume and is excluded from the summary and
assertions; the retained debug runs contain both transport failure and EOF logs.

All ten retained runners exited 0. The assessment (including the unfiltered
instrumentation control) exited 0; the original progression assertion exited 1
as expected. Python compilation and `git diff --check` passed. These are
diagnostic evidence checks, not passing application-recovery tests.

```powershell
python scripts/diagnostics/playback-freeze/assess-boundary.py $privateRoot --output $summary
python scripts/diagnostics/playback-freeze/analyze.py "$privateRoot/fault-debug/result.json" --expect progressing
# Second command must exit 1: production fault remains unrepaired.
```

## Smallest actionable next change: preserve terminal input error natively

The required distinction exists **before it is erased**:

1. `stream_curl.c` completion keeps `stream_eof` and `stream_error` separately;
   retry exhaustion sets `stream_error`. `curl_fill_buffer` drains buffered bytes
   and then returns zero for either state. Its AVIO adapter likewise maps a
   nonpositive read to `AVERROR_EOF`.
2. `demux_lavf.c` receives `AVERROR_EOF` and returns false. Demux/cache state can
   consequently mark an incomplete range as terminal EOF. Neither the public
   properties nor logs retain a reliable source-associated reason.

Sources: [transport completion/read](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_curl.c#L880),
[AVIO read](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_curl.c#L1313),
[demux read](https://github.com/mpv-player/mpv/blob/2137d21b2/demux/demux_lavf.c#L1766).

Regrade scope before starting a native change. The next bounded native work item
should preserve terminal read outcome from the transport through the owning
demuxer and expose a structured **primary-input** status/property. Proposed
contract to settle there:

- Distinguish running, clean EOF, exhausted/error, and cancellation; retain an
  error code/reason without raw URLs. Do not confuse a recovered retry with
  terminal failure or an external audio failure with primary-input failure.
- Associate status with the loaded source/generation; reset on a new open, and
  define seek/cancel/cache-reuse behavior explicitly. Preserve the reason even
  while already-buffered video continues to play.
- Exercise this exact real-DLL matrix, transient failures, and switch/seek/cancel
  transitions against the patched build before selecting an app recovery trigger.
  Verify both curl and the alternative lavf transport, or declare supported
  backend eligibility explicitly.
- Then the existing wrapper's public native getter and independent observer
  client can consume it through an app-private adapter. A wrapper-only prefix
  change or bare `video_status` exposure is not enough. Keep recovery preservation,
  retry bounds and stale-operation guards as the separate remaining #184 work.

This is an actionable native boundary proposal, **not a built or validated patch**.
Parsing MP4 sample tables in the production app or independently re-downloading
sources would add a container/network subsystem and was not smuggled into ≤M.
The tiny sample-table reader here verifies generated fixtures only. No proof is
claimed that every conceivable log/codec heuristic is impossible; none inspected
provides the required dependable main-input contract across the negative controls.

No production/.NET tests changed. .NET build, WinUI analyzer and native UI
acceptance were not run for this diagnostics/documentation-only result. Session
races, preservation of quality/speed/volume/pause, danmaku synchronization and
bounded user-visible recovery remain unimplemented and unvalidated. All original
raw logs and signed URLs remain private.
