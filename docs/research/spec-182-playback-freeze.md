# Investigation #183: split-track playback freeze

Follow-up: [existing transport options and actual app seek](spec-184-transport-seek.md)
qualifies the seek conclusion below. A seek before an evicted cache start can
fetch new bytes and recover; the original retained-cache target-14 experiment
does not generalize to all manual seeks. The later report also tests real TLS h2.

Investigation date: 2026-11-07. Parent: [#182](https://github.com/MangMax/Bili.Copilot/issues/182). Base and integration tip inspected: `0d9ee8724090917a5ba8fdb52c12a539221528f3`. No production changes, dependency changes, issue mutations, or PR. Investigation completed within one working day.

## Finding and decision

**A prematurely terminated primary video stream becomes a terminal demuxer/cache EOF while the separate audio stream keeps playing.** This is reproduced with the installed package's real libmpv, independently of the application's UI, P2P selection, and hardware decoding. Recoverable transfer failure followed by retry exhaustion is the demonstrated trigger. Once the source is healthy, the existing playback does not request more video. A normal absolute seek can reuse the truncated EOF cache even when its target is beyond the last cached frame.

The historical attachment strongly supports the same mechanism: identical malformed NAL sizes first appear at demux time and then at decode time 426.923 seconds later. This does **not** identify who caused the HTTP/2 failure (server, intermediary, network, or client), establish the exact historical cache occupancy, or prove that the installed DLL was the one loaded during the incident.

**Smallest verified recovery operation:** reopen the same primary source and external audio, starting at the current playback position. This discards the terminal demuxer/cache state and makes fresh Range requests; two runs recovered decoded video. No URL refresh was required in the controlled case. This is a verified recovery primitive, **not an implemented automatic repair or a cure for the upstream transport failure**.

**Proposed smallest application repair for #184:** use the existing cache-state event to recognize a clearly premature primary-video EOF relative to the primary duration, then perform a bounded, session-scoped reopen at the current position. Keep source/quality/speed and existing session invalidation. Do not add a periodic watchdog or assume ordinary seek forces network access. The recognition rule, retry timing, and failure presentation still need a narrowly specified acceptance contract before implementation: one reopen while the endpoint remains unavailable also fails. Do not treat this spike as approval for speculative production code or close/unblock #184 automatically. Parent should inspect these results and decide whether that bounded recovery satisfies the spec; otherwise keep #184 blocked and pursue native error propagation/cache invalidation.

The earliest causal repair boundary is mpv's exhausted transport → demux EOF transition: preserve the distinction between input failure and valid EOF, and prevent a failed range from becoming a reusable terminal cache. A native patch here is outside this repository's ≤M application-only scope and has **not** been built or tested. Merely increasing retry counts, disabling curl, or enlarging a domain blacklist is not a proven general repair.

## Original evidence, independently recovered

The original thread's first attachment-tool record supplied its local path. The file still exists and was read directly; the previous assistant's narrative was not used as evidence. Private provenance and scratch paths are in `C:/Users/hp/.t3/research/spec-182/NOTES.md`.

- Original label: `log-2026-11-07.txt`, 1718 logical lines.
- SHA-256: `6971b66139e3f9037ac852eecf985afba0baf652232c578d813fff58423862aa`.
- Published evidence: [numbered, redacted excerpt](spec-182/original-excerpt.txt). Full URLs and bare signed media-filename queries are removed. No original raw log is committed.
- Re-extraction: [extract-original.py](../../scripts/diagnostics/playback-freeze/extract-original.py) checks that exact hash before using its fixed line allowlist.

All original times below are UTC+08:00:

| Time | Directly observed evidence |
| --- | --- |
| 17:44:31.959 | App identifies itself as 2.2611.2.0 (L1032). |
| 17:44:40–42 | Main H.264 video, 3240×2160, 30 fps, nominal 10786 kbps; separately appended external AAC audio, stereo/48 kHz/144 kbps (L1099, L1165–1167). Start 0, speed 1, volume 100. |
| 17:44:48 / 52 | HTTP/2 errors followed by first retries at different byte offsets (L1610–1612). |
| 17:44:54.455 | Generic `EOF reached` (L1614). Component/track prefix is absent: this cannot be called a transient **video** EOF. An external audio file finishing read-ahead is another possibility. |
| 17:49:57–17:54:44 | Repeated retries. Final five retries remain at byte 870318080; terminal transfer failure at 17:54:44.388 (L1642–1652). |
| 17:54:48.069–070 | Corrupt packet, invalid NAL size 6926 > 6292, missing picture, partial file at offset `0x33e0027a`, generic EOF (L1654–1662). That offset is 634 bytes beyond the final retry offset, consistent with the NAL shortage, not proof of the complete HTTP exchange. |
| 18:01:54.993–55.163 | Same NAL size error during H.264 decoding, hardware-decode warning, filter input/output EOF (L1664–1672). |
| 18:02:58.446 | Pause set true (L1717). No seek/reload after the failure appears in this attachment. |

The user reported continued audio and danmaku; the original log does not independently measure their frame/clock progression. The synthetic experiment below does measure decoded video and audio timestamps. The 427.093-second demux-to-filter-EOF interval is consistent with read-ahead, but **not a measurement of 300 MiB or 360 seconds of buffered content**. The log does not record those effective settings or historical duration. The generic logger drops mpv's component prefix, limiting track attribution. The NAL match is stronger evidence than the generic EOF text.

## Environment and configuration

Installed `Mang.BiliCopilot` 2.2611.2.0 contains these binaries. Direct DLL loading from WindowsApps returned access denied; read/copy succeeded, and the copied DLL hash matches the installed file. Nothing was replaced in the app installation.

| Item | Observed value |
| --- | --- |
| libmpv property | `mpv v0.41.0-1386-g2137d21b2` |
| libmpv SHA-256 | `4752efe9982ed763f0a85ade83b55cf34442cad0c19341ad9b90769146cd2f3c` |
| Embedded FFmpeg property and standalone generator | `N-128281-ge19fadf1d` |
| Standalone FFmpeg SHA-256 | `eebdf4169ea3c8e936f7840b882cf31f41d0a3913b8dea75b95bc4925877a46f` |
| FFmpeg libraries | avutil 61.20.100; avcodec 63.25.102; avformat 63.18.100; avfilter 12.12.100 |
| Current repo wrapper packages | Richasy.MpvKernel / Core / Player / WinUI `0.0.2-preview2`; NuGet source commit `c1ef0f403097129c3b6db4c466cc4147054471d2` |
| Historical rendering evidence | D3D11; AMD Radeon(TM) Graphics, driver 32.0.22050.4002; libplacebo v7.387.0 (`v7.387.0-6-gc4de47c-dirty`) |
| Experiment host | Windows x64, Python 3.13.17; ctypes invokes the real libmpv C API |

Historical custom DLL path, curl library version, complete effective mpv options, custom config, P2P toggle, exact track lengths, HTTP headers/statuses, and cache occupancy are **unknown**. The installed package version matches the app's logged version; that does not prove no custom DLL override was used.

Current `PlayerViewModel.Methods.cs:71–220` permits a custom DLL and custom configuration. Its noncustom defaults request 300 MiB and 360 seconds of read-ahead; these are defaults, not historical measurements. The checked-in `basic-mpv.conf` is only `vo=gpu-next`, `hwdec=auto`, `ao=wasapi`. Repo CI pins the 20261102 build at the same mpv revision.

Experiment effective properties: curl enabled `yes`, max retries `5`, HTTP version `auto`, seekable cache `auto`, max demux bytes `314572800`, read-ahead `360`, empty `stream-lavf-o`. All runs set config/scripts off, cache on, idle/keep-open on, software decoding, null video/audio outputs, and the `showinfo` video filter. Null sinks avoid display/audio-device dependencies; they still run real demuxing, H.264/AAC decoding and timed playback. **This establishes decoder-frame progression/stoppage, not a capture of the WinUI displayed surface or audible audio.** `video-pts` is unavailable with this VO; it is not used as the oracle.

## Reproduction and fail/pass oracle

[repro.py](../../scripts/diagnostics/playback-freeze/repro.py) generates 24-second independent H.264 and AAC MP4 files with FFmpeg. Video is an animated test pattern; `showinfo` supplies PTS and decoded frame checksums. The localhost HTTP/1.1 server supports ranges, advertises the full resource length, sends only bytes `[0, 917116)` of video, and closes. Subsequent video range replies send zero body bytes until recovery is enabled. Audio always remains complete. The cutoff lies inside an encoded packet.

Reference media: video 2,751,298 bytes, SHA-256 `ea67c5aa60eb6d1c73f39e24a22f2eee40762940450912457502814f084fc1e1`; audio 388,997 bytes, SHA-256 `aba1755d4718882d72d205b58179c8b809d22f76530a7db6e3a042cd7723fad7`. These are generated files, not downloaded Bilibili content. A different FFmpeg build may generate different bytes; every run records its media hashes, cutoff, options, requests, samples, and events. Use fresh output directories when changing media generation options.

PowerShell, from repo root (set these two paths to your inspected/copied binaries):

```powershell
$dll = 'C:/Users/hp/.t3/research/spec-182/libmpv-2.dll'
$ffmpeg = 'C:/Users/hp/.t3/research/spec-182/ffmpeg.exe'
$scratch = 'C:/Users/hp/.t3/research/spec-182'
python scripts/diagnostics/playback-freeze/repro.py --mpv $dll --ffmpeg $ffmpeg --out "$scratch/exhaust"
python scripts/diagnostics/playback-freeze/analyze.py "$scratch/exhaust/result.json" --expect progressing
# Expected exit 1: no 30 distinct late decoded video frames.
python scripts/diagnostics/playback-freeze/repro.py --mpv $dll --ffmpeg $ffmpeg --out "$scratch/reload" --action reload
python scripts/diagnostics/playback-freeze/analyze.py "$scratch/reload/result.json" --expect progressing
# Expected exit 0: >=30 distinct frames with PTS >=18, audio PTS >=20.
```

The default fault stops at wall time 12 seconds. Default manual diagnostic action is at 14 seconds, targeting media time 14. Timings below are relative to process-run start and include startup. The action deliberately occurs after the endpoint is healthy; it does not model an automatic failure detector. Full machine-readable observations: [experiment-summary.json](spec-182/experiment-summary.json). Full synthetic logs/results remain outside the repo.

| Case / additional arguments | Result | Discriminating observation |
| --- | --- | --- |
| `control`: `--scenario control --seconds 26` | PASS, natural completion | 720 decoded frames, last PTS 23.966667, global EOF yes. Cache EOF was already true during healthy read-ahead. |
| `transient`: `--scenario transient` | PASS | First resumed request at byte 917116 succeeds; decoded video passes PTS 21.8. |
| `exhaust`: defaults | FAIL progression | Six video requests finish by 0.239 s; 235 frames, last PTS 7.8; audio reaches 21.712. No request after endpoint recovery. |
| `curl-short`: `--release-at 3` | FAIL progression | Same terminal video cutoff despite endpoint recovery before cached frames run out. |
| `seek`: `--action seek` | FAIL progression | Seek to 14 reuses EOF-cached range ending at 7.833333; 25 old frames decoded again, no new request, no late frames. |
| `seek-no-cache`: `--action seek-no-cache` | FAIL progression | Changing seekable-cache immediately before seek still used the cached range in this run. Do not infer a synchronous cache purge. |
| `seek-initial-no-cache`: `--action seek --option demuxer-seekable-cache=no` | PASS recovery | Same seek now requests video from byte 1596519 and audio from 228465; late video advances. Not an automatic fix; changes normal cache-seek behavior. |
| `reload`, `reload-repeat`: `--action reload` | PASS recovery, twice | Fresh video open and Range 1596519; decoded video passes 21.9, distinct late checksums. Clears failed state without refreshing URLs. |
| `lavf`: `--release-at 3 --option curl-enabled=no` | PASS bounded outage | Only transport changes vs `curl-short`; retries at ~0.23, 1.24, 4.25 s, last request succeeds. |
| `lavf-long`: `--option curl-enabled=no --seconds 26` | FAIL progression | Retries after 0/1/3/7-second delays exhaust before 12-second endpoint recovery; video still ends at 7.8 while audio later passes 20. |
| `pause`: `--scenario control --action pause --action-at 5 --seconds 10` | PASS control | Position stable after pause; complete cache end stays within one frame of duration. |
| `short-video`: `--scenario control --video-seconds 8` | PASS control | Valid 8-second video + 24-second audio: 240 frames; duration property is 8, cache end 7.966667, audio passes 21. No transport error. |

The progression assertion was run against `exhaust` and failed (exit 1), then against both `reload` results and passed (exit 0). Final validation also asserted all five intended stalls, five intended progressing cases, normal completion, pause stability, and legitimate short video. The first 22-second `lavf-long` observation had audio only at 17.987 because it buffered; it did not meet the oracle's 20-second observation threshold. It was rerun for 26 seconds and passed the stall oracle without weakening the assertion.

## Responsibility boundaries and repair constraints

1. **Transport:** matched revision `stream_curl.c` routes both partial-file and HTTP/2 errors through the same recoverability/retry path. Received bytes reset the retry counter; failed requests without progress consume the five attempts. The controlled HTTP/1.1 cut therefore exercises the relevant exhaustion branch, but does not recreate the historical HTTP/2 framing defect itself. Adding an HTTP/2 emulator would not determine which historical endpoint/intermediary failed. [Pinned curl source](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_curl.c).
2. **EOF/cache:** after terminal failure, the curl buffer drains and returns zero. The repro records truncated main-video EOF, retained 24-second duration, and no end-file event during the stall. mpv cache seek accepts a range marked EOF for targets beyond its recorded end; the seek log and HTTP request ledger demonstrate this behavior. [Pinned demux source](https://github.com/mpv-player/mpv/blob/2137d21b2/demux/demux.c).
3. **Alternative transport:** mpv's FFmpeg stream path sets reconnect and a maximum reconnect delay. It survived the short outage and failed the longer one; changing backend is a mitigation, not evidence that HTTP/2 alone causes the freeze. [Pinned lavf stream source](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_lavf.c).
4. **Wrapper/application:** the pinned Richasy event handler logs level/text without component prefix and does not map these `transfer failed`, `partial file`, or NAL messages into its recognized errors. `CacheStateChanged` exposes seekable ranges and `EofCached`; the app currently forwards it. App error replay requires `Duration < 1`, which does not cover this case. Global player position drives danmaku, explaining why continued audio-clock movement can keep danmaku moving. [Pinned wrapper handler](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.Events.cs); local `PlayerViewModel.Methods.cs:361–411`, `PlayerViewModel.Playback.cs:20–23`.
5. **Resolver:** `VideoMediaSourceResolver.GetSourceAsync` puts video in the main source and audio in `AudioTracks`. `LoadMediaInfoAsync` returns when `_playbackInfo` exists or `_initTask` completed. Repeating `GetSourceAsync` does **not** prove a fresh playurl. Refreshing signed URLs is not needed in this repro and is not selected as the repair. Any future expiry case must verify the service call and changed URL outside that cache. P2P hostname matching is a separate hypothesis: localhost reproduces the problem, and the historical toggle is unknown. No domain-list or hardware workaround is justified by these results.

For an event-driven early-EOF candidate, **do not use `EofCached`, `fw-bytes=0`, or stopped video alone**. Each also occurs normally. Here the fault has cache end 7.833333 versus primary duration 24; controls end within one frame of primary duration, including the valid short-video case. That supports testing a duration-aware predicate for finite, known-duration main-video sources, with a tolerance for rounding and missing timestamps. It does not yet validate arbitrary media, inaccurate duration metadata, sparse frames, changing tracks, or event ordering in the wrapper.

A reload must preserve the existing selected source and playback rate, capture current position, avoid using stale callbacks after switch/close, and be bounded if the endpoint remains unavailable. The diagnostic `loadfile ... start=14` operation proves network/cache reset, not those application invariants. Do not simply call the existing replay-to-zero branch. Do not classify generic EOF logs as faults or reload on natural completion/pause.

## Remaining uncertainty / handoff gate

- No server-side capture, HTTP/2 trace, historical complete config, or proof of exact historically loaded DLL. No attribution to CDN, P2P, GPU, or libcurl defect beyond the evidence above.
- No app-level automatic recovery, full WinUI surface test, quality/speed preservation test, switch/close race test, or long-duration soak was implemented. Those belong to #184 after its precise trigger/retry contract is settled.
- Original native source bytes and corrupt packet were not downloaded; the reproduction demonstrates the failure class with synthetic faststart MP4 tracks, not the identical Bilibili asset/container layout.
- Full recovery while a source remains permanently unavailable is impossible; bounded failure must become visible rather than an infinite reopen loop or silent static video.
- If #184 must eliminate the transport-to-EOF defect rather than implement bounded recovery, keep it blocked pending a native fix and repeat this exact fail/pass experiment against that binary. This spike supplies a reproducible failure and rejects several tempting but insufficient fixes; it does not claim the spec is implemented.
