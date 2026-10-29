# Integration review — #55

2026-10-30. Code integration and controlled process verification; **real WinUI acceptance remains unverified**.

## Provenance and preservation

- Only checkout: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`. Initial HEAD `07d6efeae9b2f1e624ff652aafe32f50e46fc521` was clean.
- Read AGENTS.md, docs/agents rules, GitHub #55/#45, issue-55.md, winui-code-review, winui-ui-testing, run-tests and verification-before-completion. No repository test overlay found. SDK 10.0.403; existing xUnit v2/VSTest runner, net9.0 with LatestMajor roll-forward.
- Executed `git merge --no-ff 344b00a997ffbcd453412c575178e8ffe6865238 -m 'Merge asynchronous download process waiting (#55) into performance integration'`.
- Merge `20ccfa953eb2dc24551d1112eee986772d7ed991` has exactly the initial HEAD and requested source as parents. No conflicts. Relative to the initial HEAD, only issue-55.md, DownloadProcessRunner.cs, DownloadViewModel.cs and DownloadProcessRunnerTests.cs changed. All #46–54 code/evidence, charset, ranking notification and Markdown remount fixes/evidence remain unchanged.
- Kernel gitlink and checkout remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`, clean. No other worktree, subagent, push, PR or issue mutation. Desktop belongs exclusively to #57: no deployment, application activation, desktop input, real download or authentication.

## Review outcome

No material blocking defect identified in this change. The runner owns the supplied unstarted Process in a using scope covering Start, WaitForExitAsync and ExitCode access. Both normal completion and exceptions dispose it; the directory callback runs after disposal, only for exit code zero and an enabled setting. A directory callback exception cannot leak the download process handle. The ViewModel awaits inside the existing background Task.Run and catches failures, logging only exception type or nonzero exit code, without command/credential text or a download-success notification.

ProcessStartInfo(fileName, command) and its defaults are preserved. External mode still selects `BBDown`; bundled mode still selects `_bbdownPath`. The external-plus-copy-only branch still copies and returns before scheduling any process. Command construction, credential policy, clipboard notification and download paths are unchanged by the diff. The copy-success notification is not a download-success notification.

With auto-open disabled the runner now also awaits completion to release resources deterministically. That await does not keep a worker synchronously blocked. No cancellation token, Kill, job object or main-window lifecycle hook was added. Main-window/application shutdown code is unchanged; disposing a Process wrapper does not itself terminate its child. These are static compatibility conclusions, not an executed close-window or parent-process-exit test. A fully exited parent cannot subsequently run the directory callback, as before.

No XAML, binding, theming or accessibility changes. Dedicated Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer is not referenced and was not installed/run; existing build analyzers were retained. No expansion into unrelated fixes.

## Test fidelity and resource evidence

All seven DownloadProcessRunner cases passed in the complete suite. Tests link the real production runner and use windowless `cmd.exe /d /q /c "set /p gate= & exit /b N"`, with redirected stdin controlling completion. No network, credentials, Explorer launch or real BBDown. They cover no callback before exit, success callback once, disabled opening, exit code 7, missing-executable startup failure, callback failure and captured SafeHandle closure. The outer test using/finally cleanup occurs after the closure assertions, so those assertions exercise runner disposal rather than test cleanup. Fifteen seconds is a failure timeout, not a performance target. The author's historical red/green runs were read, not repeated here.

The resource case warmed one process then completed three batches of ten. Every captured handle in those 30 operations was closed on completion. Fresh raw output from integration-55-release.trx:

```text
waiting-1: handles=639, OS threads=34, OS waiting=33, pool busy=2, pool threads=16
warm: handles=554, OS threads=34, OS waiting=33, pool busy=2, pool threads=16
waiting-10: handles=584, OS threads=34, OS waiting=33, pool busy=2, pool threads=16
completed-10: handles=574, OS threads=33, OS waiting=31, pool busy=3, pool threads=16
waiting-10: handles=604, OS threads=33, OS waiting=31, pool busy=3, pool threads=16
completed-20: handles=594, OS threads=33, OS waiting=31, pool busy=3, pool threads=16
waiting-10: handles=624, OS threads=33, OS waiting=32, pool busy=3, pool threads=16
completed-30: handles=614, OS threads=33, OS waiting=31, pool busy=3, pool threads=16
```

Thirty operations suffice for the limited repeated-handle-closure observation, not a leak or throughput verdict. Total host handles increased from 554 to 614; this run does not establish zero total growth, its cause or long-term stability. The full-suite test host includes runtime infrastructure and other tests. OS waiting counts all waiting host threads; pool busy is an instantaneous available/max difference, not stack attribution. Columns are non-atomic snapshots, with no forced GC, ETW stacks, sustained baseline or before/after matched workload. The evidence does not measure application-wide speed, UI responsiveness, real download throughput, memory improvement or a percentage benefit. The removed synchronous wait previously blocked a background pool worker, not directly the UI thread.

## Fresh validation

Executed on the merge above:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-55-release.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/
```

Release suite exit 0: **Passed: 393, Failed: 0, Skipped: 0**. Independently parsed TRX: total/executed/passed/result elements all 393; failed/error/timeout/aborted/notExecuted all zero, every result Passed. Seven download cases, six PresenterChangeSession cases, both populated-ranking notification cases and SvgResponsePreservesDeclaredCharset were present and passed. No test warnings emitted.

ValidateRuntimeDependencies exit 0: existing x64 libmpv, ffmpeg and BBDown file presence only, not loading/execution or playback. No bypass, installation or asset copying.

Debug x64 build exit 0: **0 warnings, 0 errors**, elapsed 00:02:00.40. This compiles actual WinUI C#/XAML without deployment or activation. `git diff --check`, merge-parent/file preservation and kernel cleanliness checks passed.

Raw ignored evidence is under `tests/BiliCopilot.UI.Tests/TestResults/`: integration-55-release.trx, integration-55-release.log, integration-55-debug-build.log and integration-55-runtime-gate.log. The follow-up commit adds only this report; tested production inputs are the merge above.

## Unverified acceptance

UI **not tested**. No WinUI/UIA or visual acceptance, actual Explorer opening, external BBDown PATH resolution, clipboard action, real command invocation, main-window closure with an active child, process exit with an active child, long-running download/resource behavior, Release publication or Native AOT validation. The suite exercises the production runner but not DownloadViewModel's Windows service wiring, actual BBDown/Explorer behavior or its exception logging. These require a separate authorized desktop session after #57 releases ownership. Prior Markdown UI evidence remains preserved and is not download acceptance.
