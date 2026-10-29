# #50/#53 lifecycle integration review

2026-10-30. **有限离线实机通过，工单保持开放。** No substantive defect established in the scoped production change. This review verified inherited real WinUI evidence and ran fresh parent tests/builds; it did not deploy, launch, send desktop input or rerun the UI harness.

## Provenance and scope

Only `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`, was used. Initial HEAD was clean at `13c29ff8d06d8559bb1c2540308286c77e952ea2`. Command `git merge --no-ff 2b71b0f003fac2d89f1f84bf41c3fcb22263ac5c` created merge `76a2357c15a7be157aab3667dca282fab6f53af6`, with exactly those two parents. Incoming ancestry contains `da7e862a31523657257324d0bc9bae758704744d`, the acceptance cherry-pick of `a53413fd7aa3d8a9d2832ff26d769af6a1aa9aae`. The original acceptance's 119 files are documentation/evidence; their corresponding directories match the cherry-pick without differences.

Production delta from initial HEAD is exactly PopularMainBody.xaml.cs, PopularSideBody.xaml.cs and IncrementVerticalScrollControl.xaml.cs. The adapter and two additional tests are the only test changes. No XAML, VM, dependency or kernel changes. Existing #51 search and #52 image fixes are preserved. Kernel gitlink and clean nested checkout remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`.

Read AGENTS.md, docs/agents rules, GitHub #50/#53, incoming report, harness and structured evidence. Applied winui-code-review, winui-ui-testing, run-tests and verification-before-completion. No test overlay is present; SDK 10.0.403 runs net9.0 xUnit 2 via VSTest. No child agents, other checkout, push, PR mutation or issue mutation.

## Lifecycle review

- All three controls retain their own Loaded/Unloaded delegates across removal. `_isActive` makes duplicate Loaded and Unloaded notifications idempotent, and inactive state is set before unsubscribe/cleanup. The main VM is resolved once by PopularPageControlBase; this change does not introduce VM replacement semantics.
- The restored Richasy.WinUIKernel.Share 0.0.12-preview3 DLL SHA256 matches the incoming IL evidence: `BC68250223D14F53F87082B35428EE2144B7333D124A09AF2A172BC389A232B7`. Its first Unloaded removes only its own delegates and calls the virtual hook. These three controls no longer override those hooks, so the base and direct delegates cannot double-run the business attach/detach logic. Binding initialization/stop-tracking remains base behavior; arbitrary full-app binding/navigation combinations are not certified by this review.
- Scroll retains `_checkPending` across unload until the physical callback drains. A callback while inactive clears the slot without loading; after remount it reads current geometry and command eligibility. Clearing the slot before executing permits one reentrant follow-up. TryEnqueue rejection releases the slot. No forced execution when CanExecute is false.
- Each effective unload increments `_loadVersion` once. The 500 ms continuation must retain its version and then pass the active guard before enqueueing; a delayed notification cannot load into a later mount. This is callback invalidation, not cancellation of an already running service request.
- Side unload clears `_selectionPending`, detaches layout/VM/selection events and clears ItemsSource, while retaining `_selectionQueued` until its callback drains. Reload restores ItemsSource, subscriptions and selection intent. The callback reads current SelectedSection/current index, resets its queued flag and rejects inactive/unloaded state. Old selection intent is not captured for publication after remount. A missing index leaves pending intent for a later layout opportunity. Physical queue interleavings remain headless/static evidence, not instrumented real-dispatcher queue-depth measurements.
- The adapter now models the NuGet first-unload self-unsubscription rather than directly calling virtual hooks every time. Its event invocation snapshot preserves derived handlers when the base removes its own. It copies no pagination logic, but does not emulate XAML, bindings or ABI; the real-NuGet fixture is the separate evidence for those exercised paths.
- No added blocking waits, background UI mutation, exception swallowing, business logic, or altered pagination threshold. Existing project analyzers passed; dedicated WinUIAnalyzer is not referenced and its checks did not run. No new dependency was installed.

## Evidence audit

