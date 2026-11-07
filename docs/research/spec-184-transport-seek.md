# #184: existing transport options and ordinary app seek

**Evidence-only result; #182/#184 remain incomplete.** Existing configuration can
recover particular transient failures. Ordinary app seek can also recover, without
reopening the media. The earlier single failed seek did not justify a general
claim that seeking cannot reconnect.

No application default, installed setting, dependency, or native code changed.
The tested mitigations do not establish which transport policy fits the historical
incident, and they all retain the terminal-cache failure after their finite retry
budget. In particular, switching the entire app to FFmpeg networking is a broader
behavior change than a demonstrated correction of the historical HTTP/2 fault.
These results support a bounded configuration trial, not claiming #184 fixed.
No heuristic detector or automatic seek was introduced.

Base: `a39a02470dd93fa4d39e2e7c2e11653b66c32de2`,
`integration/spec-182-playback-freeze`. Full issue bodies/comments were read with
`gh`; the latest comment authorizes configuration experiments before native work.

## Why retry can fail while a later manual seek works

The pinned wrapper's `SetCurrentPositionAsync(double)` calls
`mpv_set_property(handle, "time-pos", MPV_FORMAT_DOUBLE, &position)` from a worker
task. It does **not** send the earlier harness's `seek absolute+exact` command.
The new harness invokes that native property call with a real double. mpv maps it
to an absolute seek with default precision. The application uses this path while
playing; its separate `PlaybackState.End` branch calls `ReplayAsync` and is not
what these seek experiments claim to reproduce.

