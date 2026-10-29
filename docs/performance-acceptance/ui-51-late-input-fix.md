# #51 late-input guard — independent limited PASS

2026-10-30. Fixed production commit **7516070bd8b88be1bc024515db92ee3931ed6a43**, parent **244226f0ee265685103a5f32a95325444c5a3d24**, branch `feat/performance-45`. The delayed UserInput-after-Unloaded blocker is no longer reproduced under its required ordering. **This is bounded offline functional acceptance, not full #51 closure.**

## Scope and provenance

Worked only in `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`; clean starting checkout. Read AGENTS.md, issue-tracker/triage/domain rules and winui-code-review, winui-ui-testing, run-tests, verification-before-completion, systematic-debugging; used code-testing for the focused harness extension. No test overlay or code-testing-extensions catalog was available. No subagents, production edits, pushes, PR/issue writes, other worktree changes, Dev/production launches, PID9608 interaction, credential access, installs or policy changes.

The actual production AppSearchBox XAML/code-behind, SearchBoxViewModel, item VM, converter/selector and real Richasy.WinUIKernel.Share base run through existing harness-51. Only adapters at DI/settings/localization/navigation/service boundaries remain fixtures. No real API, authentication or destination pages. [Source hashes](evidence-51-late-input/source-hashes.json), [host snapshot](evidence-51-late-input/App.xaml.cs.snapshot), [identity](evidence-51-late-input/Package.appxmanifest.snapshot), per-run runner snapshots and raw events identify what ran. SDK 10.0.403; winapp 0.7.1; WinAppSDK 1.8.251106002; Debug x64 JIT. Harness build: four existing MVVMTK0045 AOT warnings, zero errors. Dedicated WinUIAnalyzer and Native AOT runtime were not run.

New host observation: after arming, observe the real bound `Keyword=q` PropertyChanged, then enqueue **one** high-priority dispatcher detach. Observe real AutoSuggestBox TextChanged reason and `control.IsLoaded`. No direct private handler call, synthetic TextChanged, or copied production search logic. The passive subscription remains across remounts; the arm is one-shot and not armed in other runs. The added test command clips the fixture toolbar's right edge; this is recorded below, not a production layout change.

Production guard review: `OnKeywordChanged` returns before scheduling work when unloaded; `OnBoxGettingFocus` likewise returns before reloading. Existing independent Unloaded cleanup and the real library base remain unchanged. Fresh linked compilation and runtime Loaded/input evidence support these guards for the exercised paths. This is not a proof for every possible focus/activation ordering.

## Required ordering and evidence

Historical failure remains untouched in [ui-51-fixed.md](ui-51-fixed.md) and [original lifecycle events](evidence-51-fixed/lifecycle/events.jsonl): Keyword q 8921.709ms → Unloaded 8928.121ms → UserInput 9083.233ms → new request 9343.575ms → result published 11411.266ms while unloaded.

Fresh run PID15960, [ordering](evidence-51-late-input/late/ordering.json), [events](evidence-51-late-input/late/events.jsonl):

- Keyword=q at **5789.8609ms**, loaded=true.
- Actual Unloaded at **5802.3537ms**.
- Actual TextChanged(UserInput q) at **5947.7575ms**, loaded=false, **145.4038ms after Unloaded**.
- No requests or nonempty state/render publications during the entire **2739.0364ms** unloaded interval, including releasing the cancelled initial recommendation. Requests remain 1; popup closed, suggestions empty. No q response exists to release because no q request was started.

[Offline audit](evidence-51-late-input/audit.ps1) independently verifies the full interval and writes [audit.json](evidence-51-late-input/audit.json). Each UI scenario ran once; there were no random retries or production fix attempts.

