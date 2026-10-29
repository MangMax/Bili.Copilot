# Integration review — #53

2026-10-30. Integration/build verification only; **real WinUI acceptance remains unverified, and end-of-list acceptance is not signed off**.

## Provenance and preserved changes

- Parent checkout only: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`, initially clean at `4984ac5f710b3e3040fe663b3d0447888d989736`.
- Read root AGENTS.md, docs/agents rules, GitHub #53/#45 bodies and comments, issue-53.md, and winui-code-review, winui-ui-testing, run-tests, verification-before-completion skills. No repository test overlay found. SDK 10.0.403; existing xUnit v2/VSTest runner.
- Executed `git merge --no-ff d469d14d56c9ababa4d3751e4171db92c8596397 -m 'Merge scroll load-more coalescing (#53) into performance integration'`.
- Merge `ae16b95e8b6ac7066371a7b48474873203b64a16` has exactly the initial HEAD and specified source commit as parents; no conflict. Only the five #53 files differ from initial HEAD. #52, SVG charset fix, Markdown remount correction and all prior evidence remain unchanged.
- Kernel gitlink and checkout remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. No other checkout operations, deployment, app launch, desktop input, push, PR, issue write/closure, or delegation. Desktop remains exclusively owned by #57.

## Review and fidelity

The production change maintains a per-control physical pending callback slot, released before command execution and on enqueue rejection. Geometry and command are read at execution; threshold remains inclusive at 800. While unloaded it cannot execute. A retained callback may serve the current loaded view after rapid reload, preserving the single physical slot; old 500 ms delays are rejected by load version. Event subscriptions are detached on unload. Current CanExecute is respected. No new self-scheduling callback loop was introduced.

The 12 scroll cases link actual control code-behind, but replace its base class, dependency properties, ScrollView, generated XAML and dispatcher with adapters. They are stronger than tests of a copied boolean/session helper, but are **headless command-boundary tests, not actual WinUI control tests**. The manual clock and RelayCommand simulate pages 1–5, busy state, resize and terminal CanExecute=false. This verifies the controlled sequence and idle fake queue; it does not establish that production page commands expose terminal state or that Windows event ordering/UI-thread affinity matches the adapters. Multiple delay timers remain possible; coalescing bounds queued checks, not timer allocations.

Reviewed burst coalescing, execution-time 799/800/801 position, rejected enqueue recovery, null/replaced command, unload/reload, old-delay generation rejection, disabled command, reentrant layout, and short-page continuation. No blocking defect identified in the changed control itself. XAML/bindings/theme/accessibility structure are unchanged. WinUIAnalyzer is not referenced and its specialized checks did not run; existing build analyzers remain enabled.

## Finding for parent — P2 / acceptance blocker, existing page behavior

`src/Desktop/BiliCopilot.UI/ViewModels/View/PopularPageViewModel/PopularPageViewModel.cs:161` only requests a partition ranking when `Videos.Count == 0`. With a populated `PopularRankPartitionViewModel`, subsequent LoadVideosCommand calls skip all request branches but still emit `VideoListUpdated` at line 171 and queue clearing IsVideoLoading. The early terminal return at line 139 covers the global Rank enum, not partition ranking.

`Controls/Popular/PopularMainBody.xaml:35` binds this command; `PopularMainBody.xaml.cs:27` routes each update to DelayCheckItemsAsync. While within 800 px of bottom, the update schedules another check after 500 ms, the completed async command is executable again, and another no-op update can follow. This is a source-demonstrated recurring work path, not a measured desktop reproduction or evidence of duplicate network requests. These page files are unchanged from 4984ac5, so this is not attributed as a new merge regression. It prevents asserting the ticket's broad “last page without continuous idle work” criterion. Parent should address terminal/no-change notification policy in the page command and verify the real boundary; no expansion/fix was made here. The test's artificial terminal CanExecute=false hides this production policy difference.

## Fresh validation on merged sources

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-53-release.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/
```

Release suite exit 0: **Passed: 378, Failed: 0, Skipped: 0**. Independently parsed XML: total/executed/passed/results=378; failed/error/timeout/aborted/notExecuted=0. All 12 scroll cases and SvgResponsePreservesDeclaredCharset passed. No test warnings emitted. Reports are retained in ignored `tests/BiliCopilot.UI.Tests/TestResults/integration-53-release.trx` and `integration-53-release.log`.

Explicit ValidateRuntimeDependencies exit 0 (`integration-53-runtime-gate.log` in the same directory), checking existing x64 libmpv/ffmpeg/BBDown file presence. No gate bypass, runtime installation or asset copying. This is a build-time asset gate, not runtime execution.

Debug x64 build exit 0: **0 warnings, 0 errors**, elapsed 00:02:05.02. Full log: `tests/BiliCopilot.UI.Tests/TestResults/integration-53-debug-build.log`. This compiles the actual WinUI C#/XAML without deployment or activation. `git diff --check` and preservation checks passed. The final follow-up commit changes this report only; tested production inputs remain the merge above.

## Real UI work remaining

After #57 releases the desktop, exercise actual short-list and terminal page commands, busy completion, rapid scroll towards/away from bottom, width/height/DPI resize, unload with pending callbacks/delays, rapid reattach and return after drain. Observe real request/page sequence and idle periods, particularly populated partition rankings, empty/error/terminal responses and layouts settling after 500 ms. Capture UI outcomes and inspect screenshots. No Windows ABI, real layout, UI-thread scheduling, network pagination, actual idle CPU, memory/allocation benefit, Release/AOT publication, accessibility/theme matrix or performance percentage is established by this run. Prior Markdown evidence is preserved but does not validate #53.
