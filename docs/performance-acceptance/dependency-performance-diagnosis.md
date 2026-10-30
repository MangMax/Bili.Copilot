# Dependency performance diagnosis

Local experiment on 2026-10-30, based on review `cce01fd620e54bbd70c8d958f6fa10ed5e5a689f` (fast-forwarded only this research worktree). Stable candidate `60abad7834f7037501fcb99696494d3dcebb8977`, original baseline `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`. Production src tree remains `a5e099655ce21b460392e5a3d0b4f70feb1f8aec`; kernel `0ae5e4e000a41a07f6696746706df11c06e8835b`, with cancellation/pageSize ancestry intact. No production edits, parent merge, remote mutations, default SDK change, RC rebuild or full 487-test rerun.

## Decision

**Native restored-CPU cause remains unlocated. Keep the aggregate candidate on HOLD/experimental; this round supports neither a performance-driven Win2D/icon rollback nor an AppSDK rollback.** Group 1 restored ranges overlap (A 46.80–66.22, B 51.26–67.66 ms/s). Group 2 also overlaps (B 46.92–59.45, C 54.98–56.35). The high candidate signal reproduces in one run but not consistently across independent processes. Two processes per arm, substantial within-phase drift and changing time order do not exonerate any package or establish an overall performance gain. No numeric acceptance threshold was invented.

**Retain final jagged+empty-shortcut LCS.** All tested nonempty results, incremental events and references match the old algorithm. Extra row allocation is real and quantified; final-version throughput has no consistent regression across the bounded workload. Keep the flat-array idea as a separately validated future candidate, not an untested production edit.

The smallest useful next step is a focused CPU profile of one freshly owned full-source fixture through restored first/last halves, correlated with thread CPU and runtime/JIT/GC events (cached DiagnosticsClient 0.2.510501 and TraceEvent 3.1.23 are available). Obtain a repeatable hot-stack signal before trying another package group; distinguish managed sampling coverage from native threads rather than claiming a managed-only trace explains native CPU. This round stops at the two authorized native groups. No CPU profile was collected because the first group gave no stable direction; the authorized second-group branch was followed instead.

All eight accepted processes passed Player15/Main4, 183 rows at 1.5 scale, 20 same-HWND cycles, zero unhandled exceptions and explicit exit. B/C controls each passed 7/7. This is targeted compatibility evidence, not closure of the performance hold, AOT, authenticated navigation or #49.

## Controlled package boundary

The requested Win2D-only rollback is **not resolvable**: FluentIcons.WinUI 2.2.344 requires Win2D >=1.4.0. Restore returned NU1605. The red log and rejected graph are retained; the downgrade warning was not disabled and that closure was never deployed.

The smallest tested compatible rollback uses the cached original FluentIcons version and its exact Common dependency. Host/kernel AppSDK remains 2.6.4; kernel declares no Win2D package. The resolved library set differs exactly as follows (all other package IDs/versions match):

| Package | A: final candidate | B: compatible rollback |
|---|---|---|
| Microsoft.Graphics.Win2D | 1.4.0 | 1.3.2 |
| FluentIcons.WinUI | 2.2.344 | 1.2.315 |
| FluentIcons.Common | 2.1.344 | 1.2.315 |
| Microsoft.Extensions.Hosting.Abstractions | absent | 8.0.0 |
| Microsoft.Extensions.Configuration.Abstractions | absent | 8.0.0 |
| Microsoft.Extensions.Diagnostics.Abstractions | absent | 8.0.0 |
| Microsoft.Extensions.FileProviders.Abstractions | absent | 8.0.0 |

The four additions come from the old icon package's Hosting.Abstractions dependency. Thus this is a graphics/icon compatibility-group comparison, **not a single-Win2D causal experiment**. Resolved package graphs retain dependency edges and package content hashes. Experimental declarations live only in ignored fixture props, with hashes; no new kernel gitlink object is needed.

