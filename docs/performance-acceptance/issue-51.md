# Issue #51 — Search suggestion debounce and request ownership

Date: 2026-10-30. Code and deterministic service-boundary tests implemented. **Real WinUI acceptance is pending; this report does not authorize closing the issue.**

## Scope and checkout

- Issue: https://github.com/MangMax/Bili.Copilot/issues/51; spec: https://github.com/MangMax/Bili.Copilot/issues/45.
- Read complete issue bodies/comments with `gh issue view 51 --repo MangMax/Bili.Copilot --json number,title,body,comments` and the same command for 45. Both had no comments. Claimed using `gh issue edit 51 --repo MangMax/Bili.Copilot --add-assignee '@me'`.
- Read root AGENTS.md and docs/agents/{domain,issue-tracker,triage-labels}. Inspected open PR file lists with `gh pr list --repo MangMax/Bili.Copilot --state open --json number,title,files`; no overlapping search implementation in #27/#43/#44/#60.
- Branch: `t3/search-debounce-cancellation`.
- Worktree: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-8c1fb4a7`.
- Before edits, `git fetch origin`, `git rev-parse HEAD feat/performance-45 origin/feat/performance-45`, and ancestor checks confirmed HEAD/local integration `745ac5c7efe814d372c17a1d46c6cfc5cce57cb6`, containing remote integration `ed35a68bb894cd63640435220008c5c79a0d1921`.
- During work, `git merge --no-edit feat/performance-45` fast-forwarded this branch to `175b2c07a67a8654d1046112fde0ae095db45913`. Repeated immediately before final verification: `Already up to date.` Integration itself was not mutated. While the first build ran, integration advanced to `2eac5fac1554e0081bbe93bf0b47eb95d219ead7` (#48). Committed the verified #51 slice, merged that tip, resolved the test logging adapter overlap by keeping the real ILogger dependency introduced by #48, then reran tests/build on the merged tree. Finally merged `eeb28f2c914a2a86491781a3f2942b860db688ee`, which adds only integration-47.md and integration-48.md; source/tests/submodule are identical to the verified tree. Final source-diff and whitespace checks passed; no redundant rebuild for those documentation-only changes.
- `git submodule status --recursive` and `git -C externals/bili-kernel status --short` verified initialized, clean submodule `6501985d0f30c55c28b3f78c6e045c2536e1d389`. Checked service/nested source files exist (17 csproj files). Initially no submodule SHA or source change. The final integration merge inherited #48's gitlink `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`; `git submodule update --init --recursive` checked out that exact integration-owned SHA. No independent submodule edit or gitlink selection was made for #51.
- No other worktrees, subagents, PR, push, issue closure, package deployment, app launch, UI input, installs, or global configuration changes.

## Implementation

- Ordinary UserInput events run a 250 ms cancellable debounce. Focus reload remains immediate, including empty-box recommendations. Keyword changes invalidate in-flight results and clear visible suggestions immediately, before the next delayed request begins.
- Every load owns a distinct cancellation source. The source identity is checked after service awaits, including recommendation-cache writes. Superseding input, region changes, submission cancellation, and unload revoke ownership. A service that ignores cancellation cannot publish a late result. Each operation disposes its own source in its completion scope; an old finally cannot clear a newer request.
- Both generated asynchronous commands allow concurrent executions, so newer input/focus can replace pending work.
- Normal cancellation, including BiliHttpClient's exception wrapper, is quiet only when this operation's token was cancelled and the base exception is OperationCanceledException. Real network failures still log, even after cancellation; unrequested transport cancellation still logs.
- Unload cancels pending debounce/network work and closes/clears suggestions. Focus continuations check interaction version and IsLoaded before reopening a popup.
- Existing UserInput filtering, UpdateTextOnSelect=False, chosen-suggestion submission, 600 ms submission suppression, deferred popup closes, and regional navigation routing remain. No XAML change. Regional options are recreated for the current keyword, including after submission clears the collection.

## Skills and test boundary audit

Applied `tdd` (including tests.md and mocking.md), `code-testing` (including report-safe names/result validation), `run-tests`, `winui-dev-workflow`, `winui-code-review`, `winui-ui-testing`, and `verification-before-completion`, all from `C:/Users/hp/.codex/skills`. `winui-design` was consulted; no XAML was edited, so its XAML-change workflow was not needed.

The issue/spec already approve the search-service test boundary. Focused tests link the production SearchBoxViewModel partial files and SearchSuggestItemViewModel into the existing test project, using the real ISearchService interface/models from the existing bili-kernel reference. No new test package, session abstraction, or shared test/CI framework. The existing headless settings/localization adapters were extended minimally; final integrated tests record errors through the real ILogger interface supplied by #48 (the earlier RED/GREEN runs extended the pre-existing logger adapter); SearchTestAdapters supplies only Windows base/navigation types. Navigation stubs are not evidence of routing acceptance.

A test-local TimeProvider controls the 249/250 ms boundary. Service task completions can ignore cancellation and finish in either order. Tests observe public generated commands, Suggestion contents, service requests/tokens and emitted errors, never private fields or copied business logic. No real network/account or fixed sleeps. code-testing-extensions was not supplied; existing xUnit 2.9.3 conventions were used. No run-tests repository overlay was present. SDK `dotnet --version`: 10.0.403; test target net9.0, xUnit/VSTest. `winapp --version`: 0.7.1.

## Red/green commands and results

Each command used this exact prefix and a corresponding report filename:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-red-late.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-green-late.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-red-debounce.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-green-debounce.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-red-recommendations.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-green-recommendations.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-red-cancellation.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-green-cancellation.trx'
```

