# RC PlayerInteractivePanel timer lifetime verification

2026-10-31. **Timer lifetime fix verified for the bounded RC3 x64 Release JIT
offline sequence.** Two clean-fixture complete exits returned target code0 and
passed the exact-process event gate. Fix commit:
`8ea8af2efadffca37fb907689ba5360d8c8492d3`.
This report concerns the recovered `PlayerInteractivePanel.OnTempControlTimerTick`
callback, not all historical net9 crashes, #49 pause behavior, AOT or authentication.

## Source and minimal change

Independent `t3/player-timer-exit-fix` started at parent `f9271158`, fast-forwarded
research `f0ded61074e03edc477fe53538c3aab819aac11d`, then selected the exact RC3
SDK/TFM changes of `f6ed3ec` as local commit `72eb1640`. Parent checkout was only
read: its sole panel diff was Stop in `OnControlUnloaded`. No parent file/branch,
push, PR or issue was modified. Kernel is detached, clean
`0ae5e4e000a41a07f6696746706df11c06e8835b`; its initially unexpanded index was saved
before restoring the checked-in dependency bundle.

Relative to RC production `f6ed3ec`, the only production source change is
`Controls/Core/PlayerInteractivePanel.xaml.cs` (31 insertions, 4 deletions):

- Own Loaded/Unloaded handlers, registered once in the constructor; a managed
  `_isInteractionActive` flag makes attachment/detachment idempotent.
- Loaded starts the existing real timer. Unloaded clears the flag, stops it,
  then invokes the unchanged interaction cleanup.
- Tick rejects inactive lifetime **before** the dependency-property ViewModel
  getter. No COM catch, native `IsLoaded` getter or replacement timer is used.

`CleanupInteractionState`, shared base classes, player/controller/window shutdown
ordering and other business logic are unchanged. Existing 2.4.1, LatestPatch,
WIC, Pinner, header lifecycle and LCS fixes remain intact. This is a local timer
lifetime fix, not a runtime/package downgrade or a new framework workaround.

## Why Stop alone was insufficient

The exact frozen `Richasy.WinUIKernel.Share.dll` was hash-checked against the
historical manifest and inspected with `System.Reflection.Metadata`.
[base-lifecycle-il.txt](net11-player-timer-fix/base-lifecycle-il.txt) shows
`LayoutUserControlBase.OnUnloaded` removes **both** Loaded and Unloaded handlers
before calling the override. The generic ViewModel getter invokes GetValue.
The red source was saved before any modification in
[red-panel.cs.txt](net11-player-timer-fix/red-panel.cs.txt).

Stop-only was the first and only production variable in the first fixture. Its
real panel was inserted into and removed from a live WinUI Panel twice; its
actual DispatcherQueueTimer was observed, never replaced by an adapter.

| Requirement / exact case | Stop-only | Local ownership fix |
| --- | --- | --- |
| `LoadedTimerRuns1`, `LoadedTimerTicks1` | PASS | PASS |
| `UnloadedTimerStops1`, `NoReadAfterUnload1` | PASS | PASS |
| `LoadedTimerRuns2`, `LoadedTimerTicks2` | **FAIL**: Loaded=2, running=false, reads 3→3 | PASS |
| `UnloadedTimerStops2`, `NoReadAfterUnload2` | PASS, but timer never restarted | PASS |
| `LateTickDoesNotReadClosedVm` | **FAIL**: read probe 3→4 | PASS: 6→6 |

The late callback is explicit boundary injection through the real callback after
`PlayerViewModel.CloseAsync`, not a claim that the OS naturally delivered a tick
after Stop. Natural ticks and two real unload/remount events are separately checked.
This red justifies the local ownership and pre-getter guard; changing the general
base or swallowing exceptions would not be justified by these tests.

The first process `stop1` PID31716 exceeded the runner's initial 30s startup
readiness allowance. Its nine behavioral results completed and were recovered,
but the exit gate correctly recorded unknown code / not terminated, then owned
cleanup forced termination. It is **not** a clean exit result. All records remain;
readiness was corrected to 120s, outside the measured/tested sequence.

## Native red and diagnostic green