Group 1 completed A/B/B/A. Restored A rates 66.22/46.80 and B 67.66/51.26 ms/s overlap; B is not consistently lower. Visible B also spans 58.39–115.33. The group does not discriminate the restored signal. It neither justifies rolling back this group for performance nor exonerates Win2D. The full/half table below retains the nonstationarity.

Group 2 therefore fixes B's graphics and compares B/C/C/B, changing only the coherent AppSDK host/kernel group. C uses AppSDK 1.8.251106002 (the original cached selection). Keeping A's Win2D 1.4 would require WinUI >=1.8.260204000, conflicting with that AppSDK's exact WinUI 1.8.251105000 dependency. B is the necessary common graphics closure, not an extra experimental variable between B and C. Temporary kernel central declarations are saved byte-for-byte and restored after the C output is frozen; their patch/hashes and resolver graphs identify the experimental kernel packages without creating a new gitlink.

| Package | B | C |
|---|---|---|
| Microsoft.WindowsAppSDK | 2.6.4 | 1.8.251106002 |
| Microsoft.WindowsAppSDK.Base | 2.6.4 | 1.8.250831001 |
| Microsoft.WindowsAppSDK.Foundation | 2.6.4 | 1.8.251104000 |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.6.4 | 1.8.251104001 |
| Microsoft.WindowsAppSDK.WinUI | 2.6.4 | 1.8.251105000 |
| Microsoft.WindowsAppSDK.DWrite | 2.6.4 | 1.8.25090401 |
| Microsoft.WindowsAppSDK.Widgets | 2.6.4 | 1.8.250904007 |
| Microsoft.WindowsAppSDK.AI | 2.6.4 | 1.8.39 |
| Microsoft.WindowsAppSDK.ML | 2.6.4 | 1.8.2109 |
| Microsoft.WindowsAppSDK.Runtime | 2.6.4 | 1.8.251106002 |
| Microsoft.WindowsAppSDK.Search | 2.6.4 | absent |
| Microsoft.Windows.AI.MachineLearning | 2.4.89 | absent |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | 1.7.20250829.1 |

All other resolved library IDs/versions match B. The ML/Search removals and MSIX transitive version change are part of the actual package-group delta, not silently treated as identical graphs.

Group 2 completed with restored B 46.92/59.45 and C 56.35/54.98 ms/s. The old AppSDK is higher than the first B but lower than the last B; it does not consistently change the restored signal in either direction. First/last-half rates reverse direction across processes (see full table), so selecting only a later/lower half would be misleading.

## Protocol and environment

AMD Ryzen 7 7840HS, 8 cores/16 logical processors, 32 GiB, Windows 10.0.26300, existing balanced power plan. SDK 10.0.403; both native arms and managed benchmark use self-contained .NET 9.0.22, coreclr SHA256 `03A85B6BA7087B41AC63F5C23C534074A4BE81EAA23AC5AAE9ED252C2C28DD1C`. Native libmpv is v0.41.0-1227-ge471496c5, SHA256 `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A`. No machine-wide tools, power/DPI changes, forced GC, credential reads/copies or protected-app operations.

Each arm was built sequentially and frozen before the next build. Recipe-only content was filled once without overwriting output. Activation uses a recipe-free directory; every frozen DLL is hashed against its deployed counterpart. Actual loaded Xaml/Canvas/runtime/libmpv paths and hashes are retained. Main process source is the current candidate with the reviewed offline fixture instrumentation, not an old artifact pointed at current kernel references.

Each accepted independent process runs Player15 before Main4, actual SettingsPage, notifications disabled, scale 1.5, controller hidden during sampling, five-second warmup and two-second phase settling, 61 rows in each visible/hidden/restored phase, then 20 same-HWND hide/restore loops and explicit exit. No builds or benchmarks run concurrently with native sampling. The subsequent font check is outside sampling. Exact UTC launch order and environment records are retained.