| Report suffix | Passed | Failed | Skipped | Exit | Observation |
|---|---:|---:|---:|---:|---|
| red-late | 0 | 1 | 0 | 1 | New keyword did not cancel old request |
| green-late | 1 | 0 | 0 | 0 | Immediate invalidation and late-result rejection |
| red-debounce | 1 | 1 | 0 | 1 | Three service requests before quiet boundary |
| green-debounce | 2 | 0 | 0 | 0 | Only final keyword requested at 250 ms |
| red-recommendations | 2 | 2 | 0 | 1 | Recommendation requests were uncancellable |
| green-recommendations | 4 | 0 | 0 | 0 | Cancelled responses cannot populate cache or suggestions |
| red-cancellation | 5 | 1 | 0 | 1 | Wrapped cancellation logged as error |
| green-cancellation | 6 | 0 | 0 | 0 | Both direct/wrapped cancellation quiet |

These are executed assertion failures, not compile failures. Compatibility/boundary cases were then added to the same focused class.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-focused.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-51-final.trx'
```

- Focused: Passed: 18, Failed: 0, Skipped: 0; exit 0.
- Full existing suite: Passed: 323, Failed: 0, Skipped: 0; exit 0.
- Parsed final TRX with `[xml]$report = Get-Content tests/BiliCopilot.UI.Tests/TestResults/issue-51-final.trx; $report.TestRun.ResultSummary.Counters`: total/executed/passed 323; failed/error/notExecuted 0. TRX artifacts are in this checkout's ignored TestResults directory.

## Final integration verification

After merging integration `2eac5fac1554e0081bbe93bf0b47eb95d219ead7` and initializing its submodule SHA:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-51-integrated.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=issue-51-integrated-focused.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-8c1fb4a7/src/ -p:GenerateAppxPackageOnBuild=false
```

- Integrated full suite: Passed: 338, Failed: 0, Skipped: 0; exit 0. Parsed TRX confirms total/executed/passed 338, failed/error/notExecuted 0.
- That run reported two test-only logger nullability warnings (CS8633/CS8766), from the older ILogger contract now inherited through #48. Matched BeginScope to that interface and delegated its unused scope implementation to the actual NullLogger; no production change.
- Final focused rerun after that correction: Passed: 18, Failed: 0, Skipped: 0; exit 0, no warnings emitted. Parsed focused TRX confirms total/executed/passed 18 and failed/error/notExecuted 0. The test name set and behavioral assertions are unchanged. This focused command is the final verification of the correction; the broader suite was not redundantly repeated.
- Integrated build: exit 0, 0 warnings, 0 errors, 7m43.81s. Output: `C:/Users/hp/AppData/Local/Temp/issue-51-build-integrated.log`.

## Requirement coverage