| Requirement | Fresh evidence / exact test |
| --- | --- |
| Unloaded before delayed real input; no new request/result | `LateUserInputAfterUnloadStartsNoWork`, [late 2/2](evidence-51-late-input/late/results.json), required order asserted, not normal cancellation substituted. |
| Remount real input and empty focus | `RealInputAndEmptyFocusAfterRemount`: resumed result and recommendation popup visible. |
| Initial real input not blocked by Loaded guard | `BurstFinalPopup`: Loaded 307.8377ms; first UserInput bili 1970.122ms loaded=true; request 2225.7603ms; result and popup visible. Initial focus also requested recommendations while loaded. |
| First unload cancellation | `FirstUnloadCancelsFocusRequest`, [lifecycle 4/4](evidence-51-late-input/lifecycle/results.json): exactly one cancellation; late completion rejected. |
| Second unload, already-started request and late response | `SecondUnloadRejectsLateResponse`: inflight request starts while mounted; cancellation 3246.9197ms, Unloaded 3247.5681ms, cancelled completion 3262.7770ms; no transient publication or remount revival. |
| Third unload cancels pending debounce and resumes | `ThirdUnloadCancelsDebounceAndResumes`: q UserInput 6740.2989ms loaded=true; Unloaded 6746.8379ms (6.539ms later, inside 250ms); no q request before remount, then real resumed input succeeds. A q focus request after remount is legitimate and not counted as unloaded work. |
| Selection timer and late focus response | `SelectionTimerUnloadAndLateFocusStayCleared`: fourth/fifth unloads, 850ms fences, cancelled focus completion rejected, final remount empty/closed. No direct timer tick proof claimed. |
| Seven real-input regressions | [suite 7/7](evidence-51-late-input/suite/results.json): `BurstFinalPopup`, `SelectionDoesNotRequery`, `OutOfOrderOldNeverPublishes`, `ClearRejectsLateResult`, `EmptyFocusRecommendations`, `UnloadRemountRejectsLateResult`, `ChineseImeComposition`. Actual NIHAO virtual keys via installed IME, composition start/end, Space commits 你好; no UIA SetValue. |
| Actual mouse selection after IME | [mouse 1/1](evidence-51-late-input/mouse/results.json), `ImeMouseSelectionExactNode`: exact visible item clicked, chosen keyword 你好, display 你好 result, closed popup, no added request through actual fence **876.7376ms after submit**. |

Original `SecondUnloadRejectsLateResponse` in repro-unload.ps1 depended on the old debounce defect to create q. Its historical precondition failure remains in [original-repro/results.json](evidence-51-fixed/original-repro/results.json). It was not copied as the current precondition or rerun to erase its failure. The independent started-request case above covers late response rejection. Historical lifecycle failures and incomplete mouse-fence evidence remain unchanged.

## Screenshot review and limits

All **20 new PNGs** were individually opened with the image viewer and compared with state/UIA/events; per-image findings are in [visual-review.json](evidence-51-late-input/visual-review.json). Three late-input, five lifecycle, eleven suite, one mouse captures. Expected English suggestions, empty state, remount, actual Chinese IME candidate/composition/commit, Chinese result and mouse closure are visible.

**Visual gaps retained:** every screenshot has right-side clipping in the fixture command row (Global/Close area); no complete fixture-layout PASS. `suite/old-rejected.png` omits the input/popup pixels although adjacent state and UIA describe loaded/open `new result`, matching the historical capture inconsistency. The immediately preceding `new-result.png` visibly contains the new result. This is not upgraded to a visual pass, and no new production diagnosis is inferred solely from it. Theme/DPI/accessibility matrices, real regional navigation destinations, release/AOT UI, performance improvement and long-term leak behavior remain unverified. Navigation is a recorder, not production pages. #51 remains open for remaining acceptance work.

## Fresh full checks and cleanup

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=search51-late-input.trx' --results-directory docs/performance-acceptance/evidence-51-late-input
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
```

Release exit 0: **Passed: 401, Failed: 0, Skipped: 0**. [TRX](evidence-51-late-input/search51-late-input.trx), [parsed counts](evidence-51-late-input/trx-summary.json): total/executed/passed/results all 401, failed/error/notExecuted/pending all zero. These headless tests do not test real AppSearchBox lifecycle. Debug x64 exit 0: **0 warnings, 0 errors**, 27.61s; [log](evidence-51-late-input/debug-build.txt). [Commands](evidence-51-late-input/commands.txt) record the harness runs.

Only `BiliCopilot.Acceptance51Late751` was deployed. Owned PIDs **15960, 62728, 53704** closed using owned Close; each package removal checked its exact InstallLocation. Each cleanup reports process/package absent; audit confirms all three absent and the identity unregistered. All four UI runner yields report `released:true`; redundant final yield reports `released:false` because ownership was already released. **Desktop released.** Temporary owned source/layout artifacts remain for diagnosis; nothing belonging to another workflow was removed. Kernel remains clean at `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`.
