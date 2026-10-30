# Runtime capability adoption

Base `42836305b4e46d4f3e86d26c09c092034300b988`, independent branch
`t3/net10-runtime-adaptation`; parent `eff8d24` is not merged into or modified.
Scope L is split into independently reviewable stages: A capability review,
B stable framework compatibility, C external measurement protocol, D bounded
native comparison. No external push, issue mutation, or delegated agent.

Kernel remains `0ae5e4e000a41a07f6696746706df11c06e8835b`, restored only in this
checkout from the existing verified bundle. Cancellation `2ca79bd` and page-size
`f02c33f` are ancestors. Initial missing-index/deletion state was saved under
`artifacts/net10-adaptation/initial-state` before recovery. No new kernel commit
or bundle is required; parent and other checkouts were not reset.

## Decision rules

New APIs must address an observed problem, preserve existing behavior and have
a separately repeatable acceptance boundary. A framework upgrade alone is not
an algorithm change. Existing runtime upgrades can improve code generation
without source edits; this is a hypothesis, not an application performance claim.

The recorded observer stack is actionable: historical `IdleBaseline.Measure`
refreshes Process state on the UI thread every second. The new protocol moves
sampling into an owned helper, keep CPU/private bytes/working set/handles/thread
counts, and keep the UI free of per-sample work. Historical protocol and samples
remain immutable and cannot be subtracted from the new measurements.

## Runtime capabilities and applicability

Official references checked online on 2026-10-30:

- [10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json): SDK 10.0.403, runtime 10.0.14. Installed SDK matches; no installation or environment-default change required.
- [Runtime improvements](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime): JIT inlining, devirtualization, escape analysis and loop improvements are relevant to list/danmaku loops. Keep existing algorithms for the runtime comparison; no forced GC or speculative tuning.
- [Library APIs](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries): new OrderedDictionary TryAdd/TryGetValue overloads return an entry index; that keyed representation is not a substitute for duplicate-preserving LCS; `RecommendListDiff.Compute` permits duplicate keys and deterministic tie-breaking. No applicable new collection API established for that hotspot.
- New async ZIP APIs have no current application call site (`rg ZipFile|ZipArchive src/Desktop`). Adding an archive subsystem would expand scope.
- JSON PipeReader support is not a drop-in optimization for the kernel's source-generated HTTP response parsing. Kernel retains its compatible TFM; changing buffering and cancellation needs a separate network workload, not idle UI evidence.
- New JSON strictness/duplicate-key rejection changes accepted server payloads. Not adopted without a protocol requirement.
- Async improvements do not permit releasing a Win2D/WIC stream before its outstanding operation finishes. Existing cancellation, decode lifetime and explicit-exit sequencing remain mandatory.
- Diagnostics rate limiting is for Activity export, not OS CPU accounting. The external observer uses Windows counters; it does not introduce tracing into the measured app.
- GC tuning is not selected: process private bytes include native WinUI/mpv allocations, and the prior trace contained no candidate collection and only one baseline Gen0. This does not identify a GC bottleneck. Server GC, heap limits and forced collections would introduce a new experiment instead of fixing an observed problem.
- Span alternate lookup/FrozenDictionary are not automatically new net10 capabilities. No matching repeated substring-to-key allocation hotspot was established; do not replace duplicate-preserving list logic with dictionary identity. The existing nonempty LCS timing evidence does not justify an unrelated new algorithm.
- NativeAOT improvements require a real publish and native toolchain. MSVC remains missing; no AOT command is rerun and Release JIT is not AOT evidence.
- .NET 11 RC remains an experimental candidate. APIs or optimizations documented only for later releases cannot be claimed available in the installed RC. No RC rerun is needed to establish stable net10 compatibility.

The online [.NET 11 runtime page](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime)
describes runtime-native async as preview and newer hardware baselines; neither
is enabled by this candidate. Its [library page](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries)
describes process capture helpers and memory-backed streams. A direct name scan
finds ReadOnlyMemoryStream, RunAndCaptureTextAsync and KillOnParentExit in the
installed RC3 reference DLLs, although the shipped XML does not document those
names (see `net11-api-probe.json`). This establishes a metadata lead, not a
compiled overload/lifetime validation. These remain RC-only exploration, outside
the stable net10 API surface. The app's `DownloadProcessRunner` and WIC stream
lifetime stay unchanged; no new stream adapter permits early disposal during
an outstanding decode. No RC optimization is treated as a net10 speedup.

