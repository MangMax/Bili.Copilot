# Integration review — #54

2026-10-30. Code integration and build/test verification; **real WinUI acceptance remains unverified**.

## Provenance and preservation

- Only checkout: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`. Initial clean HEAD: `7728b5b399905b09f2fadd2f47949e6e844e6eb1`, including the parent's populated partition ranking notification correction after `e6c12d7`.
- Read AGENTS.md, docs/agents rules, GitHub #54/#45, issue-54.md, and winui-code-review, winui-ui-testing, run-tests, verification-before-completion skills. No repository test overlay found. SDK 10.0.403; existing xUnit v2/VSTest runner.
- Executed `git merge --no-ff 39ea480eafd1b8ef90d49ff416e1d774f35ad63c -m 'Merge asynchronous player presenter settling (#54) into performance integration'`.
- Merge `8530be8ee93db839bd3ccff32f11564744a190c7` has exactly the initial HEAD and requested source as parents. No conflicts; only the four #54 files differ from the initial HEAD. All prior integration, #52 charset correction, #53 scroll and ranking notification fixes, Markdown diagnostics and evidence are preserved unchanged.
- Kernel gitlink and checkout remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`, clean. No other workspace, deployment, application launch, desktop input, push, PR creation/update, issue mutation/closure or delegation. Desktop remains exclusively assigned to #57.

## Review outcome and limits

No must-fix defect identified in the merged change. All four entry/exit paths check closed/disposed state before reading the presenter, preserve their original mode conditions and flags, and synchronously call SetPresenter through ChangeAsync. Entering full-screen/compact-overlay retains the original ordinary-window topmost clearing; exits restore Overlapped. The original `Task.Delay(500).Wait()` ran inside Task.Run and blocked a thread-pool worker, **not the UI thread**. The replacement asynchronously waits and retains the caller's synchronization context; it assumes the existing UI-thread calling contract and is not a thread-safe cross-thread dispatcher.

Each change owns a generation. Only its current generation may clear IsChanging; Close increments the generation, clears state, and permanently rejects further apply callbacks. Dispose and Destroying close the session. Delayed settling references only the session/generation, never a native window callback. The bounded 500 ms timer is not cancelled on close. Title-bar callbacks check lifecycle at dequeue and after both 800 ms awaits; activation and queued focus callbacks also reject disposed/closed windows. Dispose retains the guard against destroying an already closed native window. These are source-level lifecycle conclusions for the changed paths, not proof that every pre-existing window API tolerates arbitrary calls after destruction.

Presenter callback exceptions propagate synchronously and clear only their own current generation; they do not clear a newer reentrant operation. No new exception swallowing was introduced. The fixed production delay is a non-cancellable Task.Delay(500); injected delay fault/cancellation is not covered by the six tests and would fault the returned task rather than settle IsChanging. Production callers discard that task; no present production faulting-delay path was identified. Actual native SetPresenter failure, window teardown timing and Windows synchronization-context behavior remain untested.

The six tests link the actual PresenterChangeSession production source and control delay completion with TaskCompletionSource. They cover immediate application while changing, pending asynchronous return, old/new/third-operation ordering, close during a pending operation, synchronous close from the apply callback, immediate completion and synchronous presenter failure. Mode strings are test callback outputs: neither PlayerWindow nor a real AppWindow/presenter is exercised. These are pure session behavior tests, not WinUI/UIA or visible-mode acceptance. This integration run did not repeat the author's reported red/green experiments; those remain historical evidence in issue-54.md.

PR #43 was read only at head `5c1b52bb365c8888018969c2380623c9c7409bb5`. Its PlayerWindow patch adds the same `!IsClosed` guard around Destroy in DisposeAsync. The existing Close/DisposeAsync surface remains available. This supports a limited static compatibility conclusion; PR #43 was not merged, and no combined build or tray hide/restore/exit interaction was tested. No XAML, binding, localization, accessibility or theme changes. Dedicated WinUIAnalyzer checks were not run; existing build analyzers were not disabled.

## Fresh validation

Commands executed on the merge above:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-54-release.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/
```

Release suite exit 0: **Passed: 386, Failed: 0, Skipped: 0**. XML parsed independently: total/executed/passed/results=386; failed/error/timeout/aborted/notExecuted=0, every UnitTestResult Passed. All six PresenterChangeSession tests, both populated ranking notification cases and SvgResponsePreservesDeclaredCharset were present and passed. No test warnings emitted.

ValidateRuntimeDependencies exit 0. This checks existing x64 libmpv/ffmpeg/BBDown file presence, not DLL loadability or playback. No bypass, installation or runtime asset copying.

Debug x64 build exit 0: **0 warnings, 0 errors**, elapsed 00:01:22.05. This compiles the actual WinUI C#/XAML without deployment or activation. Raw TRX and logs remain ignored under `tests/BiliCopilot.UI.Tests/TestResults/`: `integration-54-release.trx`, `integration-54-release.log`, `integration-54-debug-build.log`, `integration-54-runtime-gate.log`. `git diff --check`, merge-parent/file preservation checks and kernel cleanliness checks passed. The follow-up commit changes this report only; tested production inputs remain the merge above.

## Remaining real UI acceptance

After desktop ownership is released, verify rapid full-screen/compact-overlay/normal switching and the final visible mode; restoration, position/size and topmost behavior; closure during 500 ms settling, title-bar delays and queued focus/activation; independent/integrated player cleanup; eventual PR #43 combined tray lifecycle. Capture and inspect real UI results. Pure tests and Debug compilation do not establish native ABI/event ordering, actual exception handling during teardown, Release publication or Native AOT behavior. No same-build performance sampling, CPU/thread-pool/memory benefit or percentage improvement is claimed. Existing Markdown evidence does not validate this window change.
