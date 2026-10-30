# Runtime capability adoption (work in progress)

Base `42836305b4e46d4f3e86d26c09c092034300b988`, independent branch
`t3/net10-runtime-adaptation`; parent `eff8d24` is not merged into or modified.
Scope L is split into independently reviewable stages: A capability review,
B stable framework compatibility, C external measurement protocol, D bounded
native comparison. No external push, issue mutation, or delegated agent.

## Decision rules

New APIs must address an observed problem, preserve existing behavior and have
a separately repeatable acceptance boundary. A framework upgrade alone is not
an algorithm change. Existing runtime upgrades can improve code generation
without source edits; this is a hypothesis, not an application performance claim.

The recorded observer stack is actionable: historical `IdleBaseline.Measure`
refreshes Process state on the UI thread every second. A new protocol will move
sampling into an owned helper, keep CPU/private bytes/working set/handles/thread
counts, and keep the UI free of per-sample work. Historical protocol and samples
remain immutable and cannot be subtracted from the new measurements.

## Runtime capabilities and applicability

Official references checked online on 2026-10-30:

- [10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json): SDK 10.0.403, runtime 10.0.14. Installed SDK matches; no installation or environment-default change required.
- [Runtime improvements](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime): JIT inlining, devirtualization, escape analysis and loop improvements are relevant to list/danmaku loops. Keep existing algorithms for the runtime comparison; no forced GC or speculative tuning.
- [Library APIs](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries): OrderedDictionary indexed lookup is not a substitute for duplicate-preserving LCS; `RecommendListDiff.Compute` permits duplicate keys and deterministic tie-breaking. No applicable new collection API established for that hotspot.
- New async ZIP APIs have no current application call site (`rg ZipFile|ZipArchive src/Desktop`). Adding an archive subsystem would expand scope.
- JSON PipeReader support is not a drop-in optimization for the kernel's source-generated HTTP response parsing. Kernel retains its compatible TFM; changing buffering and cancellation needs a separate network workload, not idle UI evidence.
- New JSON strictness/duplicate-key rejection changes accepted server payloads. Not adopted without a protocol requirement.
- Async improvements do not permit releasing a Win2D/WIC stream before its outstanding operation finishes. Existing cancellation, decode lifetime and explicit-exit sequencing remain mandatory.
- Diagnostics rate limiting is for Activity export, not OS CPU accounting. The external observer uses Windows counters; it does not introduce tracing into the measured app.
- NativeAOT improvements require a real publish and native toolchain. MSVC remains missing; no AOT command is rerun and Release JIT is not AOT evidence.
- .NET 11 RC remains an experimental candidate. APIs or optimizations documented only for later releases cannot be claimed available in the installed RC. No RC rerun is needed to establish stable net10 compatibility.

No production new-API adaptation met the evidence threshold. The
observed diagnostic problem is resolved in a separately versioned fixture, not
by manufacturing a production performance patch. Package capability review and
native acceptance are tracked below; this document is not a completion report.

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

## Preserved limitations

#49 pause remains FAIL. Authenticated latest home needs parent-owned protected
package work and is excluded. Native CPU stacks are incomplete; EventPipe wait
samples are not CPU attribution. Historical JSON newline hashes and overwritten
RC output remain parent follow-ups. No old layout, credential, protected PID or
other checkout may be modified. No performance threshold is invented.
