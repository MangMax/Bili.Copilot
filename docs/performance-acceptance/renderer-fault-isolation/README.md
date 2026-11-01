# 弹幕终态隔离复验（父 ad4d17d3）

最小产品修复及目标边界 AOT 验收完成，供父任务安排独立 review。保留既有首页诊断，**不宣称整个宿主诊断全清**。

## Scope and recovery boundary

Only production change: `VideoDanmakuPanel.xaml.cs`. The three `seekd1fc.20261101.2` packages, formal dependencies, gitlink, kernel checkout, player/VM, and global exception handling are unchanged.

The public Legacy `WorkerFailure` protocol identifies the failed instance. Before renderer calls, or when the exact published cause / its terminal `InvalidOperationException` wrapper escapes a call, the panel records the cause once, invalidates redraw, detaches references and closes on the UI dispatcher. Progress, seek, pause/resume, style/speed, add/clear and redraw cannot reuse the retired instance. Unrelated exceptions still propagate. Cleanup exceptions are caught and recorded only for a known faulted renderer; cleanup failure prohibits recovery on the same panel.

Recovery is explicit: a different playback ViewModel, or closing/reopening a playback window with a new panel. Switching an episode **within the same ViewModel**, refreshing its danmaku list, changing renderer settings, hide/show and Loaded/Unloaded do not clear failure. If cleanup failed, a fresh panel is required. No automatic retries. Core has no corresponding public terminal-cause property; this patch does not invent a private-state probe or alter its engine handling.

## Verification

- Red: linked **unchanged parent panel source** reproduced `Progress -> UpdateTime` terminal rejection. 4 failed, 1 passed; original output and TRX retained.
- Final panel tests: 12 passed, 0 failed, 0 skipped, fresh output directory; renderer/XAML protocol adapters, not an AOT qualification substitute.
- Existing full suite: 577 passed, 0 failed, 0 skipped, fresh exported parent+patch source and exact gitlink kernel export. No tests ran against or reconstructed the dirty kernel checkout.
- Native verification: `native-fixed` is an unexecuted intermediate build; `native-final` / run `final` used the final product source and passed home 27, normal seek 20, all fault-isolation checks, and new-instance creation. Its last `RecoveredSeek` assertion failed because it read MPV's observable position before its asynchronous event (28 seconds arrived 66 ms later). Exit was 0, exact WER=0 over 31+ seconds, registration/process absent, yield invoked, terminal log exactly once. This run remains **failed**, with its full receipts retained.
- Only the fixture was corrected to wait up to 1,000 ms for both the 28-second position and progress text. Product source did not change. Corrected replay `native-verified` / `verified`: **27 home + 32 media/danmaku assertions passed**, including `RecoveredSeek position=28 progress=00:00:28`. Terminal log=1; global exception logs after retirement=0. Native x64 EXE SHA256 `5b7441bae0c8d0b0de11a0b7fbc37fa056213d6cf371b005e5dcea841128cc2f`, CLR directory=0; no CLR runtime modules loaded; real libmpv loaded from the owned fixture. PID48252 retained exit=0; 31.3796-second exact PID/path WER gate `PASS_BOUNDED`; process and registration absent. Yield command succeeded (`released=false` receipt retained).

| Requirement | Evidence |
| --- | --- |
| Progress, seek, style, redraw, hide/show do not reuse failed instance; log once | `TerminalProgressRetiresOnceAndKeepsSubsequentInputsUsable` |
| Failure published inside a call | `FailurePublishedDuringCallIsRetired` |
| Preserve unrelated exceptions, including concurrent terminal publication | `UnrelatedExceptionStillPropagates`, `ConcurrentUnrelatedErrorIsNotMistakenForTerminalRejection` |
| Unload handles previously unobserved terminal failure | `UnloadDetectsPreviouslyUnobservedFailure` |
| Explicit recovery; ordinary reload cannot recover | `NewViewModelCanRecoverButReloadAndRedrawCannot` |
| Cleanup failure cannot escape or enable reuse | `FaultedCleanupFailureIsLoggedAndDoesNotEscape`, `CleanupFailureRequiresNewPanel` |
| Failure while replacing renderer stops replacement | `FailureDuringReplacementCleanupDoesNotCreateAnotherRenderer` |
| Cleanup on UI owner; queued input cannot cross replacement/unload | `BackgroundProgressRetiresOnlyOnOwnerDispatcher`, `QueuedOldViewModelInputCannotTouchReplacement`, `QueuedInputCannotCrossUnloadAndReloadOfSameViewModel` |

## Replay (fresh owned output only)

Use the installed SDK `11.0.100-rc.3.26522.148`. Example paths below are this machine's existing read-only inputs. Do not install AotDev or replace an existing registration.

