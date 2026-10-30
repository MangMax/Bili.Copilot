# Restored-window CPU profile

2026-10-30. Research-only follow-up to `0ac4a89ff068a4f47e16538bc589912acd2d21db`. Parent baseline remains `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`. No production change, parent merge, push, PR, issue mutation, LCS rerun or full build/test rerun.

## Decision

**Keep the aggregate stable upgrade on HOLD for performance acceptance. Stop this bounded investigation here.** EventPipe works, but does not capture native on-CPU stacks for the threads responsible for much of the measured CPU. These traces do not justify blaming or rolling back Win2D/AppSDK, declaring no regression, or declaring a performance win. Existing targeted compatibility passes remain valid; this round does not grant aggregate performance acceptance. A compatibility-only adoption would require explicitly accepting the unresolved native CPU risk, not relabeling these results as a performance pass.

There is concrete observer work in the frozen measurement: the UI thread repeatedly enters `System.Diagnostics.NtProcessInfoHelper.GetProcessInfos` from `IdleBaseline.Measure` while reading process properties. Do not attribute that work to product layout/animation. It does **not** explain all process CPU or the baseline/candidate difference. The actionable measurement correction is to move monitoring off the UI thread and use process CPU endpoints (or cheap process-specific APIs), avoiding `Process.Threads`/whole-process information refresh every second. Keep a separate observer-disabled validation when such a measurement revision is undertaken; do not change product features to improve this score. That revision was not implemented or benchmarked in this task.

No exact application defect or reproducible product fix was established. Keep **#49 FAIL, AOT blocked, authenticated home unverified, RC excluded**. The previous nonempty LCS results stand without rerunning them.

## Controlled experiment

- Original baseline: retained `bili-dep-d48-base-player/frozen-output` in the local temp directory, independently checked against the original `t3-d48c749d/artifacts/dependency-upgrade/base2complete-closure.json`. Source label `bae42e81d244dcc4ec33bd305c92f8858ff7c416` has the original baseline production source; kernel `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7` (see prior review for ancestry).
- Final candidate: `t3-340954c4/artifacts/dependency-performance-diagnosis/player-a/frozen-output`, checked against its frozen all-file manifest. Fixture source label `cce01fd620e54bbd70c8d958f6fa10ed5e5a689f`; production source tree `a5e099655ce21b460392e5a3d0b4f70feb1f8aec`, shared with stable `60abad7834f7037501fcb99696494d3dcebb8977`; kernel `0ae5e4e000a41a07f6696746706df11c06e8835b`.
- Each new task-owned input/layout contains no appxrecipe. Only package identity and the source-provenance text are supplied locally. No old project/deployment recipe is evaluated. The original frozen directories are read-only inputs. Every accepted layout passes a two-way **357-DLL** gate (no missing, extra or mismatched DLL), plus exact original runtimeconfig hash. All loaded modules, versions and SHA256 values are recorded per process.
- App runtime remains self-contained .NET **9.0.22**, coreclr `03A85B6BA7087B41AC63F5C23C534074A4BE81EAA23AC5AAE9ED252C2C28DD1C`; libmpv `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A`. The native package/runtime differences are retained, not mixed with RC or arbitrary new package groups.
- `IdleBaseline.cs` is byte-identical in both frozen source fixtures: SHA256 `CA5F6C02533BDEC3F4B01BF750D819F1F4253DE5DED9A5528D789597805902AA`. Player15 precedes Main4; real SettingsPage, notifications disabled, controller hidden, scale 1.5, 5-second warmup, 2-second phase settling, 61 rows/phase, 20 same-HWND cycles, explicit exit. No concurrent build/benchmark. These are synthetic offline fixtures, not authenticated production sessions.

## Collector and limits

The local collector uses cached DiagnosticsClient **0.2.510501** and TraceEvent **3.1.23**. Only the profile host was built. The first host launch discovered no shared net9 runtime; the host was configured to roll forward to installed stable **10.0.14** (actual loaded coreclr hash recorded). This does not alter either self-contained target runtime or introduce RC. The same collector binary/runtimeconfig is used for all accepted runs.

Both target runtimeconfigs were inspected before collection. Neither disables EventSource; neither was edited. EventPipe successfully returns CLR events and symbolized managed frames. This is not an inference that no events means no CPU. No application custom EventSource, network/HTTP, auth provider, heap dump, forced GC, global tool install or system profiling-policy change was used.

Profiles enable only `Microsoft-DotNETCore-SampleProfiler` and CLR GC/JIT/Loader, with 64 MiB buffer and rundown. The helper waits for the frozen fixture's 122-row hidden-phase file, then records thread/process CPU before and after a 61-second capture. References run the same helper without an EventPipe session. No UIA polling occurs inside the measured phases; file polling is out of process. The one thread-start-address probe occurred during the candidate reference's **visible** phase, outside the restored window, and returned zero for all 264 threads.