CPU ms/s = `(cpu[last] - cpu[first]) / (utc[last] - utc[first]).TotalSeconds`. Full phase uses endpoints 0/60; first half 0/30; last half 30/60. The halves share a boundary. These are 60 correlated intervals from one process, not 61 independent trials; rates are not whole-machine percentages. All rows, including transients, remain in raw evidence. Five seconds of warmup is a matched recipe, not proof of stationarity; thermal/background state is not fully controlled.

## Nonempty-middle LCS result

**Retain the final jagged+shortcut implementation for now.** The changed branch has measurable extra row allocation, but the final version has no consistent throughput regression across these workloads. This is not a general LCS memory optimization. No algorithm change was made merely to improve a benchmark.

Exact sources: rectangular from `eff8d24`, pre-shortcut jagged from `dbea98d`, final jagged+shortcut from `60abad78`. The standalone generator changes only namespaces and records original/generated SHA256. Same executable includes all three implementations, same runtime, same inputs and harness. Six independent processes run R/J/S/S/J/R. Each case has five warmups and twelve retained iterations; allocation/timing covers Compute+Apply, excludes input/list setup and verification. No forced collection. Normal tiered JIT remains enabled, so code layout/tiering and process timing variation limit throughput attribution.

Fifteen named cases per process cover local replacement, middle rotation, middle reversal, duplicate-key reversal and rearrangement using original object references, at 256/1024/2048 total items. Each asserts nonempty trimmed middles (128/512/1024 here), unchanged prefix/suffix anchors, target key sequence, identical incremental event sequence without Reset, and reference-by-reference identical results among all three algorithms. Every process passed all 15 cases and produced 180 measurements (1,080 retained measurements total). These are focused standalone behavior checks, not a rerun of the repository's 487 tests. Maximum matrix payload is about 4 MiB, avoiding large stress allocations.

| Changed middle | Additional bytes per operation, jagged vs rectangular | Jagged vs shortcut |
|---|---:|---|
| 128×128 | 4,624 | Identical allocation and behavior |
| 512×512 | 18,448 | Identical allocation and behavior |
| 1024×1024 | 36,880 | Identical allocation and behavior |

The additional allocation is ~5.6–5.9% at 256 items and ~0.85% at 2048 items. It is deterministic across all repetitions/processes. An empty-middle shortcut cannot remove it. Pre-shortcut jagged has timing outliers (notably the 1024 original-reference case); retain them rather than relabeling them as final-shortcut behavior or claiming the outer branch alone causes a timing win.

This bounded run does not certify sustained GC pressure or middles larger than 1024×1024. The one large rectangular object and many small row objects have different GC placement/lifetime behavior even when byte totals are close.

One bounded diagnostic rerun used the **same already-built executable/DLL** with process-local DOTNET_TieredCompilation=0 and COMPlus_TieredCompilation=0, after all native sampling. Another R/J/S/S/J/R sequence retained all 1,080 measurements and 15 behavior cases per process, with identical warmup/counts and no forced GC. Every allocation result is unchanged. The anomalous default 1024 same-reference pre-shortcut jagged medians (5.105/5.118 ms vs rectangular 2.403/2.613) do not persist as a fixed 2× gap: no-tier medians are jagged 1.944/2.777, rectangular 2.199/2.592, final shortcut 1.774/1.807. This is evidence against attributing the default anomaly solely to row storage, **not proof that tiering alone caused it**: configuration and time changed, and other timing variability remains. Both complete datasets are retained separately; default production behavior is the primary comparison. Total managed evidence is 12 independent processes and 2,160 measured operations; no new binary or algorithm was substituted.

If an allocation budget later warrants a separate candidate, the minimal analyzer-compatible alternative is one `int[]` with `columns = targetEnd-start+1`, checked total length `(currentEnd-start+1)*columns`, and index `(i-start)*columns+(j-start)`. Preserve the existing equality/tie direction, kept masks, removal/insertion ordering and empty shortcut. Such a candidate needs independent behavior/allocation/throughput validation, especially checked-overflow behavior and large-object allocation effects. It is a proposed bounded next experiment, not a validated replacement in this delivery.