No production new-API adaptation met the evidence threshold. The
observed diagnostic problem is resolved in a separately versioned fixture, not
by manufacturing a production performance patch. Package capability review and
native acceptance are tracked below. No production algorithm speedup is claimed.

## Package capability evidence

[All 85 actual reference declarations](net10-adaptation/package-capabilities.md)
map old resolved versions to selected versions, concrete usage and minimum
acceptance. [Metadata](net10-adaptation/package-metadata.json) records repository
commits and new/old package assets. The XML comparison includes internal and
multi-TFM documentation: a new XML member is a lead, not proof of a new public
API. Missing XML is explicitly unknown.

Release/source references reviewed:

- [Win2D WinAppSDK branch changelog](https://raw.githubusercontent.com/microsoft/Win2D/winappsdk/main/CHANGELOG.md): 1.4 changes packaging/projections; animated-control CPU/pause fixes predate 1.3.2. The UWP master changelog was rejected as the wrong branch.
- [WindowsAppSDK 2.6.4](https://github.com/microsoft/WindowsAppSDK/releases/tag/v2.6.4): diagnostics/lifecycle reliability and language-model access; no application lifecycle ownership waiver.
- [CsWinRT releases](https://github.com/microsoft/CsWinRT/releases): generator/AOT attributes and ABI support. Build passes do not certify AOT.
- [Toolkit 8.2.251219](https://github.com/CommunityToolkit/Windows/releases/tag/v8.2.251219): Segmented, Sizer, layout fixes. No requested feature requires a new control.
- [MVVM 8.4.2](https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.2) and 8.4.1: C#14 generator compatibility, partial properties and analyzer ordering; broad property migration is L, deferred.
- [WinUIEx 2.9.3](https://github.com/dotMorten/WinUIEx/releases/tag/v2.9.3): persistence/tray fixes; app uses another tray implementation, so no fix is transferred by analogy.
- [Serilog 4.4](https://github.com/serilog/serilog/releases/tag/v4.4.0): SelfMetrics is a possible future diagnostic feature (M), without a demonstrated current sink bottleneck.
- [Markdig 1.5.1](https://github.com/xoofx/markdig/releases/tag/1.5.1), [protobuf 36.2](https://github.com/protocolbuffers/protobuf/releases/tag/v36.2), [gRPC 2.85](https://github.com/grpc/grpc-dotnet/releases/tag/v2.85.0): shipped XML/API review is linked above; parser/protocol migrations need their own workload, not idle samples.

H.NotifyIcon and Richasy packages expose repository commits through their
actual nuspecs. Some GitHub tag/compare pages did not resolve; those failures
are not release evidence. No unverified behavioral fix is attributed to them.
The connector Azure XML delta is generated speech metadata, not proof that all
providers acquired a new feature. Provider-specific live acceptance remains
outside this no-account scenario.

Priority: P0 stable compatibility and observer correction (this task); P1
separate Markdown corpus/protocol roundtrip coverage (M each); P2 provider
feature adaptation (M per provider), MVVM/AOT migration (L, split first).

## Compatibility candidate

App, Visor, Visor.Models and tests target net10; resource generator/UI.Models
retain netstandard2.0 and kernel retains its existing compatible TFMs. Exact
SDK remains pinned with roll-forward disabled. H.NotifyIcon.WinUI becomes 2.4.1.
The app's net9-only direct Metadata workaround is removed; successful restore
is recorded in `artifacts/net10-adaptation/restore.log`. The existing central
Metadata declaration is not a runtime reference and is left alone.
The test host now uses LatestPatch rather than the old net9 LatestMajor escape
hatch, so a future installed major runtime cannot silently replace the stable
net10 verification runtime. Final full-suite execution validates that setting.

## Preserved limitations

#49 pause remains FAIL. Authenticated latest home needs parent-owned protected
package work and is excluded. Native CPU stacks are incomplete; EventPipe wait
samples are not CPU attribution. Historical JSON newline hashes and overwritten
RC output remain parent follow-ups. No old layout, credential, protected PID or
other checkout may be modified. No performance threshold is invented.

## Fresh native red evidence

`r3-net9` passed Player15 and Main4 but crashed during sampling. Application
Error event 1000 identifies the owned path/PID31584, CoreMessagingXP.dll
12.6.4.0 and 0xc000027b. WER records combase.dll/80131509. The full owned WER
report and filtered Application log are retained. No dump/native stowed stack
was available; the root cause is unresolved. All deployed DLLs had passed the
357-file two-way hash gate, so a mixed-output deployment is not established.
The external helper correctly failed when the process exited. This is a real
failed native run, not a performance datum or PASS. No production fix was
invented. One additional identical-binary net9 repeat (`r5-net9`) completed after
the next net10 arm. The failed run remains part of the acceptance record.

The first observer buffered rows until completion; therefore this failed run
has no complete counter file. Subsequent runs preserve completed phase files
and markers for diagnosis, without changing counter APIs/cadence or app code.
The first two runs used a Python launcher shim; explicit supplemental audits
confirm their actual interpreter PIDs exited. Later cleanup records both PIDs.

## Observer correction and experiment boundaries

V2 removed process refresh from the UI dispatcher, but its Python loop enumerated
all system threads. Actual sample wall overhead was 46–67 ms at each run's median.
That discovery disqualifies v2 as the final low-overhead comparison protocol;
`summary-v2.json` and every raw run remain intact as diagnostic/native evidence.
V3 uses the documented process-snapshot `cntThreads` field, with the same CPU,
memory and handle APIs and identical cadence. Twenty alternating owned-self
probes are in `observer-probe.json`. Neither observer is zero overhead, and
wall overhead is not observer CPU usage.

The final comparison uses only fresh v3 runs, fixed order net9/net10/net9/net10,
against the already frozen binaries. Final tray 2.4.1 native acceptance is a
separate v2 run; its memory values are not compared with the common-tray arms.
No algorithm adaptation was selected, so there is no separate production
microbenchmark gain to add to runtime results. No build/test duration is a UI
performance metric.

All three full-source fixture arms share the application source at `a926eaf6`;
the only later production-project change is test-host roll-forward. Fixture
identity constants normalize identically across all C# files. Common tray is
2.3.2; final net10 uses 2.4.1. Net9 additionally needs its direct Metadata pin
and a net9 copy of Visor.Models. `closure-comparison.json` and compressed assets
enumerate unavoidable framework asset/package differences. This is a controlled
target-framework comparison, not an isolated coreclr/JIT substitution.

Each output was frozen before the next build, with no recipe left in the frozen
directory. Every replay checks the complete DLL set in both directions before
activation/measurement, then records loaded-module paths, versions and hashes.
The replay preparation script now directs referenced outputs to per-arm paths;
existing frozen outputs are never regenerated or overwritten.

## Final bounded comparison

All four v3 runs completed Player15, Main4, 20 same-HWND cycles, lazy font
enumeration (0 before first display, 108 after), zero logged unhandled
exceptions and explicit process exit. Net9 checks 357 deployed DLLs per run;
net10 checks 359. Each observer and registration is absent at cleanup.

| v3 run | Visible CPU ms/s | Hidden CPU ms/s | Restored CPU ms/s | Visible private MiB median |
|---|---:|---:|---:|---:|
| r1 net9 | 32.81 | 1.30 | 47.40 | 470.39 |
| r2 net10 | 51.04 | 1.30 | 50.00 | 422.21 |
| r3 net9 | 53.90 | 4.43 | 62.76 | 470.62 |
| r4 net10 | 47.91 | 2.86 | 43.49 | 480.39 |

[Interactive artifact](net10-adaptation/results.html),
[derived data](net10-adaptation/summary.json) and raw phase counters retain all
values. The experimental unit is one process, not each of the 61 correlated
readings. The v3 sampler median wall cost is 10.40–10.98 ms per call across runs;
the largest call is 15.94 ms. It samples once per second outside the UI process.
This measures observer wall cost, not native CPU attribution or zero disturbance.

Visible/restored CPU varies between processes, and the two net10 private-memory
medians span both sides of net9. This does **not** establish an overall performance
win, a memory reduction, or a statistically supported regression. No threshold
was provided or invented. Hidden-process CPU is low in this fixture, but that
does not turn #49 pause into PASS. The v2 crash remains unresolved despite all
four v3 runs completing; changing observers is not evidence of a crash fix.

The final net10/NotifyIcon 2.4.1 closure independently passes Player15/Main4,
20 cycles, first font display and explicit exit. Controls7 passes, including
cover resize/unload/failure/recovery. Screenshots were inspected: the fixture's
synthetic white video and right-edge danmaku clipping remain limitations, not
evidence of production video quality or authenticated content correctness.

Recommendation: use stable .NET10 as the compatibility candidate within these
tested boundaries, with the pre-existing and newly recorded limitations visible
to the parent. Do not market it as a performance upgrade or a fix for the net9
native crash. RC11 stays experimental: none of its preview-only capabilities is
needed to justify this candidate, and no RC/native/AOT acceptance is inferred.

## Final validation and handoff

| Check | Fresh result |
|---|---|
| SDK | 10.0.403, root global.json exact/disable/prerelease false; no installation |
| Debug x64 rebuild | PASS, 0 errors, one existing protobuf unused-import warning |
| Release x64 JIT rebuild | PASS, 0 errors, same warning |
| Visor Release JIT | PASS, 0 errors/warnings |
| ValidateRuntimeDependencies x64 | PASS: libmpv, ffmpeg, BBDown assets present |
| Full suite after LatestPatch change | 487 PASS, 0 FAIL, 0 skipped; fresh `tests-final/final-net10.trx` |
| Final tray 2.4.1 native closure | Player15/Main4/20 cycles/font first display/explicit exit PASS |
| Controls native closure | 7 PASS; 258-DLL two-way gate |
| v3 runtime comparison | net9/net10/net9/net10, all complete; no overall performance-win claim |
| NativeAOT | NOT RUN: existing MSVC absence; no empty publish retry |
| Ownership | All 11 native run identities absent; per-run process/helper cleanup and yield archived |

`verification.json` records raw archive-hash verification, 3,223 DLLs across nine
completed application runs, the controls layout and all three frozen outputs.
The four final production/test NuGet assets files are archived as raw-byte gzip
snapshots and checked against `production-package-graphs.json`. Archive ledger:
649 files recorded, 530 compressed small files; screenshots retain raw hashes
and local originals. `final-ownership.json` records exact PIDs/identities, parent
and kernel. No authenticated account/package, old layout, global SDK default,
DPI setting or external branch was changed.

Reviewable commits after the imported research tip:

- `a926eaf6`: stable net10 targets, tray 2.4.1, removal of direct net9 Metadata workaround.
- `ef028f5e`: initial actual-package capability/source audit.
- `2bb29261`: external observer v2 and frozen replay; retained as history.
- `bbfed951`: separately versioned v3 process-snapshot observer and overhead probe.
- `e9ce1617`: LatestPatch test-host constraint, verified by the final 487-case run.
- Final documentation/evidence commit: capability precision, bounded results, raw archive and replay/verification scripts; no production C# algorithm change.

The per-arm reference-output path hardening was verified by MSBuild property
evaluation (`output-isolation.log`); it was not used to rebuild the frozen
accepted arms. Source closure checks report no normalized C# difference between
runtime arms or final/common tray arms. Production outputs were serially rebuilt
after sampling, so the earlier net9 fixture reference build cannot remain in
the checkout's current stable Release output.

Parent follow-ups remain explicit: five historical JSON newline/hash records,
overwritten old RC output, authenticated latest-home protected-package update,
#49 pause FAIL, and the unresolved v2 native crash. No parent merge, push, PR,
issue update or fresh kernel bundle is performed by this task.