**Window distinction matters:** helper endpoints include approximately two seconds of restore settling and omit roughly the last second of the 60-second restored plateau. Per-thread CPU totals therefore describe that explicitly timestamped helper window, not exactly the plateau or its halves. The reducer restricts profile stacks to complete restored counter intervals contained in the trace and records the selected row indices/UTC endpoints. Whole-phase and half-phase rates always use the original fixture endpoints. Do not subtract different windows or convert stack counts into exact CPU milliseconds.

EventPipe samples managed thread state, including External/native/wait states; it does not provide native on-CPU stacks. The external mpv, WaitHandle, semaphore and XAML dispatcher stacks cannot be ranked as CPU hotspots by count. [Microsoft's EventPipe scope documentation](https://learn.microsoft.com/dotnet/core/diagnostics/eventpipe) explicitly distinguishes this from ETW native CPU sampling. The .NET 9 [sample-profiler implementation](https://github.com/dotnet/runtime/blob/v9.0.11/src/native/eventpipe/ep-sample-profiler.c) uses a nominal 1 ms sleep; this is a version-family reference, not proof of exact effective cadence on this runtime. Actual observed per-thread cadence is reported with the trace data. The profiler itself suspends/wakes managed threads and can incur target CPU and timer-resolution effects; it is an independent observation condition, not a transparent performance score.

WPR is installed and `wpr -status` reports no recording. Current token is non-elevated and exposes neither profiling privilege in `whoami /priv`'s filtered output. The previous `0xc5585011` policy-enable failure is documented in [the original UI investigation](ui-46-47.md). No redundant `wpr -start`, elevation, policy change or system-wide capture was attempted. `ProcessThread.StartAddress` returned 0 for all probed owned threads, providing no module attribution. Loaded module presence alone cannot identify which DLL executed on a hot native thread. Further native attribution is unavailable under this session's constraints; another package spike would not repair that evidence gap.

## Results

Accepted chronological order is candidate-profile / baseline-reference / candidate-reference / baseline-profile. Each row is **one independent process**, not 61 independent trials. CPU ms/s is cumulative process CPU delta divided by actual elapsed seconds; it is not machine utilization. References mean no EventPipe collector, not removal of the original in-app monitoring.

| Run / PID | Restored full ms/s | First half | Last half |
|---|---:|---:|---:|
| r2-up-profile / 18884 | 58.69 | 54.86 | 62.51 |
| r3-base-ref / 33592 | 33.64 | 33.89 | 33.40 |
| r4-up-ref / 37848 | 45.22 | 34.44 | 56.00 |
| r5-base-profile / 2552 | 78.62 | 72.39 | 84.86 |

The candidate reference is higher than this baseline reference; profile ordering is reversed. These four points cannot isolate observer overhead from time order/process variability or establish a package regression/no-regression. Do not subtract the profile/reference values as an exact profiler tax. Five-second warmup remains a matched recipe, not a stationarity guarantee. Whole-phase visible/hidden data and all 183 raw rows per process remain in evidence.

### What is actually observable

| Helper endpoint window, UTC | Process CPU ms | Largest pre-existing thread / CPU ms | UI thread / CPU ms, user + kernel | New threads after attach / cumulative CPU ms |
|---|---:|---|---|---|
| up profile 13:53:53.064–13:54:54.150 | 3281.250 | TID32468 / 1609.375, no EventPipe samples | TID28512 / 468.750 = 62.500 + 406.250 | TID14304 / 984.375; TID24360 / 109.375 |
| base profile 14:06:56.419–14:07:57.493 | 4578.125 | TID35672 / 2359.375, no EventPipe samples | TID32628 / 421.875 = 31.250 + 390.625 | TID35460 / 1359.375; TID35824 / 187.500 |

Use archived `before.json`/`after.json` for full-precision endpoint times. Thread snapshots are not atomic; CPU is quantized in 15.625 ms steps on this machine. The largest uncovered pre-existing thread consumes roughly half the profile-window process CPU, but its native/runtime module and method are **unknown**. This is not proof that it is a compositor, AppSDK, Win2D, tray or application animation thread. New threads consume 1093.750/1546.875 ms after attachment, consistent with significant tracing machinery cost; their roles are not symbolized, so this is temporal association, not exact named-method attribution. References have no newly appearing threads in these snapshots.

The baseline/candidate reference helper windows respectively record 2812.500/2750.000 process CPU ms, with largest threads TID37216/2125.000 and TID7408/2218.750 ms. Their narrower plateau rates above have a different ordering. This demonstrates why the two-second restore transition must not be silently pooled into the restored plateau. References have no stacks, so their hot threads cannot be matched to profile threads by recycled/different TIDs.

Both profiled UI threads expose this specific call path:

```text
IdleBaseline.<Measure>d__0.MoveNext
  Process.EnsureState → ProcessManager.GetProcessInfo
    NtProcessInfoHelper.GetProcessInfos → native/kernel boundary
```

Inside complete trace-covered counter intervals there are **193 candidate / 194 baseline samples containing this path** (189/193 with the named leaf, plus 4/1 with an unresolved leaf). The source refreshes process state, then reads CPU, memory, handles and thread count every second on the UI dispatcher. This is direct evidence of in-app observer work in both arms. The entire UI thread's measured CPU is only an upper bound for that work; External samples do not yield its exact CPU time. No UIA query ran during these windows, and `winapp` CPU is outside the target PID's counters. The fixture's `Task.Delay` timer remains part of the scenario.

| Trace-covered restored counter interval | Candidate | Baseline |
|---|---|---|
| Rows | 0–58 | 0–59 |
| UTC | 13:53:55.0734096–13:54:53.4748670 | 14:06:58.3404290–14:07:57.7454217 |
| Same-row process CPU delta | 3171.875 ms | 4703.125 ms |
| Samples, all `Type=External` | 332,950 | 495,828 |
| `Type=Managed` in this interval | 0 | 0 |
| Parser-reported lost events | 0 | 0 |
| Per-thread spacing median / p95 / maximum | 1.9888 / 2.3689 / 3.2720 ms | 1.9902 / 2.3548 / 26.2873 ms |
| Missing stack / samples containing blank method / depth-100 cap reached | 0 / 4 / 0 | 0 / 1 / 0 |

Managed method names come from EventPipe metadata/rundown; no native PDB resolution or native stack capture was available. Having named managed boundary frames is **not** high native symbol coverage. `MpvClient.<Run>b__24_0`, semaphore waits, WaitHandle waits and XAML `Application.Start` dominate sample counts. Their waiting/boundary frames are not CPU work. Baseline has nine sampled mpv event-loop threads and candidate four, illustrating why even sample totals are not comparable CPU quantities; this does not establish a leak or new defect.

Runtime evidence: candidate has no GC Start/Stop in the trace; baseline has one Gen0 `AllocSmall`, nonconcurrent GC at 14:07:13.4457 UTC, with **24.5406 ms suspend-to-restart wall duration** (not CPU time). Suspend reasons are candidate 34,734 `SuspendOther`; baseline 34,091 `SuspendOther` plus one `SuspendForGC`. Counting all `GC/SuspendEEStart` names as collections would be wrong. There are no reported JIT method events, but the selected informational verbosity does not establish exhaustive JIT coverage; no JIT hot work is positively identified. No managed animation/layout or tray callback hot path is identified either; the native/runtime blind spot prevents excluding them.

### Raw trace identity

| Owned trace | Bytes | SHA256 |
|---|---:|---|
| r2-up-profile/profile/cpu.nettrace | 17,756,178 | `a2299bb75e9bcfb9d3aa53901c3861586b23f16e049ee967390151f25359b6e7` |
| r5-base-profile/profile/cpu.nettrace | 19,108,018 | `b13caa8d78559a941b828103b94eeca5e9585de4635c0da1e85f1007cb77c8f7` |

No second round: the first matched profiles establish a native on-CPU coverage limitation; more samples with the same tool would not resolve it. The actionable outcome is a specific monitoring correction plus an explicit native attribution limit, not an indefinite request for overall performance proof.

## Failures and cleanup

Two preparation failures are excluded from comparisons, not erased. `r1-base-ref` rejected its own correctly launched PID because of slash normalization in a path guard; exact normalized path was checked, PID 31468 stopped and only its fresh registration removed (`cleanup-corrected.json`). `r1b-base-ref` passed Player15 but its helper failed to start without shared net9; PID 6392 was deliberately stopped after saving partial phase evidence, then its registration removed. Neither is an accepted complete reference.

The current checkout's initially incomplete nested kernel tree was backed up as an ignored binary patch, then restored to the candidate gitlink using the existing local kernel bundle. No other checkout was edited. Accepted run cleanup/yield records are retained verbatim; protected Caa6a7be registration/layout, login layouts and PID9608/Dev were not operated on. Large raw traces/ETLX/sample streams and immutable layouts remain ignored. Small filtered summaries, original trace hashes, counters, replay source and provenance are committed.

Fresh verification: all four accepted Player15/Main4 results and 732 rows validate; **1,428 deployed DLLs** pass the repeated two-way source hash gate with no extras/missing files, and both trace hashes match. Final read-only audit finds zero task-owned processes/registrations. Explicit final yield is **released:false** (no remaining lease), preserved exactly. Parent `feat/performance-45` is still `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`; production src/global.json and kernel working tree remain unchanged by this diagnosis. [Replay and portable evidence verification](dependency-cpu-profile/README.md).