## Red attempts and boundaries

- Win2D-only restore: NU1605, never deployed.
- First A process 27140: Player screen capture returned `foreground_not_target` before Main. Process remained responsive. Preserved red capture/events/yield, stopped only the validated owned PID, removed only D34A registration, then restarted the entire sequence with new processes/layouts. Both arms use direct window captures; measured workload is unchanged.
- Controls harness runtime pin attempt: setting RuntimeFrameworkVersion=9.0.22 also caused Windows SDK reference-pack resolution to request nonexistent Microsoft.Windows.SDK.NET.Ref 9.0.22. Removed that fixture-only property, retained the red log, and verified the resulting self-contained runtimeconfig is 9.0.22. No package suppression or product source change.
- Full-source Player fixture builds: A 1549 warnings/0 errors (includes original protobuf import warning); B 1548/0; C 1550/0 (the regenerated original protobuf warning and an older-SDK CS0618 Icon warning account for the two additional messages versus B). Both controls fixture builds 121/0. These are fixture diagnostics, not new claims about production zero-warning gates. Managed harness build 0/0.

Player screenshots show the existing synthetic white media/faded controls/right-edge danmaku clipping; they are not broad visual acceptance. SettingsPage is the real rendered page. WIC stream ownership, non-cancelled decode await, post-decode stale-result rejection, and PR27/43 lifecycle defenses remain intact. Keep **#49 FAIL, AOT blocked, authenticated home unverified, RC experimental**.

Replay commands and evidence conventions: [diagnosis README](dependency-performance-diagnosis/README.md). Raw originals and frozen executables remain under this worktree's ignored `artifacts/dependency-performance-diagnosis` and `artifacts/dependency-upgrade`; committed small evidence/hashes are the portable record. Existing parent account layout and protected Caa6a7be/PID9608/Dev identities were untouched.

## Final provenance and cleanup audit

Fresh two-way deployment audit covers **10 layouts / 3,387 DLLs**, with zero missing, unexpected or mismatched DLLs against frozen all-file manifests. Native A has 357 DLLs, B 361, C 356 (including recipe-only DLLs); controls B/C have 261/256. The loaded system AppSDK modules are separately hashed: 2.6.4.0 for A/B and WindowsAppRuntime1.8_8000.994.2142.0 for C. These counts come from this round, not relabeled historical green artifacts.

Accepted native PIDs: group 1 **31960, 32136, 14652, 3564**; group 2 **9356, 4856, 29972, 36116**. Controls **31924/11948** exited through Close. Aborted screen-capture PID **27140** was stopped only after path/identity validation. Final read-only audit finds none of the five D34 task identities registered and no surviving owned process. Per-run cleanup/yield JSON is preserved. Final explicit yield returns **released:false** because no lease remains; it is not rewritten as true. Frozen outputs remain ignored for reproduction; no shared runtime/framework or protected package was removed.

`archive.py` gzip-preserves original bytes, including JSON line endings; the ledger records original byte lengths/SHA256 and archive mapping. `verify.py` checks raw gates and decompressed hashes; `audit-deployments.py` checks every deployed DLL and rejects extras. Kernel status and production src/global.json diff are clean. No remote publication or parent-branch merge.


## Measurements

CPU entries below are full / first-half / last-half **ms/s**, never independent-sample estimates.

### Native group 1

