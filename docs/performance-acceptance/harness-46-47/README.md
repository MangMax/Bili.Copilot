# Offline acceptance harness for #46 / #47

This is a temporary source-linked WinUI harness, not a production route. Run only with ownership of the local desktop slot. It never launches BiliCopilot Dev/production, reads credentials, or calls a model provider.

`prepare.ps1` copies these harness files into a new temporary directory. It links all production Markdown C# files and `StreamingResponseSession.cs`, copies the production control template, and extracts the `AIStreamingResponse` TextBlock from `AIBody.xaml` with only its ViewModel binding removed. Pin/record the source checkout SHA before running. Build and registration use an independent `BiliCopilot.Acceptance4647` identity. Do not run competing copies of this harness identity.

Only localization and the post-copy notification service are stubbed. The image provider supplies a packaged local PNG using the production `IImageProvider` seam. Parsing, document generation, syntax highlighting, hyperlink construction, table layout, cleanup, session scheduling/cancellation, and code-block clipboard writes execute the actual linked sources. The full-answer copy button is harness wiring and does **not** test the production AI copy command. This does not exercise AIViewModel source-change/regeneration entry points, authentication, provider adapters, network image loading, or Release/AOT.

Use the already installed .NET SDK and WinApp CLI. The project is self-contained because this machine has no shared .NET 9 runtime; its runtime pack was already cached. No environment installation is required. The production manifests and code are never edited; the runner verifies their original checksums in `finally`, closes only its PID, removes only its temporary package (after matching its layout), and yields the desktop.

```powershell
$source = (Resolve-Path .).Path
$project = Join-Path $env:TEMP ('bili-accept-4647-' + [guid]::NewGuid().ToString('N'))
$layout = "$project-layout"
$evidence = "$project-evidence"
if (Get-AppxPackage -Name BiliCopilot.Acceptance4647) { throw 'Identity already registered: do not take over or uninstall another task package.' }
./docs/performance-acceptance/harness-46-47/prepare.ps1 -SourceRoot $source -Destination $project
dotnet build "$project/Harness.csproj" -p:Platform=x64 -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed' }
$launch = winapp run "$project/Harness.csproj" --arch x64 --no-build --detach --json --output-appx-directory $layout | ConvertFrom-Json
if ($launch.Error -or -not $launch.ProcessId) { throw 'Harness activation failed' }
if ($launch.ProcessId -eq 9608 -or -not (Get-Process -Id $launch.ProcessId).Path.StartsWith($layout + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected process: refuse UI actions.' }
./docs/performance-acceptance/harness-46-47/run-ui.ps1 -AppPid $launch.ProcessId -Layout $layout -PreparedProject $project -Evidence $evidence
```

At `eeb28f2`, expect exit **1**, because `SameInstanceUnchangedTextAttach` reproduces a blank document after a real Loaded event. Do not weaken this assertion. Read `results.json`, `observations.json`, each UIA tree and **view every PNG**; UIA assertions cannot detect visual layout defects. The source harness assertion checks visible document content and public control state, never source-text patterns or private fields.

Fixed fixture: `fixture.md`, UTF-8, LF, 312 UTF-16 characters. Streaming sends the first 156 characters individually before draining one queued callback, then appends the remainder and completes before the final queued callback drains. Expected publications: one interim plus one final. Cancellation injects an old queued flush, late append and late completion both before and after replacement completion. The fixture uses only synthetic text; clipboard checks never read pre-existing clipboard data.

GC profiling is separate. On this machine `dotnet-trace`, `dotnet-counters`, and PerfView were unavailable. System WPR exists, but `wpr -start DotNET -filemode` failed with `0xc5585011` (policy enablement failure); `wpr -status` confirmed no recording. No elevation, policy change or installation was attempted, and there is no runtime GC/performance claim.

## Fixed-source recheck

The original `SameInstanceUnchangedTextAttach` assertion remains unchanged. The fixed-source run additionally checks `InitialTemplateLoaded`, `RepeatedAttach1` through `RepeatedAttach3`, `EmptyTextReattach`, and `LifecycleBoundaries`. Repeated attaches require exactly seven blocks, preserving the same control and fixture; empty reattachment requires zero. These are real WinUI event/layout assertions, not private-field/source-text tests. Reassignment is observed through the same low-priority dispatcher helper as the original cases.

The runner waits for `HarnessStatus` before looking for the owned window, and records bounded image/layout readiness before direct HWND screenshots. The diagnosis runner also attempts a screen-region capture at the same stable checkpoint, records capture times/errors, and does not use a compositor sleep. Screen-region capture can fail with `foreground_not_target`; direct window capture avoids capturing other applications. The historical title suffix `eeb28f2` remains a harness selector, **not** source provenance: record the actual checkout and assembly hashes. See [fixed recheck](../ui-46-47-fixed.md). Never overwrite earlier evidence; use a new evidence directory for each run.


## Predicate and capture diagnosis

`Diagnostics.cs` records each original Rendered term, document and table/code text, hyperlink URIs, control identity, exact blocks, and image source, dimensions, pixel dimensions, creation/open/failure times. `FirstLoadedImmediate` and `LifecycleAssignmentImmediate` additionally sample the synchronous boundary. `Observe` still records the original assertion after its original low-priority dispatcher + UpdateLayout; it then records a separate `*Ready` result. An image predicate may be true before ImageOpened: positive dimensions alone are not load completion.

Readiness subscribes before checking current state, completes on ImageOpened/LayoutUpdated with positive image size (or already observed opened/sized state), terminates on ImageFailed or a 3-second timeout, and removes temporary subscriptions in finally. It never changes Markdown Text or repeats an operation to make it pass. Images are tracked through the existing offline provider seam; diagnostic references live only for this finite process. Repeated mounts retain the exact original content/identity/block predicates and now preserve a failing immediate record while allowing its separate terminal observation. A 30-second Continue checkpoint permits a screenshot of each repeat and the empty reattach without rerendering. The runner checks terminal readiness; it is **not** a claim that all original immediate records passed. Always examine both series in observations.json.

Use a new evidence directory. The first diagnostic attempt and its tested source snapshots are retained under `evidence-46-47-diagnosis/instrumentation-attempt`; the final run is at its root. See [diagnosis report](../ui-46-47-diagnosis.md). No production code is changed by this instrumentation.
