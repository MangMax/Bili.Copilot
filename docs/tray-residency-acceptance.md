# Tray residency acceptance — T3 / #42

Date: 2026-10-29 (Asia/Shanghai). Spec: [#39](https://github.com/MangMax/Bili.Copilot/issues/39); task: [#42](https://github.com/MangMax/Bili.Copilot/issues/42).

**Overall: NOT ACCEPTED.** Automated lifecycle tests and builds pass. A real isolated WinUI login window was launched and inspected, but no authenticated main window was available. The tray interaction, resource measurements and playback/download acceptance below remain NOT RUN. This report must not be used to close the acceptance ticket as passed.

## Revision and environment

- Production source tested: `6bd16eee126b32311f699258010be84761aaba87` (T1 + T2); prepared branch `t3/verify-tray-residency` started clean at exactly this integration tip. No production code or unit-test changes were made for T3.
- Windows 11 Pro x64, OS version `10.0.26300`, build `26300`; .NET SDK `10.0.403`, WinApp CLI `0.7.1`; app TFM `net9.0-windows10.0.22621.0`. Tests use xUnit 2 / VSTest.
- Release x64 JIT, **not Native AOT**. Normal configured compiler analyzers ran. Dedicated WinUIAnalyzer is absent and was **NOT RUN**; no analyzer package was added.
- Applied `winui-ui-testing`, `winui-dev-workflow`, `winui-code-review`, `winui-packaging`, `run-tests`, `systematic-debugging` and `verification-before-completion`. No unit-test edits required a TDD cycle.
- Sandbox was unavailable per prior deployment evidence; the read-only snapshot also reported no running/ready guest. Local UI testing was explicitly authorized. No Windows feature changes or reboot.
- Local evidence root: `C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/t3-evidence`. Screenshots, deployment layouts, logs and data are deliberately outside the repository.

## Commands and automated results

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=t3-tests.trx' --results-directory C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/t3-evidence
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-5bc1cf3a/src/ -bl:C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/t3-evidence/t3-production-final.binlog
```

**PASS — Passed: 300, Failed: 0, Skipped: 0.** TRX independently parsed: total/executed/passed 300, failed/error/timeout/aborted/notExecuted 0. All 18 `TrayLifecycleTests` results pass (`lifecycle-results.json`). These tests simulate window/tray/storage boundaries; they are not Windows Shell or real storage tests.

**PASS — final production build: 0 warnings, 0 errors**, 9.02 seconds (`t3-production-final.log/.binlog`). Correct `SolutionDir` was supplied from the first build. Initial clean build also passed, with one external protobuf `empty.proto` unused-import warning, 0 errors, 3:10.49 (`t3-build.log/.binlog`). The intermediate isolated-identity build passed with 0 warnings/errors, 16.34 seconds (`t3-isolated-build.log/.binlog`). Ignored libmpv/ffmpeg build assets came from the existing main checkout.

## Actual deployment and UI evidence

The original installed release and development identities were not uninstalled, reset or used for test interaction. No authentication data was read/copied and no login bypass was introduced.

1. Project-mode `winapp run --no-build --manifest <temporary manifest>` still deployed the generated production identity and was rejected with `0x80073CFB`. The suggested uninstall was not performed (`launch.json`).
2. Folder-mode staging using the source dev manifest registered a separate identity and launched PID 34908, but crashed with `0x80040154` in Windows App SDK initialization: the source manifest lacked the generated runtime dependency (`launch-folder.json`, `folder-runtime-error.json`).
3. A temporary generated manifest preserved `Microsoft.WindowsAppRuntime.1.8` and WinRT registrations while changing only package identity, publisher, toast COM GUID and the staged executable path. Further CLI staging replaced it with the input manifest (`launch-final.json`, `launch-ready.json`). Native development registration succeeded after omitting `-DisableDevelopmentMode` (that flag first produced `0x80073CF9`). PID 51260 had no visible window at inspection and was terminated only after checking its isolated executable path.
4. To generate matching manifest/resource metadata through the SDK, the production manifest was temporarily replaced during a build inside a `try/finally`, then restored byte-for-byte. No application source logic changed. A project-mode launch then used the built isolated identity (`launch-sdk.json`). PID 49408 returned empty early UI trees and was ended after checking its isolated path. These early empty trees are not proof of a permanent startup failure.
5. A diagnostic project-mode launch with `--debug-output` opened the real login window: PID **43596**, HWND **9442372**, `WinUIDesktopWin32WindowClass`, 1080×690 physical pixels, 144 DPI. Executable: `t3-evidence/sdk-isolated-layout/BiliCopilot.UI.exe`. Package: `Mang.BiliCopilot.TrayAcceptance.T3_2.2610.0.0_x64__y1mf40g4r9srg`; AUMID: `Mang.BiliCopilot.TrayAcceptance.T3_y1mf40g4r9srg!App`.
6. `winapp ui` inspected 24 elements and captured `startup-login.png`; the image was visually reviewed. It shows only QR/web login, refresh, documentation and close, with no clipped or overlapping controls in this captured state. `ui-tests.ps1` then verified the process path/window, waited for `TipBlock`, recorded the tree, invoked the actual window Close button, and observed process exit within 10 seconds. Results are in `ui-results.json`. No tray icon was created, so this cleanup is **not** evidence of tray exit cleanup.

The tested isolated `BiliCopilot.UI.dll` SHA-256 is `36CD3BEF84CFEE5F7466A3D5B38689958A17D7ED97435DD9546D56AA58A5DD0A` (`tested-binary.json`). Debug output contains first-chance CLR exceptions; no successful tray behavior is inferred from that run. Only test-package processes were stopped. The final process inventory contains the user's installed app, no isolated test app. The isolated development package/layout is retained for follow-up testing.

The remaining gate is explicit: `AppViewModel.LaunchAsync` creates `MainWindow` only when `_tokenResolver.GetToken()` is non-null; otherwise it creates `StartupWindow`. `MainWindow` constructs the tray lifecycle. The startup UI/source has no guest/skip route. Supplying fake token data or constructing a test-only main window would not satisfy acceptance.

Package inventory notes: the existing dev installation was version `.17` at initial inspection and `.18` at final inspection, with a different user-app PID. This session did not deploy either installed user identity; both attempted production-identity registrations were rejected. Thus the before/after inventories record concurrent external activity, not a claim that the user's package version remained unchanged. No user settings files were inspected to compare their contents.

## Requirement matrix

PASS in the unit/static column is deliberately separate from real application acceptance.

| Requirement | Unit/static evidence | Real application status |
| --- | --- | --- |
| Default off, ordinary close | PASS: `FreshInstallationKeepsNormalCloseAndHasNoTray`; settings fallback false | NOT RUN: main window requires login |
| Live enable, persisted switch, restart | PASS: `EnabledSettingSurvivesRestartAndClosesToTray`; settings read/write and TwoWay binding reviewed | NOT RUN: no settings UI or real persisted toggle exercised |
| Hide and same-window restore; tray click and Show menu | PASS: shared restore seam, 20 simulated cycles | NOT RUN |
| Minimized restore and repeated launch | PASS: restore calls Show, presenter Restore, Activate; redirected activation shares seam | NOT RUN: no main-window/native second-instance acceptance |
| Disable/re-enable, release icon repeatedly | PASS: `RepeatedEnableDisableReleasesIconsAndRecoversHiddenWindow`, 20 simulated cycles | NOT RUN: no native icon created |
| Tray creation failure retains access/retry | PASS: both `TrayFailureRetainsAccessibleWindowAndCanRetry` cases | NOT RUN: no native failure injection |
| Last player close stays resident, integration on/off | PASS: both `ResidentPlayerCloseKeepsMainHiddenUntilExplicitRestore` cases; five nonresident compatibility cases | NOT RUN |
| Explicit exit saves/joins cleanup; repeated exit/activation races | PASS: shared exit, pending player close, save/cleanup failure tests | NOT RUN: login-window close is not this path |
| Real process and tray-icon removal after explicit exit | PASS: terminal seam disposal/recreation guard | NOT RUN |
| Playback continuity while resident; playing/paused/immediate-open exit and history | Static cleanup/history ordering reviewed | NOT RUN: authentication/main window unavailable |
| Download continuity | No download logic changed in feature diff | NOT RUN: authentication/main window unavailable |
| Notification switch and original cadence | PASS static: no scheduling changes or added periodic requests in feature diff | NOT RUN: real notification settings/cadence not exercised |
| Keyboard/Narrator access, live binding and menu text | PASS static: localized labels and automation IDs; TwoWay toggle | NOT RUN: tray/settings UI unavailable; no Narrator run |
| Explorer restart recovery | PASS unit/static after review fixes: live health, post-recreation event, access restoration and retry; see addendum | NOT RUN: user Explorer not restarted |
| Foreground 60 s / resident 60 s CPU, private bytes, handles | No qualifying sample | NOT RUN |
| 20 actual hide/restore cycles and resource trend | Simulated cycles pass; no native measurements | NOT RUN |
| New periodic polling / repeated subscription audit | PASS static for introduced code; no new timer/poller/GC trimming | NOT RUN for runtime growth claim |
| Isolated real login window and its close | PASS: UIA, screenshot review, actual close/process exit | PASS (deployment smoke only) |

## Measurements

| Required state | Duration actually sampled | CPU time | Private bytes | Handles | Result |
| --- | --- | --- | --- | --- | --- |
| Same idle main page, foreground, notifications off | 0 s | N/A | N/A | N/A | NOT RUN |
| Same idle main page, resident, notifications off | 0 s | N/A | N/A | N/A | NOT RUN |
| Before/after 20 actual hide/restore cycles | 0 cycles | N/A | N/A | N/A | NOT RUN |

No resource/performance PASS, leak conclusion or CPU comparison is claimed. Startup/login activity includes authentication requests and is not the specified idle page. A debugger-attached login process would also be a confounded baseline. `performance-status.csv` explicitly records unavailable samples instead of fabricated zero-valued measurements. Playback/download continuity must be measured separately after login.

## Final WinUI review

- **PASS, static ownership:** each enable creates a fresh resource dictionary, commands and icon session. Both command subscriptions detach before icon disposal. Initial creation failure disposes the partial session and cancels close with restore. Disable releases the current icon; final disposal rejects recreation. App-instance activation is one subscription per app lifetime, not per cycle.
- **PASS, static exit ordering:** native main close is canceled before any await; `Task.Yield` lets that event return. `ExitAsync` publishes the shared task before callbacks, awaits settings and snapshot player cleanup, rejects reactivation, then releases tray and closes. Player close/dispose operations are shared; initialization is joined before disposing player/client/window. Native Destroying routes through observed close; duplicate native destruction is guarded. Keyboard, SMTC, client and connector subscriptions have cleanup paths. Network completion and actual native callback ordering still require runtime tests.
- **PASS, static UI contract:** `TrayResidencyToggle` has a localized accessible name and explicit TwoWay binding. The existing observable setting persists and applies live. `TrayShowMainWindow` / `TrayQuitApp` have automation IDs and localized command labels via the existing resource mechanism. Settings layout reuses the existing card/theme. Runtime native popup accessibility, focus, translations and Narrator remain unverified.
- **PASS, static overhead:** no added periodic timer, polling loop, forced GC or working-set trimming in the integrated feature diff. Notification/download scheduling was not modified. This is not proof of stable native handles/memory over 20 cycles.
- **Original review finding — addressed in source by the addendum below:** restored H.NotifyIcon 2.3.2 records repository commit `6b97277d084f68bcf60f08fcd769503e8e703bfa`. Its `MessageWindow` registers `TaskbarCreated`; `TaskbarIcon.OnTaskbarCreated` removes/recreates the icon, but catches and ignores recreation exceptions. The reviewed baseline only checked a cached session. Live-health checks and a post-recreation event handler now cover that failure at the approved unit boundary. Normal Explorer restart and native recreation failure still require real verification. Source snapshots are saved under `H.NotifyIcon-*.cs` in evidence; no Explorer restart was performed for this fix.
- **Note — exception continuation limits:** the tested save/player cleanup failures continue to exit, but arbitrary exceptions in the final native shutdown callback can still fault the shared task and be logged by `RequestExitAsync`. Native notification unregister/window-close failures were not injected. Do not generalize pure-seam PASS to all OS failures.

## Follow-up and evidence

Authenticate the retained isolated test package through its normal UI using a permitted test account; do not transplant user tokens. Then execute every NOT RUN row, including the same-page notification-off 60-second samples, 20 cycles, playing/paused exits and independent download continuity. Explorer recovery requires a safe dedicated desktop/session. Parent performs final two-axis review and integrates into draft PR #43; this work creates no PR, push or issue closure.

Primary evidence: `t3-tests.trx`, `test-counters.json`, `lifecycle-results.json`, build logs/binlogs, `environment.json`, `packages-before.json`, `packages-after.json`, launch logs, `ui-tests.ps1`, `ui-results.json`, `ui-batch-tree.json`, `ui-windows.json`, `ui-process-verified.json`, `startup-login.png`, `tested-binary.json`, `processes-after.json`, `performance-status.csv`. The screenshot contains a fresh test login QR and is not committed. No raw application logs/settings/authentication material is committed.

## Integrated review fixes (2026-10-29)

Prepared branch `t3/tray-residency-review-fixes` started clean at integration `b99cb14dcefca54eb4bb5802f64cab7de2c0173a`. This addendum supersedes the cached-session and Efficiency Mode defects in that baseline; earlier deployment evidence remains historical. No UI was launched, closed or inspected, and the retained isolated package/layout/data was not modified. Parent must redeploy after login while preserving its data.

- P1: `ITrayIconSession.IsCreated` reads live native icon state. Every close checks it; an unhealthy session is unsubscribed/disposed and fresh creation is attempted. Failure cancels close, logs and restores the same window. Shell recreation failure also restores an already resident or minimized window immediately.
- Ordering: the actual restored H.NotifyIcon 2.3.2 exposes `TaskbarIcon.TrayIcon.MessageWindow.TaskbarCreated` (`EventHandler`, verified with project-scoped `winapp find-api` and package XML). Its constructor subscribes the synchronous remove/create callback before our session subscribes to the same event. Our handler therefore observes the completed library attempt, including its swallowed failure, without a second HWND listener or a dispatcher race. It does not interpret the temporary removal state as a failure. The source snapshots and restored references agree; this ordering must be revisited if upgrading the dependency.
- The failure callback retains the session until the native message returns; it does not destroy the message window inside its window procedure. The next close retries with fresh resources; a later Shell restart can also recreate the retained icon. Successful recreation does not activate the main window. Disabled, disposed, replaced-session and exit-time notifications cannot restore or recreate resources. Each session has one health subscription, removed before disposal; its native event and both commands also detach. No timer, polling, dependency or shutdown workflow was added.
- P2: `App.CreateTrayIcon` now calls `ForceCreate(enablesEfficiencyMode: false)`. Static dependency inspection confirms this skips the process-wide scheduling change. This is not runtime evidence of playback performance or Windows process QoS.

Testing remains confined to the approved `TrayLifecycle` system boundary. The fake desktop supplies Shell availability/events, icons, windows and diagnostic output. Assertions cover visible/minimized windows, live icon ownership, errors and subscriptions rather than only lifecycle flags. The actual native adapter and event ordering are statically checked and compiled, not executed by these unit tests.

| Requirement | Evidence |
| --- | --- |
| Cached icon loss, failed next close and later retry | `PostCreationLossPreventsCloseIntoMissingTrayAndCanRetry` |
| Failed Shell recreation restores resident/visible minimized window; later close retries | `FailedShellRecreationRestoresAccessAndSubsequentCloseRetries` (2 cases) |
| Successful Shell recreation preserves residency and icon | `SuccessfulShellRecreationKeepsResidentWindowHidden` (20 events) |
| Repeated enable/retry/disable releases subscriptions and resources | `RepeatedEnableAndRecoveryDoNotAccumulateHealthSubscriptions` (20 cycles) |
| Disabled/old-session late event cannot affect window or replacement | `DisabledSessionCannotRestoreWindowOrAffectReplacement` |
| Pending and completed exit ignore recreation failure/late event | `ShellFailureDuringAndAfterExitCannotRestoreOrRecreateTray` |
| No process Efficiency Mode change from tray creation | Static: explicit false argument; dependency conditional branch inspected; app build |

Baseline rerun: Passed: 300, Failed: 0, Skipped: 0 (`review-fix-baseline.trx`). TDD red: cached-loss test failed on a hidden window; both Shell-failure cases failed on inaccessible/minimized windows (`review-fix-red-loss.trx`, `review-fix-red-event.trx`). Green focused suite: 25/0/0. Final full suite: **Passed: 307, Failed: 0, Skipped: 0**, including all 25 lifecycle cases (`review-fix-tests.trx`, XML counters checked). Evidence root for this addendum is `C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec`.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=review-fix-tests.trx' --results-directory C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec

dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-20f60d60/src/ -bl:C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/review-fix-build.binlog
```

All native and performance **NOT RUN** rows remain unchanged. Still required: actual Explorer restart success/failure, foreground and resident failure recovery, subsequent close/retry, native event/resource cleanup across disable/exit and repeated cycles, tray accessibility, playback/download continuity, process exit and the specified 60-second samples. Overall acceptance remains **NOT ACCEPTED**.

Build evidence: clean Release x64 JIT build passed (exit 0, 1 existing external protobuf unused `empty.proto` import warning, 0 errors, 3:16.41; `review-fix-build.log/.binlog`). Final immutable-source build passed (exit 0, **0 warnings, 0 errors**, 20.44 seconds; same command with `review-fix-build-final.binlog`). Correct `SolutionDir` was supplied from the first invocation. Required ignored libmpv/ffmpeg assets were copied from the main source checkout, not the retained test layout.

Final `winui-code-review`: no additional blocker found in the changed source. Reviewed same-UI-thread event ordering, native/managed/command unsubscription, idempotent disposal, stale-session/exit guards, live health before hide, no new polling or process scheduling change. No XAML/resources/package references were changed. Normal compiler analyzers passed; dedicated WinUIAnalyzer remains absent/NOT RUN, and Native AOT was not tested. Applied TDD, code-testing, run-tests, winui-dev-workflow and verification-before-completion; focused tests used existing xUnit conventions (no code-testing-extensions bundle was supplied). `git diff --check` passed.
