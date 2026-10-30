# Synthetic Popular control observation (#56 supplement)

Requires an explicitly granted exclusive local desktop. No login, credentials,
production package, PID 9608, environment installation, forced GC or system DPI change.
Do not interpret synthetic responses as the original real-business 50-page acceptance.

The production PopularPage, both headers/bodies, PopularPageViewModel partials,
VideoItemViewModel partials, VideoCardControl, BasicCoverImage, BasicScaleEffect,
IncrementVerticalScrollControl and real NuGet AppRepeater/base classes run unchanged.
The production page/body/scroll XAML is copied verbatim. Three real card styles are
extracted using the existing #50/#53 preparation script; they are not simple templates.

Adapters: application composition, in-memory layout/last-section settings, localizer,
duration/count display formatting, fail-fast unused navigation/player/menu boundaries,
and deterministic IVideoDiscoveryService. Every response creates actual kernel models.
IDs 0–9 overlap deliberately between sections; no item instance is reused by the service.
The only HTTP handler accepts fixture.invalid and returns packaged Logo.png bytes; it
never sends a network request. Real kernel image decoding, caching and draw paths remain.
The kernel logger is initialized (unlike the old #50/#53 fixture).

## Reproduction

Use new paths for every build/layout/run. The package identity must be absent.
`replay.ps1 -SourceRoot <repo> -Scratch <new-directory> -Evidence <new-directory>`
performs cached builds and two sequential runs, then validates data/provenance and
generates the chart. It still requires manual individual PNG review. The component
commands below were executed in this investigation; the final convenience wrapper
was syntax-checked rather than running an unnecessary third pair.

```powershell
./docs/performance-acceptance/harness-56/prepare.ps1 -SourceRoot $PWD -Destination C:/new-owned-cache56
dotnet build C:/new-owned-cache56/Harness.csproj -c Release -p:Platform=x64 -p:RestoreSources=C:/Users/hp/.nuget/packages -p:NuGetAudit=false
dotnet build docs/performance-acceptance/harness-47-gc/Trace/Trace.csproj -c Release -p:RestoreSources=C:/Users/hp/.nuget/packages -p:NuGetAudit=false
./docs/performance-acceptance/harness-56/run.ps1 -Project C:/new-owned-cache56/Harness.csproj -Layout C:/new-owned-cache56-layout -Evidence C:/new-owned-cache56-evidence -TracePrefix C:/new-owned-cache56-trace
```

Use the same built project with another fresh layout/evidence/trace path for the repeat.
The runner verifies PID/path ownership, launches packaged Release JIT x64, records
loaded-module hashes, captures phase checkpoints and always attempts owned cleanup/yield.
The Trace utility uses existing cached assemblies and targets only this PID; no heap dump.

Phases: A30 → A150 → A450 middle → bottom → B150 → C60 → A and ten B/C/A
loops with no manual loading → collapse the same page → show → ordinary A refresh
→ ten-second weak-reference observation. Each switch first explicitly scrolls to top:
this deliberately avoids claiming implicit per-section scroll restoration. Commands run
through the real generated VM commands; side selection is not driven by a user click.
The sidebar focus/selection highlight may remain at Recommend. This is not a full
production navigation test. Hiding sets the actual page Visibility; MainFrame itself
is not hosted. No partition, sorting, refresh-undo or account-change scenario is tested.

Readiness: no VM load pending, all positive-width BasicCoverImage instances have a
non-null decoded CanvasImageSource and are not loading, and request/update/geometry/
item/tree-count fingerprint remains identical for at least 1200 ms. Bound is 20 seconds.
The observation tail is a fixed ten-second scalar sample window, not a readiness fence.
Reflection only reads private cache/cursors/displayed section and protected image state.
Nothing writes production fields. UIA is only a driver/capture tool, never the card oracle.

`realizedCards` is the number of actual VideoCardControl instances traversed in the
main visual subtree, including any retained/recycling elements. It is not an
ItemsRepeater active-index count or whole-heap count. `visibleCards` adds positive
dimensions, own visibility and rectangular intersection with the ScrollView viewport;
collapsed page forces zero. It counts partially visible edge cards, not pixel occlusion.
Cache `Count` is occupied reference slots; Capacity is separately logged. Unique VM
identity covers active Videos plus cache lists only, not every VM in the heap. Weak card
references cover distinct cards encountered at checkpoints only, and do not root them.
Old VM weak references are captured immediately before ordinary refresh, with NoInlining.
No forced collection or extra allocation-pressure experiment is used.

`analyze.py <evidence-root> <run-name> ...` validates exact stimulus, cache identity/ID
counts, sequence hashes, cursor preservation and no added requests during ten loops,
decoded images, hidden-page counts and cleanup, and writes summary/compact GC events.
It does not assert an arbitrary memory ceiling, no-leak conclusion or production budget.
Review every screenshot manually. Keep raw failures; large EventPipe files remain ignored
with hashes, and the compact GC JSON is a subset of those original traces.
