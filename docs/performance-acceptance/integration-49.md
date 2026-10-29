# Performance #45: integration of #49

Date: 2026-10-30. Integration verification only; real Win2D/DirectX and performance acceptance remain **pending**.

## Provenance and scope

- Read AGENTS.md, all docs/agents rules, GitHub issue #49, incoming issue-49.md, implement-spec merger instructions, winui-code-review, winui-ui-testing, run-tests and verification-before-completion. No test overlay exists. SDK 10.0.403; existing net9.0 xUnit 2/VSTest project.
- Worked only in `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`. Starting HEAD was exactly `e6f425509a728a4086eefbe29e7273d2851a03f0`; working tree and nested dependency were clean.
- Verified `t3/renderer-readiness-redraw-coalescing` resolves to requested `ba1e76adc6142ada0ad170ba7ace480fbbf733ca` and contains the starting integration HEAD. Read incoming evidence through `git show` before merging; did not access the child worktree.
- Executed `git merge --no-ff ba1e76adc6142ada0ad170ba7ace480fbbf733ca -m 'Merge ticket #49 renderer redraw into feat/performance-45'`.
- Merge commit: `60a93e1d40cf3832eab4b388060b9fa38747ba4b`, with exactly the starting and requested heads as parents. No conflicts or corrections; merged tree equals incoming tree. Delta is limited to the panel, redraw session, position getter, four session tests and issue-49.md.
- Ancestor checks passed for #46 `7d598c154271db8f1ff7f94ad232c6c80fd00197`, #50 `6ed6d46c0c0c1574731927c7789e31ed315e5a01`, #47 `175b2c07a67a8654d1046112fde0ae095db45913`, #48 `2eac5fac1554e0081bbe93bf0b47eb95d219ead7`, and #51 `ed84c6a76b9d18fa8ad44b2f21ddd71fbc8a2cfa`.
- Kernel gitlink and nested HEAD remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. Read-only `git -C externals/bili-kernel ls-remote --heads origin refs/heads/fix/copilot-48-danmaku-cancellation` returned that exact published SHA.

## Focused review and coverage boundaries

No substantive blocker established by static review. No production/test corrections made.

- Both renderer selections use the same low-priority dispatcher batch. The batch reads the current Renderer, releases the inactive instance if needed, creates only the selected instance and redraws. No fixed 500 ms delay or requirement for both mutually exclusive fields to exist remains. Low-priority scheduling is not a hardware latency guarantee, and constructor completion is not proof of native GPU readiness.
- Repeated requests replace the pending action without queuing extra work in the same generation. Pending is cleared before invocation, preserving a reentrant next-frame request. Rejected enqueue releases the slot. This seam assumes serialized UI-thread use; it is not a thread-safe/global scheduler.
- Clear, ViewModel replacement and unload invalidate the generation. An old queued callback returns before consuming new pending work. Renderer selection changes coalesce into the current batch; they do not capture a renderer instance. Unload invalidates before detaching events and releasing renderers; both fields are nulled and visuals removed. Load reattaches through the same detach/attach helper and requests redraw. Nine event subscriptions have symmetric detach/attach paths.
- Redraw reads cached content, Position, style, speed, pause and visibility at execution. Empty content clears the selected renderer. Position is updated by the view model even while the panel is detached. Redraw explicitly pauses paused/hidden content and resumes only visible, playing content. Existing direct style/speed handlers and XAML visibility binding remain. This supports state restoration by inspection, not proof of rendered output.
- Four new tests compile the actual pure session source via the existing wildcard: latest callback replacement; rejected enqueue/reentrant tail; generation invalidation across replacement/unload/reload; and 101 requests coalescing to one callback that reads the latest state tuple.
- These tests use queued delegates and strings/tuples, not the VideoDanmakuPanel, DanmakuRenderViewModel, actual subscriptions, native renderer readiness/disposal, or GPU output. The simulated state tuple does not test font/style application, seek, pause animation or actual content parsing. The test named for a ready renderer proves immediate enqueue only. Actual control clear/ViewModel replacement paths are inspected but not executed by these tests.
- No XAML, accessibility, theming or localization changes. Dedicated WinUIAnalyzer is absent and its checks did not run; existing build analyzers remain enabled. No Release app/Native AOT runtime claim.

## Fresh verification

Commands executed from the integration checkout, using existing restored dependencies and assets without installations:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-49.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -v:normal
```

- Full Release suite exited 0: **Passed: 342, Failed: 0, Skipped: 0**. Parsed TRX confirms total/executed/passed 342; failed, error, timeout, aborted, notExecuted, notRunnable and pending are all 0. All four DanmakuRedrawSessionTests outcomes are Passed.
- One existing protobuf warning during test compilation: `pgc/gateway/vega/v1.proto(10,1)` unused `google/protobuf/empty.proto` import.
- Local ignored test evidence: `tests/BiliCopilot.UI.Tests/TestResults/integration-49.trx`. Build log: `tests/BiliCopilot.UI.Tests/TestResults/integration-49-build.log`.
- Debug x64 build exited 0: **0 warnings, 0 errors**, elapsed **00:01:46.70**. C#/XAML output: `src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll`.
- Explicit `ValidateRuntimeDependencies` gate exited 0: **0 warnings, 0 errors**, elapsed **00:00:01.09**. Log: `tests/BiliCopilot.UI.Tests/TestResults/integration-49-runtime-gate.log`. This target checks platform and existence of three runtime assets; it does not load native DLLs or launch the app. It was invoked separately because this build disables packaging.
- Whitespace checks passed. Only this evidence document was added after verification; no source, test, project or runtime-asset changes followed the tested merge.
- Existing assets reused in place: libmpv-2.dll 121108480 bytes, ffmpeg.exe 107860992 bytes, BBDown.exe 21521408 bytes. No download, installation or asset mutation.

## Pending real acceptance

Desktop exclusivity belongs to #46/#47. No deployment, app launch, input, UIA inspection or screenshots were performed. **UI NOT RUN / NOT PASSED**.

Validate each engine independently on the real control: startup/readiness, rapid redraw and renderer switching, playing/paused seek, style/content correctness, hide/show, clear/reinitialize, ViewModel replacement, unload with queued work and reload. Check no disposed-instance callbacks, duplicate overlays, incorrect position or resumed paused animation. Capture structured UI outcomes and screenshots for visual review when the desktop slot is granted.

Before/after redraw latency, CPU/allocation/GPU/frame-time improvements and Release/AOT runtime acceptance remain pending. Deterministic batching evidence is not a measured application performance gain. Prior #46/#47/#48/#50/#51 acceptance gaps remain unchanged. No delegates, other-worktree operations, unrelated-process operations, push, PR changes or issue closure.