Historical red remains at the original `t3-bc1a3769` path, never overwritten:
PID38208, retained target exit `0xC000027B`, AppError6525/WER6526, full dump
1,550,491,329 bytes, SHA256
`eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d`.
The hash was freshly recomputed. The existing recovered stack and source mapping
in [full-dump analysis](net11-exit-full-dump-analysis.md) are retained; no new
heap/credential inspection was performed.

Diagnostic `green1` PID24844 completed Player15, Main4, the unchanged 20-cycle/font
preparation, one live player and the original extra controller. Retained handle
exit=0; complete 30s event observation: no exact-PID/path/creation-time AppError,
linked or unresolved WER. The result is **PASS_BOUNDED**, not indefinite immunity.
The same gate continues to reject historical red.

The code-only trace records **17 panel instances, 18 starts, 18 stops, 42 reads**.
All final states are stopped; no read follows Stop without another Loaded/Start.
The final instance has a read after Dispatcher ShutdownStarting and **before its
Unloaded/Stop**, followed by ShutdownCompleted. Thus this evidence supports
unload cancellation, not an assertion that shutdown contains no timer callbacks.
No `0x8000FFFF` first-chance event was recorded in this diagnostic run.

The diagnostic read counter keeps panel references and writes trace records;
these can affect lifetime/timing. Therefore its no-crash result alone is not
production certification. Final fixture removes those counters/listeners and all
MainWindow/PlayerWindow/TrayLifecycle diagnostic markers. Its panel bytes exactly
match production. Only the established offline App/GlobalDependencies composition
and FontCatalog probe differ after BOM/newline normalization. The separate final
test checks real expired-volume state changes, actual timer IsRunning and real
mount events; it adds no code to the production panel.

| Run | Target PID | Target code / gate | Behavior |
| --- | --- | --- | --- |
| stop1 | 31716 | unknown / BLOCKED, forced cleanup | timer 6 pass / 3 fail; startup deadline error retained |
| green1, diagnostic | 24844 | 0 / PASS_BOUNDED | timer9, Player15, Main4, font PASS |
| final1, clean | 40300 | 0 / PASS_BOUNDED | timer9, Player15, Main4, font PASS |
| final2, same clean binary | 10064 | 0 / PASS_BOUNDED | timer9, Player15, Main4, font PASS |
| controls1 | 40740 | unknown / BLOCKED, forced cleanup | Controls7 PASS; CLI refused system-key close |
| controls2 | 22968 | 0 / PASS_BOUNDED | Controls7 PASS; explicit owned close succeeded |

The initial Controls runner omitted WinApp's `--allow-system-keys` opt-in for
Alt+F4. Its refusal was preserved. By the subsequent close attempt its known HWND
was already gone after cleanup; no alternate window was targeted. A new identity
reran the same seven checks with the explicit opt-in and passed retained-exit
verification. Neither failed runner attempt is relabelled as a clean native exit.

Controls cases: `PartitionSwitchPreservesCacheOwnershipAndCursor`,
`PartitionEmptyRefreshPreservesUndoAndStopsNotification`,
`PartitionLateRefreshDoesNotBlockChildOrPublishFailure`,
`RealControlsRefreshUndoAndRemount`, `PopularHeaderUndoSurvivesRemount`,
`PartitionHeaderUndoSurvivesRemount`,
`CoverResizeUnloadFailureAndConsecutiveRecovery`. Main cases:
`NativeMainCloseHidesToTray`, `RestoreSameMainHwnd`,
`DisableTrayRestoresHiddenMain`, `ClosingPlayerPreservesIntentionalResidency`.
All Player names and raw details are retained in
[evidence.json](net11-player-timer-fix/evidence.json).

## Builds, artifacts and bounded release scope

Existing checksum-pinned SDK under
`t3-d48c749d/artifacts/dependency-upgrade/sdk11` was reused via absolute dotnet and
process-local DOTNET_ROOT. No machine-wide installation or global SDK change.
All app/test builds use this isolated SDK. The small read-only IL helper initially
used the already installed RC3 SDK; it installed nothing and built no application.
The SDK archive's original SHA512 was freshly rechecked. Fixture preparation on
this Windows host also produced ANSI-encoded diagnostic panel / offline App
sources; raw hashes retain that distinction. The final production panel is an
exact byte copy, and the one non-ASCII App logging literal was verified present
unchanged in both fixture and production DLLs. No source encoding change is
included in the production fix.
SDK `11.0.100-rc.3.26522.148`, self-contained x64 runtime
`11.0.0-rc.3.26522.148`; Release is JIT, `PublishAot=false`.