```powershell
$dotnet = (Resolve-Path ../t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe).Path
$replay = 'artifacts/fault-replay-new'
python docs/performance-acceptance/home-refresh-navigation/prepare.py --ref HEAD --kernel-repo ../t3-c10467bb/artifacts/aot-dev-install/kernel.git --runtime-assets ../t3-c10467bb/artifacts/home-aot/fixture/Assets --output $replay
python docs/performance-acceptance/renderer-fault-isolation/prepare-seek-aot.py --artifact-root $replay
& $dotnet test "$replay/source/tests/BiliCopilot.UI.Tests" --logger trx
& $dotnet test "$replay/source/tests/BiliCopilot.DanmakuPanel.Tests" --logger trx
./docs/performance-acceptance/renderer-fault-isolation/publish-owned-host.ps1 -Dotnet $dotnet -ArtifactRoot $replay -MpvFeed ../t3-c10467bb/artifacts/aot-payload-verification/local-feed -Output "$replay/native"
./docs/performance-acceptance/renderer-fault-isolation/run-home-seek.ps1 -Run final -ArtifactRoot $replay -InputDirectory "$replay/native/publish"
```

`prepare.py` exports the fixed ref, exact kernel gitlink and complete application. The diagnostic fixture replaces startup/DI with the established offline home/player services, adds the typed driver and a setter-return timing marker; it changes no engine DLL or trim roots. `SeekAot.cs` injects only a normal `InvalidOperationException` through real Legacy `FailWorker`. No damaged COM/GPU/native resource, dump investigation, login or credential copying.

The runner rejects CLR PE, uses a fresh `Mang.BiliCopilot.HomeRegression.T5df5.*` identity, retains the owned process handle, closes via its own UI, observes exact PID/path WER for >=31 seconds, removes only its exact temporary registration and yields. It never terminates a user process or cleans historical files. Check `seek.tsv`, `controlled-results.json`, loaded modules, `exit-verdict.json`, and `cleanup.json`; launcher exit alone is insufficient.

## Protection and limits

The first package snapshot recorded user AotDev `2.2611.1.1`. Before any test-host launch, a later read-only snapshot recorded `2.2611.1.2` and user PID `47980` (created 13:31:51 +08). This task did not install/launch that update; do not report the entire session's registrations as unchanged. The later snapshot is the protected baseline for the owned runs. Official and Caa6a7be registrations are also protected. No user data, credentials or processes are targeted by the scripts.

This is the existing complete-UI offline AOT fixture, not a signed-user-update qualification. Its pre-existing fixture warning policy is retained; warnings are not claimed cleared. Normal latency samples are bounded machine observations, not optical/frame-level or all-history coverage. The screenshot shows only the owned home harness; fault/media assertions come from the real PlayerViewModel/MPV and panel receipts.

The inherited home fixture logs six `TemplateRoot not found` exceptions through the **unchanged** global App handler, plus the existing offline partition/image errors. The read-only original failing host's `owned-app.log` contains the same signatures. The first corrected-product run had **zero** global-exception logs after the terminal-retirement marker. Raw application logs are retained. The inherited exit gate's `managedUnhandled` counter only scans `controlled.jsonl`; it is not an application-wide zero-error claim. The validator separately reports total application global-exception logs and terminal-phase logs. Whole-host diagnostics are not claimed clean; this fix is limited to the renderer/media boundary.

## Review handoff

Parent verified as `ad4d17d3a521cf2930caca247a69757f356b056e`. `final-source-audit.json`: 1,084 exported inputs, only the panel differs; final panel SHA256 `96547fb549b4bccbc7aa998dbe1ed9f57dadfa3fedcfefc7c9c04293d8923e7c`. `dependency-audit.json`: all 474 kernel files match gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`, and all four ILC package DLL/cache/feed byte chains match. The live dirty kernel remains at `f02c33f639addd5d421700c43fa66382beafe323`.

**Protection observation is not wholly unchanged:** official/AotDev identities, versions and paths, and PID47980 with its exact creation time/path remained unchanged. The pre-run Caa6a7be registration is absent from the final snapshot; cause is unknown. The task's only uninstall operations target exact `T5df5.final` / `T5df5.verified` identities with matching owned layouts, as the runner and cleanup receipts show. No user-package restoration/relaunch was attempted. Preserve this unresolved observation for the parent/reviewer (`protection-verdict.json`).

Self-review: terminal recognition is restricted to the published current-instance failure or its exact wrapper; unrelated concurrent exceptions propagate; retirement occurs once on UI, stale callbacks cannot cross instances, and cleanup failure blocks reuse. No engine/internal reflection was added to product code. Independent review is left to the parent as requested; no subagent was launched.

Small receipts, original native failure, red tests and the failed recovery-assertion run are in [evidence.zip](evidence.zip), with a verified SHA256/length manifest. Native payloads/PDB/binlogs remain under this worktree's `artifacts/home-aot/`; no historical cleanup was performed. Run `python docs/performance-acceptance/renderer-fault-isolation/validate.py --run verified` to verify the completed local run.

No push, PR, issue, subagent or writes to source worktrees `t3-c10467bb`, `t3-870b980a`, `t3-d1fc3815`. The initial modified `externals/bili-kernel` entry remains untouched.

Evidence ZIP SHA256: e0fcfd865fc5df6c2fd93f1bba9e472c85edcb4a189436f0d40efbc7b8759955 (88 manifest-verified payload entries).
