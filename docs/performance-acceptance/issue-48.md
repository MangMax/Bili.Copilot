# Issue #48 — 弹幕加载会话取消与隔离

## Scope and provenance

- Scope M: only #48, parent spec #45. No sliding-window prefetch, renderer rewrite, shared CI framework, or unrelated PR changes.
- Started on `t3/implement-performance-issue-48` at `90f357733c30d7cc405a7f6f649922f3ab8ed29c`; ancestry verified before editing. Claimed with `gh issue edit 48 --repo MangMax/Bili.Copilot --add-assignee @me`.
- Read full #48/#45 body/comments (no comments at read time), local plan P03 and review F03, repository agent/domain guidance; inspected #27/#43/#44 changed paths without merging them.
- Applied skills: systematic-debugging (deployment identity diagnosis), scope-gate, tdd (the service seam was already approved in #45/#48), code-testing, run-tests, winui-dev-workflow, winui-code-review, winui-ui-testing, verification-before-completion. No XAML edits, so winui-design not needed. No delegates. No code-testing-extensions catalog was available; used existing xUnit conventions. Broad test research/plan/status are in worktree-specific Git metadata, not staged.

## Implementation

- `DanmakuLoadSession` uses the existing `IDanmakuService` boundary, six concurrent segments per load and cancellation-aware semaphore waits. A permit is released only after acquisition; semaphore and cancellation source are disposed after all segment tasks settle.
- A load replacement, media transition, reload or disposal invalidates pending dispatcher publications. The callback rechecks session identity under the same lock as invalidation, covering data, count/loading state and errors even when a service ignores cancellation.
- Video/PGC identifiers are captured before awaiting. Async relay commands permit overlapping replacement loads; an old playback-initialization continuation cannot start danmaku after a newer transition. Closing permanently prevents future loads.
- Segment failures still log and allow partial results. Normal cancellation, including wrapped failures after cancellation, is silent.
- bili-kernel preserves caller cancellation through HTTP and checks cancellation before each Web/gRPC segment attempt and before retrying. Non-cancellation failures still attempt three sends; transport timeout without caller cancellation remains a wrapped failure. An owned `HttpMessageHandler` overload supports deterministic real-service transport tests without live API calls.

## Reproducible nested repository

- Base gitlink: `6501985d0f30c55c28b3f78c6e045c2536e1d389`.
- New kernel commit: `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`.
- Published, with explicit parent authorization, to `https://github.com/MangMax/bili-kernel/tree/fix/copilot-48-danmaku-cancellation`.
- `git -C externals/bili-kernel ls-remote --heads origin refs/heads/fix/copilot-48-danmaku-cancellation` returned that exact SHA before the parent gitlink was staged. Keep this remote branch reachable until the commit is retained by a permanent ref. No default-branch push, history rewrite or kernel PR.
- Nested repository initially lacked Git author identity. Commit used command-local `-c user.name=MangMax -c user.email=hi@mang.zone`, matching the main repository configuration; no global config changes.

## Automated evidence (2026-10-30, Windows, .NET SDK 10.0.403)

Runner: SDK-style net9.0, xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, VSTest. No repository run-tests overlay. Test artifacts are under `tests/BiliCopilot.UI.Tests/TestResults/` (ignored).

Baseline command:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-48-baseline.trx'
```

Passed: 282, Failed: 0, Skipped: 0.

TDD evidence:

- `issue-48-red-switch.trx`: `Switch_LateOldResult_OnlyCurrentSessionPublishes` failed (old token was not canceled), then `issue-48-green-switch.trx` passed after session isolation.
- `issue-48-red-limit.trx`: `Cancel_SixActiveAndQueuedSegments_StopsSchedulingAndPublishing` failed (one active request instead of six), then passed with cancellation-aware six-permit loading.
- `issue-48-red-http.trx`: `HttpSend_CallerCanceled_PreservesCancellation` failed with `KernelException` instead of cancellation, then passed. An initial `--no-build` filter found zero tests; that attempt was not counted as evidence and was rerun after compiling.
- `issue-48-red-retry.trx`: Web and gRPC `Segment_CanceledDuringSend_DoesNotRetry` both failed with three actual handler sends instead of one; after the retry fix `issue-48-green-retry.trx` passed all 11 then-current targeted cases.

Final full-suite command:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --logger 'trx;LogFileName=issue-48-final.trx'
```

Passed: 297, Failed: 0, Skipped: 0. Parsed the final TRX XML: total/executed/passed 297, failed/notExecuted 0. Seven session cases and eight transport cases are new; no source-text assertions or fixed sleeps. The earlier `issue-48-all.trx` run passed 296 before adding the explicit close-while-active variant.

| Requirement | Evidence |
|---|---|
| Cancel active and waiting segments; no new obsolete sends | `CancelOrClose_SixActiveAndQueuedSegments_StopsSchedulingAndPublishing` (cancel/close): 20 planned segments, exactly six started, all tokens canceled, no later starts |
| Late results and queued state/count publication belong only to current load | `Switch_LateOldResult_OnlyCurrentSessionPublishes`: old service deliberately ignores token, only new loading/final state and one new item delivered |
| Close invalidates queued success and late error, forbids future work | `Close_QueuedCompletionAndLateFailure_PublishesNothingAndRejectsNewLoads` |
| Continuous reload of the same identifiers | `Reload_ContinuousSameMediaLoads_OnlyLastResultAndCountPublish`: five loads, reversed completion order, only last item/count published |
| Six permits, failures release permits, partial results preserved | `Load_FailedSegment_ReleasesPermitAndPublishesRemainingSegments`: 20 requests, maximum six active, zero left active, one error, 19 items |
| Empty media state | `Load_EmptyMedia_CompletesWithEmptyCountAndNoRequests` |
| Caller cancellation preserved by HTTP | `HttpSend_CallerCanceled_PreservesCancellation` |
| No retry after cancellation; no send when already canceled | `Segment_CanceledDuringSend_DoesNotRetry` and `Segment_AlreadyCanceled_DoesNotSend`, each for Web and gRPC |
| Ordinary failures and timeout compatibility | `Segment_OrdinaryFailure_RetriesThreeTimes` (Web/gRPC), `HttpSend_TimeoutWithoutCallerCancellation_KeepsWrappedFailure` |
| Real video/PGC part-switch, reload, close | Blocked by login in the actually launched isolated package; unit tests prove shared session boundary, not actual view wiring |

## Build and real WinUI acceptance

Debug x64 initial build succeeded with 0 errors and one existing protobuf unused-import warning. Final explicit-x64 project build/launch succeeded with 0 warnings and 0 errors. Actual startup window/UI tree/screenshot were verified, but **ticket player UI acceptance is BLOCKED by login**, not passed.

Environment discovery:

- WinApp CLI `0.7.1`; inspected `run`, `target`, `ui inspect`, `ui screenshot` help.
- Sandbox snapshot was stale/not running, which alone was not treated as unavailable. `winapp target exec sandbox -- cmd /c ver` then failed explicitly: `Windows Sandbox must be enabled before this command can run.` No feature enabled. Read-only `Get-WindowsOptionalFeature` was denied elevation. Selected local target with parent's exclusive desktop reservation.
- Existing PID 9608 belongs to another task's `Mang.BiliCopilot.TrayAcceptance.T3` layout; not used as evidence or modified.
- Required ignored libmpv/ffmpeg assets copied from the existing main checkout into this worktree. No package upgrades, environment installs, or global configuration changes.
- WinUIAnalyzer is not referenced by the existing app; its checks did not run. No analyzer dependency added outside ticket scope.

## Remaining validation and limitations

- Independent package has no authenticated session and displays only login. Real video/PGC part switches, reloads, continuous switches and player close during network loading remain unverified. Do not close #48 on this evidence alone if full WinUI acceptance is required.
- Release/Native AOT publication, production performance/memory sampling, high-density long-video behavior and actual request traces in WinUI are not verified. No performance percentage claimed.
- A transport that ignores cancellation can continue executing its already-started calls; the session drops their outputs and stops queued work. The concurrency bound is six per load, matching prior behavior.
- Final branch synchronization and clean-worktree evidence is recorded below.

## WinUI code review

Reviewed the new coordinator, both transport changes, and all PlayerViewModel lifecycle/dispatcher wiring after the first successful app build.

- Error findings addressed: old command execution could block a replacement; stale playback-ready continuation could initiate a load after transition; old queued callbacks could publish state after cancellation; canceled semaphore waits must not release permits.
- Warning addressed during review: removed an unnecessary full-list `ToList()` copy at final renderer initialization; ownership of the completed list transfers to the UI.
- No new XAML, binding, theme, accessibility, localized user-facing text, native interop or AOT reflection changes. Existing unrelated patterns remain outside #48. No unresolved static correctness finding in this slice.
- Limitation: static review does not validate actual video/PGC navigation or rendering, and WinUIAnalyzer / Release-AOT checks were not run.
### Build / launch commands and deployment diagnosis

Successful initial compile (exit 0, 1 existing protobuf warning, 0 errors):

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9ac33023/src/ -p:BuildOutDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9ac33023/_build/issue48/ -v:minimal
```

Local raw build log: `%TEMP%/issue-48-build.log`.

1. `winapp run ... --no-build --arch x64 --detach --json` refused to replace the already-installed non-development `Mang.BiliCopilot.Dev_2.2610.0.20_x64__y1mf40g4r9srg`. Did not uninstall or modify that package.
2. Passing an independent source via `--manifest` still staged the generated Dev manifest. Inspected the actual layout manifest to confirm this; deployment failed with `0x80073CFB`. No success claimed.
3. Temporarily changing only the source package identity to `Mang.BiliCopilot.Issue48` correctly generated an isolated identity, but `winapp --arch x64` alone left this project's `Platform` at `AnyCPU`; its existing runtime-dependency packaging target rejected that. Application compilation succeeded, overall command failed. The original manifest bytes were restored in `finally`.
4. Successful project-mode build/launch with explicit `-p Platform=x64`, the same temporary identity, and a fresh output layout. No tracked manifest change is intended for delivery:

```powershell
winapp run src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore --arch x64 --detach --json --output-appx-directory "$env:TEMP/BiliCopilot-issue-48/layout-v3" -p Platform=x64 -p SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9ac33023/src/ -p BuildOutDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9ac33023/_build/issue48/
```

Raw launch attempts: `%TEMP%/issue-48-launch.json`, `issue-48-isolated-launch.json`, `issue-48-launch-v2.json`, `issue-48-launch-v3.json`. Temporary identity is strictly an acceptance deployment, not an application feature or committed project configuration.

### Actual local UI outcome

- Corrected project-mode run exited 0: 0 warnings, 0 errors; AUMID `Mang.BiliCopilot.Issue48_y1mf40g4r9srg!App`, PID `45532`, owned layout `%TEMP%/BiliCopilot-issue-48/layout-v3`.
- Original `Package.Dev.appxmanifest` restored byte-for-byte; `git diff --exit-code -- src/Desktop/BiliCopilot.UI/Package.Dev.appxmanifest` exited 0. No application identity change delivered.
- First inspection occurred before a window existed and recorded failure, not PASS. Later inspection found real HWND `3876138`, 1080×690, DPI 144 (150%), 24 UIA elements. The app's existing Visor initialization logged a timeout before the login window appeared; this is not attributed to #48.
- Ran `winapp ui list-windows -a 45532 --json`, `winapp ui inspect -w 3876138 --json --depth 8`, and `winapp ui screenshot -w 3876138 --output <temp>/BiliCopilot-issue-48/initial.png --json`. Opened the actual PNG with the image viewer: login page with QR code, web-login and refresh links, no player. No visible clipping in this startup capture; no claim about player visuals or accessibility.
- Local artifacts: `%TEMP%/BiliCopilot-issue-48/initial.png`, `initial-tree.json`, `windows.json`, `ui-results.json`, `ui-setup-first.json`, `inspect-owned.ps1`. QR/login artifacts remain local and are not committed. No credential files copied, no authenticated session borrowed, no external account actions performed.
- Player video/PGC switching, reload, close during active loading: **NOT RUN — login required**. Startup screenshot/close does not count as the ticket's player-close acceptance. This is the remaining UI acceptance blocker.
- Closed only the owned startup window using `winapp ui invoke btn-close-cc37 -w 3876138 --json`; invocation succeeded, subsequent owned window list was `[]`. Called `winapp ui yield` with workflow `bili-issue-48`. Other task PID `9608` remained running. Desktop slot released and parent notified.
- Parent can reuse the isolated registered package/layout for a later authenticated acceptance. No existing installed package was uninstalled; no Windows feature was enabled.

## Integration synchronization

- Initial ticket commit: `632f794`.
- Merged then-current local integration `799d3b216b91f731e3805d781c9e1e832134e840` into this implementation branch as `0b51e478d61e382b13c3c4c3ac7a4f3e3bcc2f85`; no text conflicts.
- Integration introduced #50's test-only `ILogger`/`NullLogger` declarations, which collided (CS0436) with real logging abstractions brought in by #48's media-service reference. Removed only those redundant logging declarations from `PopularPageTestAdapters.cs`; existing page tests now use real `NullLogger<T>`. No test behavior, runner, production #50 code or package version changed.
- First postmerge run passed 320 tests but reported CS0436 warnings. After the adapter correction, `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --logger 'trx;LogFileName=issue-48-postmerge-final.trx'` exited 0 with Passed: 320, Failed: 0, Skipped: 0 and no warnings. Parsed TRX counters confirm total/executed/passed 320, failed/notExecuted 0.
- Parent subsequently froze integration at the #47 merge for final synchronization; final fixed-tip evidence follows.