- Latest-source suite: **Passed: 487, Failed: 0, Skipped: 0**, net11.0 Release,
  `artifacts/timer-fix/tests/timer-final.trx`.
- Production Debug Rebuild and Release JIT build: 0 errors, 1 existing unused
  proto import warning each. The first Debug attempt lacked native assets; its
  log/output is frozen separately. Only existing libmpv/ffmpeg files from the
  same fixture/native preparation source were copied into this checkout before
  the successful clean rebuild; no package/source fix was hidden in that retry.
- Stop-only/diagnostic/final full fixtures each freeze 901 files / 363 DLLs.
  All **134** package identities and SHA512 values match the original RC graph.
  Each freeze retains output, source hashes and assets; every deploy excludes
  recipes and compares DLL names and SHA256 in both directions.
- Production Debug/Release complete output snapshots, test output, all build
  logs, raw native events, runtime modules, observer/collector records and
  screenshots remain in ignored `artifacts/timer-fix` and
  `artifacts/exit-reproduction`; no historic snapshot was overwritten.

The additional complete final runs retain the same pre-exit workload for a matched
clean-fixture comparison. Their incidental samples are not performance acceptance;
no CPU, leak or timer-work reduction is claimed. #49 pause FAIL, AOT BLOCKED and
authenticated home unverified remain separate. The normal production authenticated
entry point and parent's net10 artifact are not certified by this RC offline test.

## Replay and review

Use a fresh checkout and the existing exact isolated SDK. From repository root:

```powershell
python docs/performance-acceptance/net11-player-timer-fix/prepare-stop.py
python docs/performance-acceptance/net11-player-timer-fix/add-timer-checks.py artifacts/timer-fix/stop-only/fixture
# build-fixture.ps1 -Variant stop-only -SdkRoot <existing exact RC3 directory>
python docs/performance-acceptance/net11-player-timer-fix/prepare-variant.py green
python docs/performance-acceptance/net11-player-timer-fix/prepare-variant.py final
# Build each variant with build-fixture.ps1, which immediately freezes it.
# replay.ps1 -Fixture <frozen fixture> -Run <new unique name> -Capture
python docs/performance-acceptance/net11-player-timer-fix/verify.py
git diff --check
```

`verify.py` audits retained records, frozen file/graph identities, exact outcomes,
the historical-red rejection and lifecycle trace. It is not a substitute for a
fresh UI run. `replay-controls.ps1` uses the same owned retained-handle/event gate
for the existing Controls harness. No old registration/login is reused.
After the native runs, the replay entry point additionally invokes
`assert-timer.py` before Player/Main, so a normal exit cannot mask a behavioral
red in a future replay. This reporting-only assertion was checked against the
retained Stop-only result (exit2, exactly the three expected failures) and final2
(exit0), as recorded in `behavior-gate-check.json`; no app code/build changed and
no additional native pass is claimed for that runner-only addition.

Manual Standards/Spec and WinUI review performed without subagents: native UI
access stays on the dispatcher, the guard precedes GetValue, no new suppression or
package is introduced, no common cleanup/base or XAML binding change is included.
The configured production analyzers ran; no new WinUIAnalyzer package was installed.
The old fixture's short controller clips its Continue button in the results image;
that is not a polished UI/visual acceptance claim and was not expanded into this fix.

Final `verify.py`: PASS; historical red stays BLOCKED, all expected passing runs
return0, all 487 TRX outcomes are passed, package graphs and frozen hashes match.
`git diff --check` passes. Final sync with local `feat/performance-45` reports
Already up to date; parent remains `f9271158` with its original sole Stop hunk.

[cleanup-audit.json](net11-player-timer-fix/cleanup-audit.json) records all six owned
app PIDs, six collectors, observer launchers/interpreters absent, and all six
registrations removed. No dotnet build processes remain. Player/Main/Controls
inner yields record `released=true`; every outer yield records `released=false`
because no lease remained. Those values are preserved exactly. The desktop is
released. No protected Caa6a7be registration/layout/login, PID9608, Dev,
credentials or authentication identity was touched.