| Requirement | Evidence |
|---|---|
| 250 ms quiet interval, only final keyword | `BurstRequestsOnlyFinalKeywordAfter250MillisecondsOfQuiet` |
| Invalidate immediately before next request | `NewInputRejectsLateResultsBeforeAnotherRequestStarts`, `NewInputImmediatelyClearsAlreadyVisibleSuggestions` |
| Out-of-order completion, including same keyword | `NewestRequestWinsEvenWhenKeywordIsUnchanged` |
| Old cleanup cannot release newer request | `OldCompletionCannotReleaseTheNewRequestsCancellation` |
| Cancel/clear before timer expiry causes zero network requests | `CancellationOrClearDuringDebouncePreventsAnyRequest` (2 cases) |
| Recommendation cancellation rejects visible and cached stale content | `CancelledRecommendationsCannotReopenSuggestionsOrPopulateCache` (2 cases) |
| Normal and wrapped cancellation not logged | `NormalCancellationIsQuietIncludingTransportWrappedCancellation` (2 cases) |
| Real failures retained on both service paths, even after cancellation | `RealFailuresRemainDiagnosableEvenAfterCancellation` (4 cases) |
| Unrequested timeout/cancellation remains diagnosable | `UnrequestedCancellationRemainsDiagnosable` |
| Empty focus, cache reuse and recommendation setting | `EmptyFocusUsesRecommendationCacheAndHonorsDisabledSetting` |
| Regional choices survive stale global result and clear/refocus | `RegionSwitchRejectsGlobalResultsAndRestoresBothChoicesAfterSubmission` |
| Real unload, popup lifecycle, IME, selection, global/regional navigation | Static wiring review only; UI acceptance blocked below |

The controlled burst is inputs at virtual 0/200/400 ms, no service request at 649 ms, one request for `bili` at 650 ms. The RED implementation made three requests. This is deterministic request-count evidence, **not measured desktop latency, allocation reduction, or a performance percentage**.

## Build and static WinUI review

Existing ignored runtime assets were copied from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets` to the same relative paths here: `libmpv/x64/libmpv-2.dll` (121108480 bytes), `ffmpeg/x64/ffmpeg.exe` (107860992 bytes). BBDown already exists in checkout. No binaries are committed and no dependencies were downloaded manually or installed.

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-8c1fb4a7/src/ -p:GenerateAppxPackageOnBuild=false
# Captured stdout/stderr to C:/Users/hp/AppData/Local/Temp/issue-51-build.log.
git diff --check
```

Initial Debug/x64 build: exit 0, 0 errors, 1 pre-existing protobuf unused-import warning, 3m27.65s. This is a build, not runtime or Native AOT acceptance.

Static review covered MVVM commands, UI-thread continuation affinity, source disposal and ownership, cancellation logging, collection identity, x:Bind source/property notifications, input/submission event order, focus continuation lifecycle, regional choices, and the unchanged XAML. Repeated the diff review after the integrated app build; no outstanding static blocker found in this diff. Existing generated observable fields follow repository style; no unrelated conversion was made. No theme, resource, security, layout, or accessibility structure changed. The existing XAML lacks some explicit AutomationIds; no accessibility PASS is claimed.

`winapp find-api members FrameworkElement --filter IsLoaded --json --project-dir src/Desktop/BiliCopilot.UI` confirmed IsLoaded in the restored app API surface. The tool warned that SDK 22621 is unavailable (using 28000 metadata) and installed Runtime 2.6 differs from referenced App SDK 1.8.39; this is not runtime validation.

WinUIAnalyzer is not referenced by the app, so its checks did not run. The user forbids installs/dependency expansion; no analyzer package was added. Native AOT/trim publish acceptance is not inferred from a Debug build.

## Explicit acceptance blockers

**#48 owns the shared desktop. No parent slot grant was received.** Following winui-ui-testing's execution boundary and the explicit task constraint, no app was launched, no package deployed, no UI input injected, and no screenshot/UIA pass claimed. Existing pending #46/#50/#47 UI acceptance provides no evidence for #51.

When the parent grants a slot, test this exact build in its selected target with winapp UI, retaining target/PID, checking command exits and delivered screenshots, and visually reviewing:

1. Rapid English input; Chinese IME composition followed by mouse and keyboard selection; Enter/query submit. Confirm selected text/navigation and no popup reopening during the existing 600 ms suppression window.
2. Clear while timer/request is pending; focus empty box with recommendations enabled/disabled; refocus nonempty text. Confirm correct content and no stale popup.
3. Navigate away/unload during debounce and in-flight request, then return. Confirm no pending request dispatched or result published after unload; new interaction still works.
4. User-space/history regional search: global and current-region choices, submitted keyword, return/refocus, actual correct destination/results.
5. Controlled slow/error responses: newer result displayed, old result ignored, normal cancellation absent from error logs, real errors retained.
6. Collect actual network request counts/timestamps and visible popup state for the same build/material. Release Native AOT runtime/performance acceptance remains unverified.

No unit test or static check here validates WinUI ABI, actual settings persistence, real IME composition, popup layout, theme/accessibility, actual navigation, or network timing.
