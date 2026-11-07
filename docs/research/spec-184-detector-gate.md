# #184: automatic recovery detector gate failed

Follow-up: [native API, source and attribution assessment](spec-184-native-boundary.md)
tests richer properties, native video EOF, and audio-only failure with healthy
sparse video. It identifies the native error-propagation boundary needed next.

Status: **production repair not implemented**. The pre-implementation gate in
[#184's approved contract](https://github.com/MangMax/Bili.Copilot/issues/184#issuecomment-6705762561)
requires returning concrete evidence instead of shipping a speculative detector.
The experiment below finds a false positive in healthy media. It does not change
the application, dependencies, settings, source selection, or URL resolution.

Base: `0f86a353a4319977c25c5898352bd5f824ff1e92` on
`integration/spec-182-playback-freeze`. See the
[original investigation](spec-182-playback-freeze.md) for the transport/EOF
responsibility boundaries. Upstream HTTP/2 failure attribution remains unknown.

## Candidate actually exercised

`repro.py --auto-reopen` samples real libmpv every 100 ms. Its initial trigger uses
the existing wrapper's available cache fields and playback properties:

- Not paused, not paused for cache, not at global EOF.
- A nonempty seekable range, `eof-cached=true`, and zero forward packet bytes.
- Primary duration exceeds the highest cache range end by **more than 2 seconds**.
- Current playback position exceeds that end by **more than 0.5 seconds**, and
  remains more than 2 seconds before the primary duration.

The first reopen is entirely condition-driven, not the old fixed 14-second action.
It reloads the same main video and external audio with the observed current
position. Only after an admitted recovery does a five-second deadline permit a
second attempt; a further five seconds without a complete cache emits one
`automatic recovery exhausted` diagnostic marker. Maximum two attempts per run.
An empty cache after a failed reopen is handled by that recovery deadline.
The marker is **not application user feedback**. These are experimental constants,
not an approved or implemented production policy. The experiment does not model
UI dispatch, pause changes during an outstanding command, seek/switch/close
isolation, quality changes, or complete application restoration of preferences.

## Real-player results

Same DLL and FFmpeg as #183: mpv `v0.41.0-1386-g2137d21b2`, FFmpeg
`N-128281-ge19fadf1d`; hashes and compact results are in
[automatic-detector-summary.json](spec-182/automatic-detector-summary.json).
Real decoding uses null sinks and showinfo frame PTS/checksums, not WinUI rendering.

| Case | Observed result |
| --- | --- |
| Original exhaustion, source healthy at wall time 12 s | Automatic reopen at 8.647 s (position 8.419860), second at 13.678 s (position 13.411777). Video reaches PTS 23.966667 with 180 distinct late frame checksums; audio reaches 23.579807. Progression oracle passes. |
| Source unavailable throughout | Two attempts at 8.650 / 13.681 s, failure marker at 18.710 s. Last video PTS 7.8, no late frames, audio reaches 23.516256. Bound assertion passes; playback is not repaired. |
| Normal 24-second video, natural end | 720 frames, no automatic reopen, global EOF. |
| Normal pause at 5 s | No automatic reopen; position stable after pause. |
| Truncated source paused at 5 s | No automatic reopen despite premature cached EOF; position stable. |
| Legitimate 8-second video plus 24-second audio | 240 frames, primary duration 8, range end 7.966667, no automatic reopen. |
| Healthy sparse video, no recovery enabled | Two valid frames, PTS 0 and 10; primary duration 20. Full video body delivered, no truncations; natural global EOF after audio. |
| **Same healthy sparse video, recovery enabled** | **False positive** at wall time 20.709 s, position 10.500710, range end 10, duration 20. Reopens again at 25.741 s. All video HTTP responses are complete. `--expect-no-reopen` fails. |

The sparse media is generated normally by FFmpeg with `--video-rate 1/10`.
Although requested generation length is 24 seconds, frame-rate rounding produces
two samples and a 20-second primary track; the experiment uses the actual mpv
duration. The MP4 `mdhd` timescale is 16384 and duration is 327680 ticks. Its `stts`
has **two samples each lasting 163840 ticks (10 seconds)**. Thus the last frame at
PTS 10 legitimately lasts until PTS 20. The 10-second duration/cache-end gap is
not corrupt metadata, a missing packet, or a network failure. Native cache
`fw-bytes` still becomes zero while that frame is valid. The baseline run decodes
both samples and completes naturally without reloading.

## Specific missing evidence

`MpvCacheStateEventArgs` exposes ranges, BOF/EOF flags, forward bytes and file-cache
bytes. The range end is the last packet timestamp, **not its presentation end**.
Neither it nor the current player duration/position/state notifications supply
that packet's duration or a track-specific terminal transport failure reason.
The complete sparse stream and the truncated stream can both satisfy the proposed
trigger using these observables. Waiting longer or requiring repeated stable
cache events does not establish that the last frame's valid presentation ended.

`estimated-vf-fps` was additionally recorded for diagnosis (0.1 in the sparse
case). It is not in the existing cache event contract. A frame-rate exclusion
would avoid this particular constant-rate counterexample, but this experiment
does not validate it for variable frame durations or prove it gives the final
packet duration. No such heuristic was promoted to production. This is evidence
against this candidate and the sufficiency of these fields, **not a proof that
every possible native detector is impossible**.

The next implementation needs independent corroboration of incomplete main-video
input (a reliably attributed terminal transport failure), or reliable last-frame
presentation-end information, or an explicitly narrower media eligibility
contract with validated evidence. Generic EOF/log text, global position, and
cache depletion alone do not provide that corroboration. A dependency upgrade or
new public interface was not authorized and was not attempted.

## Reproduction and validation

Use the binary paths from #183 and fresh scratch directories. With `$scratch`
outside the repository:

```powershell
$dll = 'C:/Users/hp/.t3/research/spec-182/libmpv-2.dll'
$ffmpeg = 'C:/Users/hp/.t3/research/spec-182/ffmpeg.exe'
$scratch = 'C:/Users/hp/.t3/research/spec-182'
python scripts/diagnostics/playback-freeze/repro.py --mpv $dll --ffmpeg $ffmpeg --out "$scratch/auto-exhaust" --auto-reopen --seconds 27
python scripts/diagnostics/playback-freeze/analyze.py "$scratch/auto-exhaust/result.json" --expect progressing
python scripts/diagnostics/playback-freeze/repro.py --mpv $dll --ffmpeg $ffmpeg --out "$scratch/auto-sparse-control" --scenario control --video-rate 1/10 --auto-reopen --seconds 27
python scripts/diagnostics/playback-freeze/analyze.py "$scratch/auto-sparse-control/result.json" --expect-no-reopen
# Expected exit 1: the candidate incorrectly reopens healthy media.
```

Other generator arguments: `auto-unavailable`: `--auto-reopen --release-at 999
--seconds 27`; `auto-control`: `--auto-reopen --scenario control --seconds 27`;
`auto-normal-pause`: `--auto-reopen --scenario control --action pause --action-at 5
--seconds 10`; `auto-pause`: `--auto-reopen --action pause --action-at 5 --seconds
15`; `auto-short`: `--auto-reopen --scenario control --video-seconds 8 --seconds
27`; `sparse-control`: `--scenario control --video-rate 1/10 --seconds 36`.

All eight real-player runners exited 0. Assertions executed:

- Original `exhaust --expect progressing`: exit 1 (expected existing failure).
- `auto-exhaust --expect progressing`: exit 0.
- `auto-unavailable --expect-exhausted`: exit 0.
- `auto-control --expect normal-end --expect-no-reopen`: exit 0.
- `auto-normal-pause --expect paused --expect-no-reopen`: exit 0.
- `auto-pause --expect-no-reopen`: exit 0; recorded tail position span 0.
- `auto-short --expect short-video --expect-no-reopen`: exit 0.
- `sparse-control --expect-no-reopen`: exit 0.
- `auto-sparse-control --expect-no-reopen`: exit 1 (**rejected detector**).

No application production code or .NET tests changed, so no .NET build, WinUI
analyzer run, or native application/UI acceptance was performed for this gated
diagnostic-only change. No claim is made for seek/switch/close races, quality,
speed/volume/pause preservation, danmaku synchronization, long-duration soak, or
user-visible bounded-failure handling. #184 remains unimplemented. Full synthetic
results and media stay in private scratch storage; no signed URLs, original
attachment, credentials, or bulk native logs are committed.
