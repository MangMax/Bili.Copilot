# Issue #46 — AI streaming response acceptance

## Scope and provenance

- Scope M, difficulty high: one AI generation lifecycle and its visible response. No provider protocol, shared test infrastructure, CI, player or kernel changes.
- Claimed with `gh issue edit 46 --repo MangMax/Bili.Copilot --add-assignee '@me'` before any repository writes. Read full #46 and #45 bodies/comments (both comment arrays empty), local draft 01 and performance review F01.
- Prepared branch: `t3/issue-46-performance`; initial HEAD `90f357733c30d7cc405a7f6f649922f3ab8ed29c`. `git merge-base --is-ancestor 90f357733c30d7cc405a7f6f649922f3ab8ed29c HEAD` returned 0 before edits.
- Reviewed file lists of pending PRs #27/#43/#44; none changes this AI slice. None merged/cherry-picked. No additional checkout, delegates, PR, push or issue close.
- Skills applied: scope-gate, tdd, code-testing (focused StreamingResponseSession seam; runtime has no code-testing-extensions catalog), run-tests, winui-dev-workflow, winui-design, winui-code-review, winui-ui-testing, verification-before-completion. The input-stream → visible-result seam was already approved in #45/#46. Existing pure-source wildcard links the coordinator into existing xUnit tests; no test-project changes.

## Implementation

One StringBuilder per response collects fragments. The first pending fragment schedules a 75 ms delay and one dispatcher callback; subsequent fragments merge into it. Final completion synchronously publishes the complete answer on the UI continuation and invalidates pending callbacks. Streaming text uses a selectable, wrapping TextBlock with OneWay x:Bind; final text still uses the existing Markdown renderer and CopyAnswer's FinalResult.

Each generation owns its cancellation source through its entire async lifetime. Stop, discard, source switch, clear and regeneration invalidate it. Subtitle/comment completions, queued text, errors, final result and final generating/progress cleanup check the captured token. A cancelled request cannot dispose the new request's token source. Captured model data avoids reading a newly selected model after awaits. Source injection clears the alternate source; delayed prompt initialization only installs prompts for its original source. These preparation/ViewModel paths received static review, not direct ViewModel runtime tests.

## Behavioral validation

Baseline before implementation:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue46-baseline.trx' --results-directory $env:TEMP/issue46-tests
```

Exit 0: Passed 282, Failed 0, Skipped 0.

Red/green cycles: `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~StreamingResponseSessionTests`.

1. Burst test first failed `Assert.Empty`: two fragment publications; minimal batching implementation made it pass.
2. Cancellation test first failed `Assert.Single`: late `oldlate` final publication followed the new result; cancellation invalidation made both tests pass.
3. Added async resumed-producer, Markdown/UTF-16 preservation, empty fragment and fixed replay checks; all six pass. No source-text assertions, real provider calls or fixed sleeps.

| Requirement | Evidence |
| --- | --- |
| Ordered batches and immediate final tail | `BurstPublishesInOrderAndCompletionFlushesTailImmediately` |
| Cancellation plus already queued callback cannot replace new answer | `CancelledSessionCannotPublishOrCompleteOverReplacement` |
| Suspended producer resumes after replacement without clearing text or changing generating state | `LateProducerCannotChangeReplacementTextOrGeneratingState` |
| Complete before pending refresh, repeated completion and split surrogate fragments preserve complete Markdown source | `MarkdownAndSplitUnicodeSurviveCompletionBeforeQueuedRefresh` |
| Empty chunks do not queue updates | `EmptyChunksDoNotScheduleWork` |
| Identical replay produces complete answers with bounded publications | `FixedReplayMeasuresPublicationAndAllocationCosts` |

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter FullyQualifiedName~StreamingResponseSessionTests --logger 'trx;LogFileName=issue46-replay.trx' --results-directory $env:TEMP/issue46-tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=issue46-final.trx' --results-directory $env:TEMP/issue46-tests
```

Results before final integration merge: targeted Passed 6, Failed 0, Skipped 0; full suite Passed 288, Failed 0, Skipped 0. TRX parsed using PowerShell XML; targeted report has 6 executed/6 passed, no errors or incomplete outcomes. Reports live in `C:/Users/hp/AppData/Local/Temp/issue46-tests/`.

## Reproducible micro-replay (not WinUI performance acceptance)

2026-10-30, Windows 11 Pro 10.0.26300, AMD Ryzen 7 7840HS, .NET SDK 10.0.403, Release net9.0 test assembly, x64 host. No account/network/model calls. Existing other agents/builds were active; wall time is not an isolated CPU benchmark. One warm-up then five runs. Fixed 1,024 numbered Markdown-like fragments, virtual arrival 5 ms, drain every 15 fragments (75 ms), final tail immediate. Baseline reconstructs the old two-string append path, omitting Markdown parsing and dispatcher allocation; new path executes production StreamingResponseSession. Allocation uses GC.GetAllocatedBytesForCurrentThread, elapsed uses Stopwatch. Dataset generation and assertions excluded from measured region.