Independent XML/JSONL parsing and current source/DLL hashes are saved in [verification.json](evidence-integration-50-53/verification.json). The three current source hashes match incoming final-source-hashes.json. Original red evidence is preserved: original 12 tests have 9 passed / 3 failed; expanded 14 have 9 passed / 5 failed; green has 14 passed / 0 failed. Failures are the reload/pending-slot/old-delay cases and the two added repeated-event/remount cases. Fresh full Release includes all 14 scroll results. These historical red runs were inspected, not recreated by reverting parent code.

Incoming real WinUI [red results](evidence-50-53-lifecycle-fix/red-ui/results.json) have 1 passed / 8 failed; [green results](evidence-50-53-lifecycle-fix/green-ui/results.json) have 20 passed / 0 failed. Read the runner assertions, source-linked fixture project and raw events rather than relying on the headline. The fixture retains real VM partials, generated commands, three controls, NuGet base/AppRepeater, Dispatcher, delay, layout and scrolling. Services and application composition are offline adapters.

Raw green JSONL independently yields 23 paired stable windows, each at least 2950 ms after a 1500 ms settling interval, with unchanged request, notification, ViewChanged and SizeChanged counts. Rendering counters continue and are not included in the idle claim. Three unload states have versions 1/2/3, active=false, main/side subscriptions 0/0 and requests 5/10/15. Each remount has active=true, restored source and subscriptions 1/1. Loaded itself adds one bounded empty-page check; refresh subsequently adds four requests and three notifications, ending at requests 10/15/20 and notifications 6/9/12 with three items. These observations support recovery without subscription multiplication, not an unlimited-duration no-leak claim.

Other inherited cases establish first cards while the directory is pending, saved-partition recovery/missing-directory fallback, empty/error refresh recovery, populated ranking quiet state, stale-section result rejection and real scroll/resize. Recorded first-card intervals are about 1090.37 ms and 184.70 ms in single controlled runs, not performance benchmarks. Stale-section switching is a fixture VM command, not complete user-navigation acceptance. Unload tests do not have outstanding service work and cannot prove transport cancellation.

This parent review opened four inherited PNGs: red reattached-3, green reattached-3, green remount-refresh-3 and green long-burst. Red has no sidebar and one item; green restores sidebar and two full visible cards plus a partial third row; the long list shows #58/#59. The incoming report records viewing all 38 red/green PNGs; this parent performed only the stated spot review, not a fresh full visual audit. Covers remain placeholders. Source identity comes from hashes, not the unchanged baseline window title. Incoming cleanup records owned process/package absent and final yield released=true; no desktop state was re-probed here.

## Explicit remaining boundaries

The fixture records **150 ArgumentNullException(logger)** events in ImageExBase. They are retained in [runtime-exceptions.json](evidence-50-53-lifecycle-fix/runtime-exceptions.json), and the harness logs without setting Handled=true. Completing the lifecycle matrix does not establish an exception-clean app or successful image loading/decoding. No image fix was added here.

Still unverified: authenticated API/full production app and navigation; saved settings across processes; PR #27 incremental refresh/undo compatibility; all real-dispatcher instantaneous queue interleavings; complete theme/DPI/accessibility visuals; Release/Native AOT execution; image budgets; sustained CPU/memory/virtualization limits and #56 production sampling. Existing process samples are fixture observations, not #56 acceptance. #51/#52 limitations and the existing AOT toolchain blocker remain unchanged. Neither #50 nor #53 is closed.

## Fresh parent verification

All commands below completed against merge `76a2357`; complete logs/TRX are committed in [evidence-integration-50-53](evidence-integration-50-53).

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=release.trx' --results-directory docs/performance-acceptance/evidence-integration-50-53
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -v:normal
```

Release exit 0: **Passed: 403, Failed: 0, Skipped: 0**; parsed TRX total/executed/passed and result-entry count all 403, including 14 scroll cases. Debug x64 exit 0: **0 warnings, 0 errors**, elapsed 00:00:56.73. Explicit runtime dependency gate exit 0: **0 warnings, 0 errors**, elapsed 00:00:00.65. File presence is not native loading or launch validation. No assets were copied/downloaded and no deployment or desktop actions were performed.
