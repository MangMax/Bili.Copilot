# #52 lifecycle fix integration review

2026-10-30. The specific real-HTTP blank-image regression is supported as corrected by the incoming red/final evidence. **Full visual and performance acceptance remains incomplete.** This integration did not acquire a desktop or rerun the harness.

## Provenance and scope

Only `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`, was used. Starting HEAD was clean at `1b1c1945103de1f1fa423b7d7c26ffd22dfaccc0`. Merge `ca362d7bf4bdd5ccda60fb136bdf95f247c89fb9` was created with `git merge --no-ff 5c3a28ac34ca08e1b805daf86c597f6a1efd9ee8`; its two parents are exactly those commits. Incoming ancestry includes acceptance cherry-pick `78d527d` (original `4c5dc7c`) and parent integration `5201e08`. The only production delta is MyImage.cs. Historical evidence is preserved without rewriting failures.

Read AGENTS.md, all three docs/agents rules, GitHub issue 52, ui-52.md, issue-52-lifecycle-fix.md, harness assertions, red/final/final-verified structured results and lifecycle observations. Applied winui-code-review, winui-ui-testing, run-tests and verification-before-completion. No test overlay exists; SDK 10.0.403, net9.0 xUnit 2/VSTest. No other fix or kernel review is implied. Kernel gitlink and nested HEAD remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`.

## Static review

No additional substantive production defect established in this scoped diff.

- Loaded and stale Unloaded notifications schedule one current-state reconciliation, not one load per event. The flag is cleared before callback work; `_loaded` and operation identity suppress duplicate loads. Refresh may itself call LoadImage, and the subsequent call is guarded. The callback reads current state rather than retaining the event's obsolete state.
- TryEnqueue rejection resets the flag, so later events can retry. There is no synchronous fallback and no promise of loading after dispatcher shutdown. This branch has static coverage only, not a fault-injected UI pass. A genuinely unloaded host takes ReleaseImage immediately without depending on dispatcher acceptance.
- ReleaseImage clears the active operation before cancellation, detaches ancestor SizeChanged and XamlRoot.Changed subscriptions, resets loaded state and replaces the child. It leaves a provider-owned Image.Source intact. Duplicate real-unload events may create another empty child, but cannot publish an old result. A queued callback after removal sees false and releases; after remount it reconciles the current host.
- Late asynchronous completions must match the current operation, be uncancelled and see a loaded host; old finally blocks cannot clear a newer operation. Custom provider work itself is not cancellable. Noncooperative SVG work may hold a shared slot until it returns; this remains a limitation, not a demonstrated leak.
- Initial Loaded can precede IsLoaded. The red trace for host 13716049 has two Loaded observations with false, then deferred true; Source becomes BitmapImage before stale Unloaded callbacks replace children 22547203, 24678187 and 54940109 with null Source while still parented to RichTextBlock 2279925. OriginalSource is null, so this is not established child-event bubbling. Removed host 52377683 instead becomes false with no visual parent.
- GetAvailableWidth walks the actual VisualTree to the first positive-width FrameworkElement and subscribes there, with XamlRoot width/800 fallback. LoadImage captures actual XamlRoot scale (fallback 1) and subscribes to root changes. IsLoaded is the production predicate; it is not a separate explicit parent/root assertion. Incoming evidence demonstrates the connected RichTextBlock path at scale 1.5, not arbitrary root replacement, fallback-width conditions or monitor migration.
- Refresh before the loaded guard catches width changes missed during temporary inline detachment. Width difference below 1 DIP and equal DPI short-circuit reloading; provided images bypass bitmap resizing. Final resize observes 480 to 300 DIP, then three forced layouts and a 400 ms quiet interval with request count fixed at 5. This bounded result does not prove absence of churn under every layout.
- No XAML, binding, localization or accessibility surface changed. Existing project analyzers run with the build; dedicated WinUIAnalyzer is absent and its extra checks did not run.

## Incoming UI evidence and exact limits

`red/results.json` contains Landscape timeout and cleanup timeout (0/2). `final/results.json` is 27/28: same-inline remount sampled IsLoaded before the Ready status's induced layout settled. Final harness adds a dispatcher flush before that assertion; it still checks the same Border, true loaded state, released old child and exactly one additional HTTP request. `final-verified/results.json` is **28/28**, including cleanup: eight original document lifecycle cases, seventeen image cases, Slots52 and Boundary52. This is inherited execution evidence, not a new parent UI run.

Image cases cover landscape/portrait/square, resize, charset through actual DefaultSVGRenderer, cached provider Source/identity, four late-provider and four held-body transition cases, eight concurrent images, same-inline HTTP remount and same-inline late-provider rejection. Unload and Reattach labels in some document cases share a transition route; they are not independent transition mechanisms. Stream bitmap readiness uses awaited SetSourceAsync publication plus positive source dimensions and loaded layout; URI provider readiness requires ImageOpened. SVG checks actual returned SvgImageSource and positive layout. Preloaded bitmap/SVG source events are true while Image.ImageOpened replay is false; SVG loadStatus is Success. Immediate false observations are retained and are not terminal failures.

At actual scale 1.5, final landscape is 480x240 DIP with requested decode 720x360, portrait 480x960 / 720x1440, square 480x480 / 720x720, resized landscape 300x150 / 450x225. Source PixelWidth/Height remain 2400x1200, 1200x2400 and 2000x2000. Physical request checks enforce 4096 per edge and 4,194,304 pixels; these are not measured native decode allocations.

Final image52.json records **35 requests, 8 connections, 0 active server responses at final save, peak server responses 2**. The separately gated slot experiment records four renderer calls with request count 28 unchanged during its 750 ms bound, then calls 5 / requests 29, oldReturned 4 and oldPublished false. Slots52 terminal state itself still has null Source and zero image dimensions: that assertion establishes slot recovery and stale-result rejection, not current-image readiness. Its later screenshot shows the recovered teal SVG. Server response peaks are not decoder concurrency measurements.

Across 68 state samples, maximum managed bytes are **356,395,928**, private bytes **996,904,960**, and recorded OS peak working set **1,065,160,704**. These include fixtures, HTTP buffers, diagnostics, XAML and JIT in the instrumented process. Sparse managed/private samples are not continuous peaks; the OS value is process lifetime. No baseline-matched improvement, native image-memory peak, soak/leak rate or production throughput conclusion follows.

The exact renderer input contains `café`, but visible SVG output lacks its glyphs. The incoming visual review marks six captures insufficient: RepeatedAttach1Ready, RepeatedAttach2Ready, LateProviderClear, LateProviderReattach, SlowBodyClear and SlowBodyReopen. Preserve that limitation; 28/28 does not certify complete visuals. This integration spot-opened Landscape, SvgCharset and LateProviderClear: bitmap and teal rectangle are visible; no café glyphs. **A provenance discrepancy remains:** LateProviderClear actually shows a small pink logo in this review, despite the incoming note saying it is absent. Its SHA-256 matches the incoming record (`0CA65C9B2E0C5CCBE5A14E8673F837D7B8515F63D491A84E5A6C8D3A7195EAD1`). The historical note is not silently rewritten, and this spot check does not upgrade all six captures or full visual acceptance. The parent should reconcile the visual records during its separate acceptance allocation.

Supplemental screen captures failed twice with foreground_not_target; neither supplied a screenshot. Incoming cleanup confirms nine owned PIDs and the owned temporary package absent. Red/green-a original five-second close failures remain recorded. Final-verified yield reports released:true; later capture-run/redundant yields report released:false. No new process or desktop inspection was performed here.

Still unverified: 100%/200% and cross-monitor DPI, EXIF/representative compressed formats, full app/authenticated cookie isolation, native decode peaks, sustained throughput/retention, theme/accessibility matrix, Release app and Native AOT runtime. The concrete blank-image correction does not close #52 or validate other tickets.

## Fresh parent verification

Executed against merge ca362d7; logs and TRX are in ignored `tests/BiliCopilot.UI.Tests/TestResults/` with prefix `integration-52-lifecycle-fix`.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-52-lifecycle-fix.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -v:normal
```

Release: exit 0, **Passed: 401, Failed: 0, Skipped: 0**. Independently parsed TRX has total/executed/passed 401 and 401 UnitTestResult entries; all other failure/pending/not-executed counters zero. These tests do not execute MyImage or real WinUI lifecycle.

Debug x64 build: exit 0, **0 warnings, 0 errors**, elapsed **00:01:46.77**. Explicit runtime gate: exit 0, **0 warnings, 0 errors**, elapsed **00:00:00.63**. Runtime file presence is not native loading or launch validation. No assets downloaded/copied, deployment, app launch, input, harness rerun, push, PR mutation, issue closure, other checkout or subagent.