```csv
mode,allocated_bytes,elapsed_ms,publications
baseline,65126224,15.674,1024
batched,2381440,1.044,69
baseline,65126224,17.388,1024
batched,2381440,0.346,69
baseline,65126224,18.291,1024
batched,2381440,0.357,69
baseline,65126224,19.206,1024
batched,2381440,0.420,69
baseline,65126224,14.037,1024
batched,2381440,0.710,69
```

These are managed micro-replay publications, allocations and wall times, NOT actual UI submit/parse counts, keyboard response latency, frame time, GC pause, Native AOT performance or app-wide gains. Virtual refresh wait is at most 75 ms before dispatcher scheduling; actual dispatcher/layout delay is unmeasured. Real same-build input response comparison remains outstanding.

## WinUI tools, review and acceptance status

- WinApp CLI 0.7.1 present. Read `winapp run --help`, used `winapp find-ui TextBlock` and `winapp find-ui --id gallery-textblock-5`.
- Initial project API lookup failed because this checkout was not indexed. After restore, `winapp find-api refresh --project-dir src/Desktop/BiliCopilot.UI` and `winapp find-api check-property TextBlock Text TextWrapping IsTextSelectionEnabled --json --project-dir src/Desktop/BiliCopilot.UI` succeeded (3 found/0 missing). CLI caveats: machine SDK 10.0.28000 exceeds target 10.0.22621; installed runtime 2.6 metadata excluded because referenced App SDK is 1.8.39. XAML compilation is the additional API check.
- Read-only `winapp target snapshot sandbox --json`: not running, not attached, no input/capture readiness, `sandbox_target_stale`. This does not prove Sandbox feature unavailable.
- Parent reserved shared desktop/Sandbox exclusively for #48. No app launch, synthetic input, screenshot, installation, Windows feature enablement or global config changes performed for this ticket. Real UI acceptance is **NOT RUN / NOT PASSED** pending reservation release and reproducible UI replay setup.
- Manual winui-code-review: changed TextBlock keeps OneWay notifications, selection/wrapping, semantic ThemeResource brushes, existing Body style and adds AutomationId; final Markdown control and clipboard source unchanged. Dispatcher owns view state. Cancellation ownership and each async preparation boundary inspected; fixed stale question history and stale source prompt publication during review.
- No new analyzer package added in this focused ticket; Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer is not referenced, so its dedicated checks did not run. Existing compiler/analyzers remain enabled.
- Must still verify real table/code/link rendering, link navigation, complete clipboard answer, stop/regenerate/source switches during preparation and streaming, text selection, responsive input, narrow window and theme/contrast rendering. Pure tests prove exact text, not visual format or actual clipboard/UI behavior.

## Build and integration

Initial build incorrectly supplied repo root as SolutionDir; failed resource lookup CS2001/ResourceGenerator CS8785. Corrected invocation:

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -p:SolutionDir=$PWD/src/ -p:Configuration=Debug -v:minimal
```

The corrected build first failed IDE0005 because the app already globally imports System.Text; removed the duplicate using in the linked pure file. The next run compiled C# and XAML and emitted BiliCopilot.UI.dll with zero warnings, then failed the repository packaging gate because this worktree lacked libmpv. Reused existing local dependencies from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets/` into the same ignored paths in this checkout (no downloads, installation or project/configuration changes):

- `libmpv/x64/libmpv-2.dll`: SHA256 `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A`.
- `ffmpeg/x64/ffmpeg.exe`: SHA256 `A1BCBA486CCE341EB033522F0625BB356AE17B1D124A2BC59CFA22540DB81D4B`.

Final same-command Debug x64 build: **exit 0, 0 warnings, 0 errors**, elapsed 23.71 seconds, with C#/XAML output at `src/_build/x64/Debug/BiliCopilot.UI/bin/`. Build log: `C:/Users/hp/AppData/Local/Temp/issue46-build.log`. This is a build, not an installed/launched app or Native AOT publication.

`git merge --no-edit feat/performance-45` returned `Already up to date.`; latest local integration tip was still `90f357733c30d7cc405a7f6f649922f3ab8ed29c`. No conflicts. `git diff --check` passed. Kernel gitlink remains `6501985d0f30c55c28b3f78c6e045c2536e1d389`, with no changes or unpublished submodule commits. Full Release TRX was parsed: 288 executed/288 passed, no errors or incomplete outcomes.

## Remaining risks / unresolved acceptance

- 75 ms is an initial batching interval, not a measured optimal input-latency budget. TextBlock still lays out the accumulated string once per batch; extremely large responses require real measurement.
- Cancellation/preparation guards in WinUI ViewModel compile and are reviewed but pure coordinator tests do not execute the ViewModel or XAML binding lifecycle.
- UI and AOT acceptance and actual before/after UI performance remain outstanding; no claim that all issue checkboxes passed.
