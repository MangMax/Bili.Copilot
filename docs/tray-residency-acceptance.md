# Tray residency acceptance — T3 / #42

Date: 2026-10-29 (Asia/Shanghai). Spec: [#39](https://github.com/MangMax/Bili.Copilot/issues/39); task: [#42](https://github.com/MangMax/Bili.Copilot/issues/42).

**Overall: NOT ACCEPTED — partial runtime acceptance completed on 2026-10-30.** An authenticated normal main window is now available. The final-runtime section below supersedes the historical login blocker and old test counts. Remaining history, download and accessibility coverage prevents closing the acceptance ticket as passed.

## User-assisted playback exit verification — 2026-10-30

**PASS (manual interaction + independent process check).** The user played a video in the isolated test app, manually chose the tray menu's Exit Application command, and confirmed that both player and main window exited. Immediately afterwards, the parent checked running BiliCopilot.UI processes by the isolated executable path (`*BiliCopilot-tray-spec*`); no isolated test process remained. Existing user installations were not stopped.

This resolves the active-playback exit acceptance gap for that normal interaction. It does not turn the earlier UIA invocation into a passing automation result, prove the cause of that failed invocation, or verify final server-side history position. History, download continuity and the remaining accessibility/failure-injection cases below are still incomplete.
## CPU profiling and acceptance follow-up (2026-10-30)

Report-only follow-up on integration `cb6cd9ad96d28c0f0636bd58eb8e1116411137eb`; merging `t3/tray-residency-setting` returned already up to date. No application changes, rebuild, repeated test suite, or repeated ten-minute study. The previous 307-test result remains prior evidence, not a new run. Production manifest SHA256 still `058D0578A5EDF4D9BD6AFB6624BE1C62EF55342EC7FB13283F4375B31CF3FFFE`.

Evidence is outside Git at `C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/t3-evidence/profiling`. PID 9608, HWND 1713510, executable path and registered package location were verified against the retained isolated identity before use. No authentication material was extracted, no app data reset, and no Explorer restart occurred.

### Bounded profiling results

Installed `dotnet-trace` 10.0.750501 into the external evidence `tools` directory only. Collected two 30-second EventPipe traces with `dotnet-common,dotnet-sampled-thread-time`, plus Speedscope exports. `profile.ps1` reused the prior native harness and recorded 32 process/visibility/session observations per phase. All observations were unlocked, debugger-free and at the intended visibility. Both phases used Settings/Application with no open player or initiated download; notifications remained OFF. Unlike the earlier fresh-process idle study, this retained process had already experienced playback before profiling.

| Measurement | Visible Settings | Hidden Settings |
| --- | ---: | ---: |
| Process-counter interval, including collector overhead | 32.208 s | 32.104 s |
| Process CPU time | 7.328 s | 3.922 s |
| CPU, percent of one core | 22.75% | 12.22% |
| Raw managed thread-stack samples | 60,596 | 91,368 |
| UI-thread samples | 10,099 | 15,228 |
| UI samples ending at native WinUI application loop | 10,086 (99.87%) | 15,227 (99.993%) |

`analyzer/Program.cs` reads raw SampleProfiler events with TraceEvent 3.1.23; `visible-stacks.json`, `hidden-stacks.json` and `summary.json` retain counts and calculations. **These samples include waiting/unmanaged time; their percentages are not CPU shares.** Five waiting/polling managed threads contribute 50,495 visible and 76,140 hidden samples (about 83.33%). The hidden remainder includes one `PlayerInteractivePanel.OnTempControlTimerTick → IDependencyObjectMethods.GetValue` sample (1/91,368 overall, 0.0011%). Visible UI samples additionally include ten `WindowMessageMonitor.NewWindowProc` and three pointer/interop samples. These small counts do not establish a managed hot loop.

Thread CPU deltas narrow the missing attribution: hidden TID 29504 consumed 1.3125 s, while UI TID 12692 consumed 1.15625 s. TID 29504 has no managed stack samples; the UI stacks mostly stop at `IApplicationStaticsMethods.Start`. TIDs 14256 and 21240 consumed 0.4375 s and 0.15625 s respectively. Counter intervals and profiler sample intervals differ slightly; these are not precise per-stack CPU percentages. Native symbols/stacks are required to attribute the cost further.

WPR was installed and initially not recording, but `wpr -start CPU -filemode` failed with `0xc5585011`, “Failed to enable the policy to profile system performance.” The session is not elevated. No Windows policy/privilege changes or additional native collection retries were made. The managed collector was the bounded alternative. Native CPU attribution therefore remains **BLOCKED BY TOOL PERMISSIONS**, not attributed to notifications, chapter rendering, GPU work or the tray library.

The hidden measurement reproduces sustained idle CPU. The visible interval includes initial tracing/provider overhead and pointer callbacks; it is not a clean performance-benefit benchmark. Tracing raised visible private bytes by 23.39 MiB; hidden private bytes fell 1.37 MiB. Do not use these instrumentation-contaminated endpoints as memory savings or leak evidence. No heap dump, retained-object analysis, forced GC or working-set trim was performed. The earlier controlled ten-minute study remains authoritative for its bounded no-growth observation.

### Ranked follow-up proposals, outside the tray change

1. **Native CPU attribution — strongest measured opportunity, medium investigation cost.** Capture 30 seconds of sampled native stacks in an already permitted elevated/dedicated diagnostic session, concentrating on TIDs 29504 and 12692 (rediscover IDs after restart). Resolve WinUI/native worker symbols before choosing an optimization. Current evidence supports the investigation, not a production scheduling/rendering change.
2. **Player interaction timer lifetime — direct stack plus source evidence, small likely implementation scope.** The hidden trace executes a player-panel timer after all native player windows have closed. `PlayerInteractivePanel.xaml.cs` starts its 500 ms `_tempControlTimer` on load; unload calls `CleanupInteractionState`, which stops only `_holdTimer` and `_tapTimer`. This code predates this follow-up and is present at `1664ad8`. Propose a separate unload/reload lifetime test, stop/restart handling, then verify callbacks cease and old panels can be released. One sample cannot quantify CPU or retained bytes. It also cannot explain the earlier fresh-process idle CPU measured before playback occurred.
3. **History observability — direct acceptance gap, small-to-medium investigation cost.** Add a separately reviewed diagnostic seam that distinguishes skipped reports, request completion and service result without logging credentials. Current absence of an exception does not establish successful history persistence. Notification toggle behavior and chapter rendering remain unmeasured candidates from the earlier report, not new optimization findings.

### Remaining acceptance matrix: actual follow-up observations

| Check | Result and limit |
| --- | --- |
| History after sustained playback | **NOT VERIFIED.** One ordinary public 2:17 video was played through its endpoint. Reviewed frames show progression and endpoint 2:17/2:17. After native close, the actual history page still displayed no recent video history (`player.png`, `player-late.png`, `history-after-long.png/.json`). This weakens the short-play-only explanation but does not establish a server threshold or tray defect. |
| Paused close/history | **PARTIAL.** Space input followed by two separated captures showed the same mid-video frame (`pause-final-a/b.png`); native player close completed and main could be restored. History remained empty after refresh (`history-after-pause.png`). Exact paused position/state and final saved position were not independently exposed by UIA, so final-position acceptance is not passed. |
| Immediate-open close | **PARTIAL, not the immediate-close edge.** The measured close occurred 10.286 s after invocation because UI automation was delayed (`exit-cases.json`). Player disappeared and hidden main survived. This cannot validate closing during initial loading or zero-position history. An earlier external-script scalar-array error was corrected; no app fix followed. |
| Explicit exit during playback | **PASS via user-assisted native interaction.** User manually clicked tray Exit while playing and confirmed both windows closed; parent verified no isolated process remained. Earlier UIA invocation remains inconclusive, not a passing automated test. Final history progress is still unverified. |
| Keyboard tray menu | **NOT VERIFIED.** HWND-targeted `home enter` reported successful posting but did not synchronously restore the hidden main. The popup later disappeared; the follow-up input targeted a stale HWND. No reliable keyboard action/result pair or Narrator evidence was obtained. |
| Download continuity and exit | **NOT RUN.** No download was initiated: player UIA exposed native window controls but not the download/panel controls, including after restore/hover and targeted keyboard alternatives. The reviewed implementation starts a separate BBDown process; future acceptance must independently observe app and downloader lifetimes. No task/artifact was created or removed, and no bulk download or credential-bearing command was extracted. |

History source review: both resolvers skip disabled reporting, private playback, position zero, and paused non-exit updates. Exit saves bypass the paused guard and use a four-second request cancellation token. The kernel posts `aid/cid/progress/type` and awaits its HTTP abstraction; no local final-history readback is performed there. Matching log inspection found no progress-report exception; no request payload, token or cookie was copied. These are preexisting behaviors, not proof that the server accepted a report or that the retained reporting flag was enabled.

Final cleanup closed only this session's test players through their native close path and restored the same authenticated main (PID 9608, HWND 1713510, visible, not minimized, no player window; `final-state.json`). Tray/integration settings remain ON and notifications OFF. Cumulative memory/handles after UI automation and repeated playback are not a controlled leak comparison. Overall acceptance remains **NOT ACCEPTED**. Parent owns further integration/review and cleanup; no push, PR change or issue closure was performed.

## Final runtime acceptance (2026-10-30)

Final application source: `9eb207a5f30b0e766192745f1632a35a91b48c65`, based on requested integration `1664ad8dd5cb2c0ded350000a68933870394b411`. The integration was already merged before this report. All evidence referenced in this section is local under `C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/t3-evidence/final`; screenshots and package/user data are not committed.

The retained isolated identity `Mang.BiliCopilot.TrayAcceptance.T3_y1mf40g4r9srg` was rebuilt and deployed without uninstall/reset or `--clean`. The temporary isolated manifest was restored in `finally`, byte-for-byte (SHA256 `058D0578A5EDF4D9BD6AFB6624BE1C62EF55342EC7FB13283F4375B31CF3FFFE`). Normal AUMID launch used retained login and reached the main window, without a login bypass or debug waiter. Tested isolated UI DLL SHA256: `D721C5C6D7011EB4739D558BA5CA3BF957913935830B89B3D5EB088A69F2DC65` (`fixed-tested-binary.json`). The prior stale HWND disappearance is not assigned a speculative root cause.

### Confirmed defect and verification

Repeated activation restored the primary window but left windowless secondary processes alive. Before the fix, 28 such processes held 1,459,736,576 private bytes and 30,402 handles in aggregate. The native regression failed with a secondary still present after ten seconds (`activation-red.json`). The redirect continuation called WinUI `Exit` from a worker thread. Commit `9eb207a` queues that call onto the instance UI dispatcher. Three activation repetitions then passed; the strengthened final regression also minimizes the owned main HWND before each activation to prove activation actually restores it (`activation-final-green.json`). This is a confirmed process-lifetime leak, distinct from idle residency in the primary process.

Full post-fix tests: **307 passed, 0 failed, 0 skipped** (`post-fix-tests.trx`, `post-fix-counters.json`). Final isolated and production Release x64 JIT builds: **0 warnings, 0 errors** (`isolated-build-ready.log`, `fixed-production-build.log`). Native AOT and a dedicated WinUIAnalyzer run were not performed. The initial missing-submodule build and external protobuf warning are retained in earlier logs; they are not the final build result.

### Native runtime coverage

| Check | Observed result / boundary |
| --- | --- |
| Toggle, persistence, disabled close | PASS: OFF persisted across normal restart; disabled close exited the isolated primary; enabling recreated the tray. Keyboard Space toggled OFF/ON. Clean-install default was not tested because retained data was preserved; default false remains automated/static coverage. |
| Close, restore, minimize, repeated launch | PASS: same primary HWND restored, including minimized activation. Twenty post-fix close/launch cycles checked secondary process cleanup (`cycles-results.json`, `cycles.csv`). |
| Tray menu | PASS for accessible labels and UIA invocation of show/exit. Native popup keyboard injection reported `no_interactive_desktop`; keyboard menu navigation and Narrator remain NOT VERIFIED. |
| Explicit exit | PASS: actual tray Exit removed the primary and its windows (`explicit-exit-fixed.json`); subsequent normal launch succeeded. Exit during active playback/download remains NOT RUN. |
| Shell recreation | PASS for twenty targeted registered `TaskbarCreated` messages sent to the app's own tray window, retaining hidden residency. This is synthetic message recovery, not a real Explorer restart or a forced Shell registration failure. Explorer was not restarted. |
| Last player closes | PASS with integrated operation OFF and ON: close main into residency, close the actual last native player, main remains hidden (`player-coordination-results.json`). |
| Playback continuity | Short real playback continued while main was resident; reviewed before/after frames for integration OFF show advancing content. This is not a long playback/network endurance test. |
| History | NOT VERIFIED: after the short playback cases the actual history page displayed no recent records (`history-final.png/.json`). Final position persistence is not established; cause is undiagnosed. Paused and immediate-close history cases remain NOT RUN. |
| Downloads | NOT RUN: no actual download continuity or exit-during-download evidence. |

The earlier pre-fix `baseline-cycles-results.json` checked primary restoration only and missed secondary ownership; it must not be counted as a complete pass. Post-fix twenty-cycle private bytes increased only 48 KiB, handles 23 at the immediate endpoint; this short stress result is supplementary and does not establish leak freedom.

### Warmed visible–hidden–visible memory study

Authoritative data: `strict-idle.csv/.json`, `strict-idle-status.json`, `strict-idle-analysis.json`, and `strict-idle-chart.html`. Same primary PID 9608, same idle Settings/Application page, no playback/download, no debugger, notifications persisted OFF **before restarting** the isolated application. This restart matters because the existing notification timer is not stopped merely by changing the master toggle. The process was already warmed by the preceding exploratory run. No forced GC or working-set trimming was used. WTS session state was measured as unlocked throughout; foreground focus is recorded metadata, not an exclusion condition.

| Phase | Duration | Private bytes (MiB), start → end | Working set (MiB), start → end | Handles, start → end | CPU (% of one core) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Visible before | 69.59 s | 248.51 → 248.53 | 366.16 → 365.32 | 3180 → 3168 | 14.93% |
| Hidden resident | 604.99 s | 249.43 → 248.32 | 366.08 → 167.17 | 3169 → 3156 | 13.54% |
| Visible restored | 65.00 s | 248.48 → 248.40 | 168.31 → 154.83 | 3168 → 3167 | 13.80% |

All 122 hidden samples were actually hidden. Nominal cadence was five seconds (median 4.996 s), with a maximum 45 s host scheduling/tool gap and subsequent clustered samples; timestamps are retained rather than pretending perfectly uniform sampling. During the last five hidden minutes, private-byte range was only 40 KiB, fitted slope approximately −11.4 KiB/min, and handles remained 3156. **No sustained private-byte or handle growth was observed in this bounded run. This does not prove zero leaks over longer use.**

Private allocations were essentially unchanged between the warmed visible endpoints. Working set fell substantially, but remained low and fell further after restoration: this is not evidence that hiding frees allocations or that the tray feature alone caused the reduction. OS residency/aging and other desktop load were not experimentally isolated. Hidden CPU remained about 13–15% of one core, providing no clear idle CPU saving in this run. CPU percentages are not percentages of total multicore capacity.

The app remained online. Periodic established-TCP counts were zero; process I/O counters showed no read/write growth, but these are not precise network-traffic measurements or proof of no network work. Managed GC heap was **not measured**: no existing easy noninvasive collector was installed. A read-only thread CPU snapshot identifies active thread IDs but supplies no stacks or causal attribution (`hidden-thread-cpu.json`).

The earlier `long-idle.*` exploratory run contains 36 samples with mismatched intended hidden state (the main became visible/minimized, then was closed again). Its data and correction are retained, but it is not the authoritative hidden comparison. Earlier short foreground/resident runs are also superseded. Playback tests occurred only after both idle studies, so their allocations are excluded from these results.

### Optimization opportunities, separate from acceptance defects

The confirmed secondary-process leak was fixed. The remaining measurable opportunity is sustained idle CPU; profile its actual call stacks before changing rendering, polling, or timers. The existing master notification toggle only persists its value, while an already-started timer has no stop path and the video-moment callback checks the per-feature toggle. That is a control/behavior concern for separate discussion, not an optimization silently included here. `ChapterSegmentBar` subscribes to `CompositionTarget.Rendering` until Unloaded without a visibility guard; it is only a profiling candidate because actual hidden-window render ticks and its contribution have not been measured. Neither candidate is a confirmed cause of this run's CPU or a demonstrated allocation leak.

The normal authenticated isolated main was restored after testing (PID 9608, HWND 1713510); test-package tray and integrated-operation settings are ON, notifications OFF. No authentication tokens were inspected/copied. Remaining native acceptance gaps above require follow-up; this report does not approve closing #42.

## Historical acceptance record (superseded where noted above)

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
