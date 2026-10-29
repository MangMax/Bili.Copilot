# Issue #49 — active renderer redraw and coalescing

## Scope and provenance

- Scope M, difficulty high: one panel's redraw/lifetime boundary under #45. No cancellation, HTTP, shared kernel, prefetch, rendering-engine redesign or shared test infrastructure changes.
- Read full #49/#45 bodies and comments (both empty), AGENTS.md and domain/issue-tracker guidance; claimed #49 with `gh issue edit 49 -R MangMax/Bili.Copilot --add-assignee '@me'` before repository writes.
- Prepared isolated branch `t3/renderer-readiness-redraw-coalescing`, worktree `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-69d36ba6`. Initial HEAD and integration tip both `175b2c07a67a8654d1046112fde0ae095db45913`; initial working tree clean.
- Existing submodule was initialized and clean at `6501985d0f30c55c28b3f78c6e045c2536e1d389`; its Git metadata is worktree-specific. No reset, shared-checkout mutation, delegates, push, PR or issue closure.
- Open PR titles/branches inspected (#27/#43/#44/#60); #60 is the ongoing parent integration. #48 cancellation work is incorporated only through integration, never duplicated here.

## Implementation

Removed the unconditional-in-single-engine-mode 500 ms delay. One UI-dispatcher batch resolves the latest selected renderer, creates/replaces it if necessary and draws once. An already-created active renderer goes directly through that batch without waiting for the inactive field to exist. Renderer switches use the same batch, so transient selections need not instantiate intermediate renderers.

`DanmakuRedrawSession` is a single panel-scoped, UI-thread-only scheduling boundary. Requests replace the pending callback; an invalidation generation prevents queued callbacks from a cleared/unloaded/replaced ViewModel session from consuming the new session's work. Rejected dispatcher enqueue releases the pending slot. No timer, background scheduler or global framework was introduced.

Redraw reads current cached content, playback position, style, speed and pause/visibility state when it executes. Empty content clears the renderer. A read-only `Position` property allows reload to use position updates received while the control was unloaded. New renderers are explicitly paused when playback is paused or the overlay is hidden. Existing direct style updates remain direct, avoiding a new full-content reset for style-only notifications.

Unload invalidates queued work, detaches subscriptions, closes/disposes renderers, nulls both fields and removes old visuals. Load reattaches subscriptions without duplicates and requests a fresh redraw. Renderer replacement and render calls run synchronously in the same UI callback and never capture a released renderer.

## Skill audit

- **scope-gate:** M boundary retained; one focused implementation handoff, no PR requested.
- **tdd:** used the single redraw-session seam already approved by #45/#49. Red/green cycles below; no new seam approval needed.
- **code-testing:** focused tests for one session class, existing pure-source wildcard in existing xUnit project; no test project/package edits. No code-testing-extensions catalog available. Report-safe test names and TRX parsing used.
- **run-tests:** repository overlay absent; SDK 10.0.403 with existing net9.0 xUnit 2/VSTest project, positional project path and VSTest filter syntax.
- **winui-dev-workflow:** existing packaged project retained; WinApp CLI 0.7.1 present. No installs or analyzer additions under the explicit no-installs constraint. Dedicated `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer` is absent, so its checks did not run; existing analyzers remain enabled.
- **winui-code-review:** manual review of changed C#, existing XAML, UI-thread ownership, callback invalidation, subscriptions, disposal and state restoration. No XAML or binding changes; **winui-design** was not needed because no XAML was written/changed.
- **winui-ui-testing:** read and applied execution boundary. Desktop reserved by parent for #46/#47; no deployment, launch, input, screenshots or UIA tests. UI evidence remains explicitly unverified pending a slot grant.
- **verification-before-completion:** results and limitations recorded separately below.
- **msbuild:** consulted during the long build; it completed successfully without changing build settings, cleaning outputs or stopping shared build servers. No build-speed improvement claimed.

## Deterministic behavior tests

Commands use the existing test project:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~DanmakuRedrawSessionTests --logger 'trx;LogFileName=issue49-red-burst.trx' --results-directory $env:TEMP/issue49-tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~DanmakuRedrawSessionTests --logger 'trx;LogFileName=issue49-red-lifecycle.trx' --results-directory $env:TEMP/issue49-tests
```

1. First red used an uncoalesced session implementation: `ReadyRendererQueuesImmediatelyAndBurstDrawsOnlyLatestState` failed because 101 callbacks were queued rather than one. Minimal batching made the test pass (1 passed).
2. Second red cleared the pending action without a generation: `SwitchAndUnloadRejectOldCallbacksEvenAfterReplacementIsQueued` failed because an old callback consumed the replacement frame. Generation invalidation made it pass; subsequent targeted run passed 4 tests.

| Requirement | Evidence |
| --- | --- |
| Immediate scheduling, burst merging and latest state at execution | `ReadyRendererQueuesImmediatelyAndBurstDrawsOnlyLatestState` |
| Pending selection replaced by latest renderer callback | `ReplacingPendingRedrawUsesOnlyTheLatestRendererCallback` |
| Old callbacks cannot consume replacement/unloaded session work; reload works | `SwitchAndUnloadRejectOldCallbacksEvenAfterReplacementIsQueued` |
| Rejected enqueue is retryable and reentrant tail is retained | `RejectedDispatchCanBeRetriedAndRedrawCanRequestAnotherFrame` |

The burst test measures 101 requests producing one queued callback and one publication. The old red implementation is a scheduling model, **not** an original-app performance baseline. Its state tuple proves latest-state callback timing, not GPU output, actual font styling or playback behavior. No fixed sleeps, network, reflection into private fields or source-text assertions. No measured app latency, allocation, GPU or frame-time improvement claimed.

## Build and final integration validation

Implementation commit: `bdb6d37a599806e475be7912b67cbf883d9fab49`.

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -p:SolutionDir=$PWD/src/ -p:Configuration=Debug -v:minimal
git merge --no-edit feat/performance-45
git submodule update --init --recursive
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=issue49-final.trx' --results-directory $env:TEMP/issue49-tests
```

Initial Debug x64 build: exit 0, 0 errors, 1 existing protobuf warning (`pgc/gateway/vega/v1.proto`: unused `google/protobuf/empty.proto` import), elapsed 9:31.08 with other independent builds active. C#/XAML compiled successfully. Log: `C:/Users/hp/AppData/Local/Temp/issue49-build.log`.

Latest integration at merge time: `eeb28f2c914a2a86491781a3f2942b860db688ee` (includes #48 and parent acceptance docs). Merged without conflicts into this branch as `91e290c8dfa2fbe9aaf215704da837a6adc92512`. Verified `git merge-base --is-ancestor feat/performance-45 HEAD` exit 0. Before the nested update, checked its porcelain status was empty; updated only this worktree's submodule to the inherited gitlink `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. No integration-branch movement by this agent and no kernel edits.

Pre-merge final focused run with `--no-restore --filter FullyQualifiedName~DanmakuRedrawSessionTests --logger 'trx;LogFileName=issue49-green.trx' --results-directory $env:TEMP/issue49-tests`: exit 0, **Passed: 4, Failed: 0, Skipped: 0**. PowerShell XML parsing confirmed 4 executed/4 passed, no errors, aborted or incomplete results.

After the #48 integration merge, full Release suite: exit 0, **Passed: 324, Failed: 0, Skipped: 0**. Parsed `issue49-final.trx`: all 324 executed/passed including all four redraw-session tests, no errors or incomplete results. Same-command Debug x64 build: exit 0, **0 warnings, 0 errors**, elapsed 4:38.21; log `C:/Users/hp/AppData/Local/Temp/issue49-build-final.log`.

During that verification, parent integration advanced again to `fff7cb5cb5ad4b30f7500d3b5c0f90c0b4fcf276` (#51). Merged without conflicts into this isolated branch as `1a6b0d8c1570eb3acda1b92d7a37cd52aad44a8a`, retaining the same clean kernel gitlink. Repeated the full Release test command with `LogFileName=issue49-latest.trx`: exit 0, **Passed: 342, Failed: 0, Skipped: 0**. XML parsing confirmed 342 executed/passed and all four redraw tests passed, no incomplete outcomes.

Latest-integration same-command Debug x64 build: exit 0, **0 warnings, 0 errors**, elapsed 58.56 seconds. Log: `C:/Users/hp/AppData/Local/Temp/issue49-build-latest.log`. This validates C#/XAML compilation and the repository's build/runtime-asset checks, not deployment, app launch or Native AOT publication. `git diff --check` passed; this ticket's diff against latest integration remains limited to the panel, redraw session, read-only position property, session tests and this report.

At handoff, integration added documentation-only commit `e6f425509a728a4086eefbe29e7273d2851a03f0` (`integration-51.md`). Merged as `7ec252e99047e8b01ae591cbe8c84b4ac0950a12`; verified the merge adds only that document and no source/project/dependency changes, so the above build/test evidence still covers identical code. Final ancestry, whitespace and clean nested-dependency checks passed.

Final manual WinUI review: no unresolved error found in the scoped source changes. Checked both mutually exclusive renderer branches, exact event detach/reattach symmetry, invalidation before unload/clear/ViewModel replacement, no captured renderer instances, current position/style/content reads at execution, explicit paused/hidden state, and no async-void redraw or fixed delay. This is static review; native engine initialization/rendering and visual state remain unverified below.

Local ignored assets reused from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets/`, only when absent in this worktree; no installation/download or project configuration changes:

- `libmpv/x64/libmpv-2.dll`: SHA256 `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A`.
- `ffmpeg/x64/ffmpeg.exe`: SHA256 `A1BCBA486CCE341EB033522F0625BB356AE17B1D124A2BC59CFA22540DB81D4B`.
- `bbdown/x64/BBDown.exe`: SHA256 `8C94F19AB3992AA1FA2E785B88FF692875D10030BB2190FD6BDF33212A412442`.

## Unverified acceptance / desktop handoff

**UI NOT RUN / NOT PASSED.** After explicit desktop slot grant, validate each engine independently with the same video and saved position: initial load, repeated redraw/settings changes while playing and paused, seek, rapid Win2D ↔ DirectX switching, hide/show, clear/reinitialize, unload with queued work and reload the same control. Verify visible text/style/density, current timeline, no paused animation, no duplicate overlays and no use-after-dispose exceptions. Capture structured UI results and actual screenshots for visual review. Current tests do not instantiate the WinUI control or third-party renderers.

Real same-build before/after redraw latency/publication measurements and release/AOT runtime acceptance remain outstanding. The source removes a fixed delay but does not establish a frame-time or responsiveness improvement on hardware.
