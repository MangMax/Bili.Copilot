# Issue #53 — Coalesced incremental scroll checks

Date: 2026-10-30. Implementation and headless behavior verification completed; **real WinUI pagination acceptance remains pending**. This document does not authorize closing the issue.

## Scope and checkout

- Ticket: https://github.com/MangMax/Bili.Copilot/issues/53; parent: https://github.com/MangMax/Bili.Copilot/issues/45. Read both bodies/comments (no comments), root AGENTS.md and all docs/agents rules. Claimed #53 with `gh issue edit 53 --repo MangMax/Bili.Copilot --add-assignee '@me'`.
- Existing independent branch `t3/fix-scroll-load-more`, workspace `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-020b4bd7`; initially clean. HEAD and local `feat/performance-45` both matched expected `ddc607451075470eccb334b4ec3ce7a4e898dd26`.
- Checked recent PRs and open PR file lists (#27/#43/#44/#60), plus #60's body/files: no competing change to IncrementVerticalScrollControl. No PR was created or modified.
- Kernel source was already present, clean and initialized at published gitlink `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. No submodule repair or gitlink change.
- Applied the requested tdd, code-testing, run-tests, winui-code-review, winui-ui-testing, winui-dev-workflow and verification-before-completion skills, including TDD test/mocking guidance and TRX validation. No XAML change. No test extension catalog or run-tests overlay was supplied/present; followed existing xUnit/VSTest conventions. SDK 10.0.403; winapp 0.7.1.
- Ran `git merge --no-edit feat/performance-45` before full verification: already up to date. The integration branch itself was not changed.
- Repeated the merge before committing: fast-forwarded to `57dceffb1f20dc9db281755acb593c98265534f8`, which adds #46/#47 evidence and a Markdown lifecycle correction. No overlap/conflict with this ticket; inherited evidence binaries are integration-owned, not new #53 assets. Reran the full suite and Debug x64 build after this production-code change (results below).
- Final local integration merge fast-forwarded to `8cf40300d533ef15acd1c034f6a2630bbb4c88d0` (additional #46/#47 evidence only). `git diff --quiet 57dceffb1f20dc9db281755acb593c98265534f8 HEAD -- src tests externals` exited 0: production/test/kernel inputs identical to the integrated verification. Rechecked whitespace and ticket diff; no redundant rebuild for evidence-only changes.

## Implementation and test boundary

Only production change: `Controls/IncrementVerticalScrollControl.xaml.cs`.

- A per-instance pending flag merges view/size/list-update checks. The low-priority callback reads current extent, viewport, offset and command. Threshold remains inclusive at 800 pixels.
- Unload disables checks and invalidates outstanding 500 ms list-update delays. Load schedules a fresh check. A physically queued callback retains its slot across unload/reload: if reloaded before it runs, it checks the current loaded view once; if still unloaded, it does nothing. Resetting the slot on unload would allow multiple physical callbacks to accumulate.
- The slot is released before command execution so a synchronous layout change can schedule one follow-up. Failed TryEnqueue releases it as well.
- Execute is called only when the current command's CanExecute permits it. No self-rescheduling polling loop; existing page list-updated notifications continue to drive the delayed short-list fill. Page/service end-of-list policy remains owned by the supplied command.
- Internal TimeProvider constructor controls only the existing 500 ms delay in tests; XAML's public parameterless constructor uses TimeProvider.System. Delays are invalidated, not cancelled; their lifetime remains at most the existing delay plus dispatcher scheduling.

The existing test project links the **actual control code-behind**. Ticket-local presentation adapters replace generated XAML fields, Windows events/property storage and dispatcher; no pagination/check logic is copied. Tests stimulate lifecycle/view/size events or public DelayCheckItemsAsync and observe the external LoadMoreCommand with real RelayCommand. Pending queue counts additionally verify the ticket's explicit scheduling bound. A private test-local clock controls timer completion without sleeps. No shared test framework, packages or CI changes.

These adapters do **not** exercise Windows ABI, actual layout/event ordering, real page view-model/network pagination or UI-thread affinity. The page sequence below is a controlled command-boundary scenario, not real-list acceptance.

## Executed tests

Commands (all from this workspace):

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-53-baseline.trx'
# Each red/green iteration used the following command with the corresponding report name:
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~IncrementVerticalScrollControlTests --logger 'trx;LogFileName=issue-53-focused.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --logger 'trx;LogFileName=issue-53-final.trx'
```

| TRX suffix (issue-53-*.trx) | Passed | Failed | Skipped | Exit | Evidence |
|---|---:|---:|---:|---:|---|
| baseline | 342 | 0 | 0 | 0 | Existing suite before production edits |
| red-burst | 0 | 1 | 0 | 1 | Expected 1 pending callback, actual 200 |
| green-burst | 1 | 0 | 0 | 0 | Burst coalesced |
| red-unload | 1 | 1 | 0 | 1 | Expected 0 loads after unload, actual 1 |
| green-unload | 2 | 0 | 0 | 0 | Unload/reload guarded |
| red-command | 2 | 1 | 0 | 1 | Disabled command executed once |
| green-command | 3 | 0 | 0 | 0 | CanExecute respected |
| red-delay | 3 | 1 | 0 | 1 | Old delayed check loaded into new lifecycle |
| green-delay | 4 | 0 | 0 | 0 | Old delayed check rejected |
| focused | 12 | 0 | 0 | 0 | Full ticket behavior matrix |
| final | 354 | 0 | 0 | 0 | Complete existing suite plus ticket tests |
| integrated | 354 | 0 | 0 | 0 | Full suite after merging local integration 57dceff |

All RED results are assertion failures, not build/discovery failures. Baseline emitted the existing protobuf unused-import warning. The first RED emitted an unused adapter constructor parameter warning, corrected immediately; final test run emitted no warnings.

Parsed every report with PowerShell `[xml]` from the ignored `tests/BiliCopilot.UI.Tests/TestResults` directory. Checked total/executed/passed/failed/notExecuted counters and UnitTestResult counts; final total/executed/passed/results = 354, failed/error/notExecuted = 0. Focused report discovers 12 cases (10 methods, including three threshold cases).

| Requirement | Evidence |
|---|---|
| One pending check and one command execution per burst | `BurstQueuesOneCheckAndExecutesLoadMoreOnce` (100 view + 100 size events) |
| Read current geometry; inclusive threshold | `QueuedCheckUsesLatestPositionAndThreshold` (799/800/801 remaining) |
| Unload prevents load; reload works | `UnloadDiscardsQueuedLoadAndReloadChecksCurrentView` |
| Rapid reload preserves one physical callback; handlers detached | `ReloadBeforeQueueDrainsStillHasOneCurrentCheck` |
| Respect CanExecute; retry after enabled | `DisabledCommandIsNotExecutedAndLaterInputCanRetry` |
| Delayed checks reject old lifecycle | `OldDelayedCheckCannotLoadIntoReloadedControl` |
| Delayed bursts merge; no load while unloaded | `DelayedChecksCoalesceAndCannotLoadWhileUnloaded` |
| Short list, rapid scroll, in-flight load, resize, last page idle | `ShortPagesFillViewportThenScrollAndResizeContinueUntilLastPage` (pages exactly 1–5, 499/500 ms delay boundary, zero queued work at end) |
| Enqueue failure recovers; null/replaced command | `RejectedEnqueueCanRetryAndCommandIsReadAtExecution` |
| Layout reentrancy preserves next page without queue growth | `ReentrantLayoutKeepsOneFollowUpCheckWithoutLosingNextPage` |

The controlled burst proves queue reduction from 200 to 1. It does not establish reduced network traffic, actual desktop latency, allocations, or any performance percentage.

Integrated rerun: `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --logger 'trx;LogFileName=issue-53-integrated.trx'`. Parsed TRX counters total/executed/passed 354, failed/error/notExecuted 0; additionally verified all 12 IncrementVerticalScrollControlTests cases were present and passed. Exit 0; no warnings emitted.

## Build and WinUI review

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-020b4bd7/src/ -p:GenerateAppxPackageOnBuild=false
git diff --check
```

Build exit 0: **0 errors, 1 existing protobuf unused-import warning**, elapsed 00:05:15.93. Full output: `C:/Users/hp/AppData/Local/Temp/issue-53-build.log`. `git diff --check` passed. The app was built but never deployed or started.

Integrated build reran the same command with `--no-restore`, after merging the Markdown production correction: exit 0, **0 warnings, 0 errors**, elapsed 00:04:16.41. Full log: `C:/Users/hp/AppData/Local/Temp/issue-53-build-integrated.log`. Repeated the ticket's source/XAML/adapter review after the build; no blocking findings. The final evidence-only integration merge leaves these build inputs unchanged.

Existing runtime assets were copied only to ignored paths in this workspace from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets`: libmpv/x64/libmpv-2.dll (121108480 bytes), ffmpeg/x64/ffmpeg.exe (107860992 bytes). No binary is staged; no environment install.

Post-build static review covers lifecycle/event subscription symmetry, per-instance queue ownership, enqueue rejection, reentrant scheduling, delayed continuation ownership, command contract and current geometry. No blocking finding in this diff. Public XAML construction and existing bindings/styles are retained. No new UI strings, credentials, resource/theme or accessibility structure. WinUIAnalyzer is not referenced by the project, so its checks did not run; no analyzer installation was attempted. Debug build is not Release/Native AOT/trim acceptance.

`winapp find-api members ScrollView --filter Height --json --project-dir src/Desktop/BiliCopilot.UI` confirmed ExtentHeight/ViewportHeight in the restored app API surface. An initial pipe-separated filter returned no matches (the filter is not a regex); corrected to Height. Tool caveats: target SDK 22621 is not installed, using SDK 28000 metadata; installed Runtime 2.6 differs from referenced App SDK 1.8.39 and was excluded. This lookup is not runtime validation.

## Unverified acceptance / desktop handoff

The desktop is exclusively reserved for #46/#47. No slot was requested or granted here: **no deployment, app launch, UI input, screenshots or real-list acceptance**. PID 9608, existing packages and credentials were not touched.

After coordinating a desktop slot, verify this exact build with actual list view models/network behavior:

1. Short/empty first pages continue until viewport coverage or actual end-of-list; confirm page sequence and no missed page while a command is busy.
2. Rapid scrolling towards and away from bottom uses current position; resize width/height and display scale while loading, including layout changes after the 500 ms delay.
3. Navigate away with a callback/delay pending, return rapidly and return after the callback drains. No off-page load; reloaded list continues normally.
4. At the final page and under disabled/error commands, observe an idle period with no ongoing requests/check loop. Confirm existing page end-of-list policy does not keep emitting list-updated notifications for no-op loads.
5. Record real requests, queue behavior and visible list contents for the same build/material. Real WinUI lifecycle, ABI, layout, DPI, theme/accessibility and Release performance remain unverified.

No push, PR modification, issue closure, main integration branch mutation or subagent creation was performed.