Sources: [pinned wrapper](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.Player.cs),
[native property](https://github.com/mpv-player/mpv/blob/2137d21b2/player/command.c),
local `PlayerViewModel.Playback.cs`.

The fault delivers video through byte 917115, then truncates at offset 917116;
audio remains available. The demux cache becomes EOF with its last usable range
ending at PTS 7.833333. Five curl retries finish long before the endpoint becomes
healthy at 12 seconds. No timer later revives this EOF cache.

| Actual app seek at wall ~14 s | Cache immediately before seek | New video request | Decoded outcome |
| --- | --- | --- | --- |
| Target 4, default cache | 0–7.833333, BOF/EOF | None | Cached video replays to 7.8, then freezes again |
| Target 14, default cache | 0–7.833333, BOF/EOF | None | No new frames beyond truncation |
| Target 7, consumed cache | 7–7.833333, EOF, not BOF | None | Cached tail only |
| Target 14, consumed cache | 7–7.833333, EOF, not BOF | None | No new frames beyond truncation |
| Target 2, consumed cache | 7–7.833333, EOF, not BOF | `bytes=219333-` | Frames progress through 23.966667 and natural end |

To make consumption reproducible with this small synthetic video, the last three
cases use `demuxer-max-back-bytes=256KiB` and `demuxer-donate-buffer=no` from load
time. This is **not** a proposed application setting. Merely setting back cache
to 256 KiB was insufficient: unused forward memory was donated to it, retaining
the entire old range. The app's setting value 0 means it omits the override;
the DLL's effective default back cache is 50 MiB, not zero. These tests do not
prove the user's historical cache size or eviction point.

mpv's `find_cache_seek_range` accepts a range when
`(target >= start || is_bof) && (target <= end || is_eof)`. Thus seeking **past an
EOF-marked end** can still use that range. Seeking **before an evicted start**
cannot. [Pinned demux implementation](https://github.com/mpv-player/mpv/blob/2137d21b2/demux/demux.c).

Additional comparisons:

- After consumption, absolute exact, absolute keyframe and relative backward
  seeks all recovered through decoded late frames. With the full EOF cache
  retained, exact/keyframe target 14 and relative +2 did not reconnect.
- The same backward app seek at wall 10, while the source was still down, made
  requests but exhausted again. The source becoming healthy at 12 did not itself
  recover video. Repeating the seek at wall 20 fetched bytes and reached the end.
- Before initial exhaustion, an app seek to 14 with 100 retries reached a new
  offset, but still failed while the endpoint was unavailable. It caused a
  second request sequence (202 video requests total). A retry count is not a
  whole-session request bound when user operations start additional transfers.
- On HTTP/1.1 the successful consumed-cache seek used a **new TCP connection**
  (fixture sends `Connection: close`). On actual TLS HTTP/2 it used **stream 15
  on the same connection 1**, after failed stream 13, with the same successful
  `bytes=219333-` request. Recovery requires new input, not necessarily new TCP.

This supplies a controlled explanation consistent with the user report: retries
happen earlier and may stop while a source is down; a later seek can request a
different offset after the source recovers, depending on cache coverage. It does
not identify the user's exact seek destination, timing, cache state or protocol.
The historical attachment contains no post-failure seek trace.

## Transport experiments

All use the existing package copies: mpv `v0.41.0-1386-g2137d21b2`, embedded FFmpeg
`N-128281-ge19fadf1d`. Binary and synthetic media hashes, effective native option
values, exact request offsets/times and decoder summaries are in
[transport-summary.json](spec-182/transport-summary.json). Options were accepted
by that DLL; observed changes in retry counts, Range headers, ALPN and backoff
validate behavior rather than only successful option setters.

| Failure class / configuration | Observed result |
| --- | --- |
| 12-second outage, default curl | 6 video requests; last at 0.078 s; 235 frames ending PTS 7.8 |
| Same outage, retries 20 | 21 requests; last at 0.303 s; still 235 frames |
| Same outage, retries 100 | 101 requests; last at 1.546 s; still 235 frames |
| Ten failed requests then healthy, default vs retries 20 | Default exhausts; 20 completes all 720 frames after request 11 |
| Same outage, 128 KiB Range cap | 12 requests; still truncated and frozen |
| Oversized-request failure, default vs 128 KiB cap | Default gets initial prefix then exhausts; bounded requests complete all 720 frames in 21 requests |
| Each response recycled after 256 KiB | Defaults already recover in 11 requests; 128 KiB cap completes 21 unbroken requests |
| TLS h2 stream-reset fixture, default vs retries 20 | Actual ALPN h2; default exhausts after 6 requests; 20 completes after 11 |
| Same h2-only failure policy, forced HTTP/1.1 | Actual HTTP/1.1; completes, because that fixture faults only h2 streams |
| Time outage on TLS h2 vs forced HTTP/1.1 | Both exhaust and freeze; HTTP/1.1 is not a time-outage repair |
| FFmpeg defaults, 3-second outage | Recovers at ~4.1 s; all 720 frames |
| FFmpeg defaults, 12-second outage | Last request ~11.1 s; freezes |
| FFmpeg bounded longer reconnect, 12-second outage | Successful request ~26.1 s; decoded video resumes, >30 distinct frames at PTS >=18 observed |

Request times above are relative to the first video request. Player samples and
action times use the player-load epoch; do not subtract across those epochs for
millisecond latency claims. Local response truncation deliberately waits 5 ms
after writing, so localhost retry spacing is fixture-specific, not a CDN latency
prediction. Counts include the initial request. The complete ledger preserves
each Range and attempt interval.

The Range-limit fixture is explicitly a request-size-dependent policy: requests
larger than 256 KiB are cut at the fixed prefix, while smaller requests are served.
The recycle fixture instead allows fresh byte progress on every response.
These separate tests prevent mistaking total endpoint blackout for a connection
or stream policy, and show that chunking is not necessary for every disconnection.
Neither policy is asserted to be the historical CDN policy. HTTP/2 is verified
by server ALPN and stream IDs, not by setting a client option against an HTTP/1 server.

The tested longer FFmpeg configuration is:

```ini
curl-enabled=no
stream-lavf-o=reconnect=1,reconnect_on_network_error=1,reconnect_delay_max=16,reconnect_max_retries=6,reconnect_delay_total_max=30
```

Its observed intervals were approximately 0, 1, 3, 7 and 15 seconds (cumulative
0, 1, 4, 11, 26). `reconnect_delay_max` is a give-up threshold, **not a cap that
turns exponential backoff into repeated fixed waits**. The ~19-second largest
gap in decoded output rules out a seamlessness claim. With permanent failure,
requests stopped around 26 seconds and the same terminal EOF/frozen-video state
remained; the config supplies neither post-budget recovery nor failure UI.
Changing `reconnect_delay_total_max` to 2 reduced the sequence to four requests,
but its last request was around 4.1 seconds: that option is not a strict end-to-end
wall-clock deadline. Connection/IO timeout and transfer progress also matter.
`reconnect_at_eof` was deliberately not enabled.

The [mpv network manual](https://mpv.io/manual/master/#network-backend-libcurl)
documents retry counts, HTTP version selection and bounded requests.
[Pinned curl source](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_curl.c)
restarts immediately and resets retries on byte progress;
[pinned lavf source](https://github.com/mpv-player/mpv/blob/2137d21b2/stream/stream_lavf.c)
sets reconnect=1 / delay_max=7.
[Official FFmpeg protocol documentation](https://github.com/FFmpeg/FFmpeg/blob/master/doc/protocols.texi)
describes reconnect bounds and live-stream EOF behavior. The actual DLL results,
not current documentation alone, establish support here.

## Controls, reproduction and next step

For retries 20, bounded Range, HTTP/1.1 and the longer FFmpeg configuration:
normal 24-second media decoded exactly 720 frames and reached EOF without extra
EOF retries; pause held position and resumed; stop during the outage became idle
with no subsequent requests; permanent no-progress failures stopped requesting
within the measured bounds. Native handle destruction was under two seconds in
every retained run. FFmpeg also held explicit pause through successful reconnect
at ~26 seconds and resumed only on the requested unpause at 30 seconds.
These are native-player controls, not WinUI switch/close race tests.

The highest tested boundary is real demux/decode and timed playback with null
audio/video sinks and showinfo frame checksums. No displayed-surface, audible
continuity, live CDN, long-duration soak, URL expiry, cookies, proxy, bandwidth
cost, or production quality/speed/danmaku preservation claim is made.

Reproduce with Python, Node built-ins and the existing binaries; no package
installation or native build is required. Generate a disposable loopback TLS
certificate/key with an existing OpenSSL executable. Do not install that
certificate into the OS trust store. TLS verification is disabled only in the
loopback diagnostic player. Keep artifacts outside the checkout:

```powershell
python scripts/diagnostics/playback-freeze/transport-matrix.py `
  --mpv <existing-libmpv-2.dll> --ffmpeg <existing-ffmpeg.exe> `
  --media <scratch-media-directory> --out <scratch-results-directory> `
  --cert <scratch-cert.pem> --key <scratch-key.pem> --jobs 3
python scripts/diagnostics/playback-freeze/assess-transport.py <scratch-results-directory>
```

`--case` selects individual matrix entries. Use a fresh media directory or the
unchanged generated 24-second/30-fps control media. The server binds only loopback;
fixtures terminate with their parent. The older `repro.py` remains unchanged.
Retained matrix: 51 runs, all runner exits 0; full assessment exits 0.
The identical `--expect-progress default` oracle exits 1 (expected red), while
`--expect-progress lavf-long` and `--expect-progress app-consumed` exit 0.
Python syntax, Node syntax and `git diff --check` were checked. .NET tests,
application build and WinUI analyzer were not run: there is no application change.

**Recommended next step:** use the bounded FFmpeg configuration above as a
controlled, explicitly scoped configuration trial if the immediate goal is
bridging transient outages, recording decoded continuity and actual request
timing. If the target is a request-size or h2-specific failure, first obtain a
trace identifying that policy before choosing a global Range/protocol default.
Record effective cache options and seek target alongside that trace. Do not
conflate a successful late seek with a retry still running, and do not require
new native code merely to explain that seek. No claim is made that all existing
configuration paths are exhausted; untested combinations remain. Parent owns
the decision to accept a narrower mitigation contract and draft #185.