| Run (chronological) | Visible | Hidden | Restored |
|---|---:|---:|---:|
| d34-g1r-1-a | 44.13 / 46.10 / 42.14 | 20.00 / 21.54 / 18.46 | 66.22 / 83.68 / 48.76 |
| d34-g1r-2-b | 58.39 / 62.54 / 54.25 | 11.27 / 13.32 / 9.22 | 67.66 / 89.60 / 45.67 |
| d34-g1r-3-b | 115.33 / 131.75 / 98.92 | 15.10 / 16.39 / 13.82 | 51.26 / 42.50 / 60.05 |
| d34-g1r-4-a | 53.69 / 67.82 / 39.56 | 20.31 / 20.56 / 20.05 | 46.80 / 34.45 / 59.15 |

### Native group 2

| Run (chronological) | Visible | Hidden | Restored |
|---|---:|---:|---:|
| d34-g2-1-b | 48.37 / 39.11 / 57.63 | 10.52 / 10.78 / 10.27 | 46.92 / 32.81 / 61.03 |
| d34-g2-2-c | 63.27 / 84.36 / 42.17 | 11.06 / 10.80 / 11.31 | 56.35 / 46.26 / 66.45 |
| d34-g2-3-c | 43.99 / 49.40 / 38.59 | 8.48 / 10.80 / 6.17 | 54.98 / 74.50 / 35.46 |
| d34-g2-4-b | 42.70 / 40.14 / 45.25 | 9.77 / 12.88 / 6.68 | 59.45 / 79.75 / 39.14 |

### LCS process-median ranges

Two independent processes per variant; every median uses all twelve measured iterations. Timing in milliseconds. Allocation is bytes/operation (Compute+Apply).

| Items / workload | Rectangular ms | Jagged pre-shortcut ms | Final shortcut ms | Rectangular → final bytes |
|---|---:|---:|---:|---:|
| 256 / replacement | 0.0842–0.0991 | 0.0960–0.1001 | 0.0910–0.0932 | 83,104 → 87,728 |
| 256 / rotation | 0.1086–0.1092 | 0.1121–0.1181 | 0.1038–0.1041 | 78,768 → 83,392 |
| 256 / reversal | 0.1159–0.1222 | 0.1270–0.1278 | 0.1129–0.1177 | 83,072 → 87,696 |
| 256 / duplicate-reversal | 0.1154–0.1230 | 0.1166–0.1208 | 0.1066–0.1208 | 82,496 → 87,120 |
| 256 / same-references | 0.0932–0.1014 | 0.0970–0.0996 | 0.0971–0.0997 | 83,072 → 87,696 |
| 1024 / replacement | 1.1617–1.3592 | 0.7787–0.7933 | 0.7980–0.8190 | 1,116,928 → 1,135,376 |
| 1024 / rotation | 2.1544–2.2044 | 1.2583–1.2835 | 1.3064–1.3245 | 1,099,792 → 1,118,240 |
| 1024 / reversal | 2.3245–2.6283 | 1.2407–1.2746 | 1.3430–1.3715 | 1,116,896 → 1,135,344 |
| 1024 / duplicate-reversal | 1.9188–3.3114 | 1.1054–1.2768 | 1.0990–1.1372 | 1,114,400 → 1,132,848 |
| 1024 / same-references | 2.4030–2.6134 | 5.1047–5.1180 | 1.5054–1.6517 | 1,116,896 → 1,135,344 |
| 2048 / replacement | 4.2483–4.4661 | 3.1631–3.3680 | 3.1910–3.2655 | 4,330,288 → 4,367,168 |
| 2048 / rotation | 4.8272–6.8277 | 3.8283–7.0829 | 3.7865–3.8131 | 4,296,096 → 4,332,976 |
| 2048 / reversal | 4.6037–5.5624 | 5.1236–7.3143 | 3.8846–3.9066 | 4,330,256 → 4,367,136 |
| 2048 / duplicate-reversal | 5.2813–6.3034 | 5.0037–5.3312 | 4.3704–4.7240 | 4,325,264 → 4,362,144 |
| 2048 / same-references | 4.1385–5.7788 | 4.2812–4.8106 | 4.3361–5.1420 | 4,330,256 → 4,367,136 |
