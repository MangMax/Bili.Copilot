# .NET 11 exit: bounded independent reproduction

2026-10-31. **HOLD / native BLOCKED.** One independent, instrumented full-source
synthetic process reproduced the RC exit failure. Its retained process handle
returned **0xC000027B (3221226107)**, despite zero managed unhandled notifications.
The new gate rejected it. A local full-memory dump contains all **30 original
stowed stack addresses**, previously absent from the retained RC minidump.
The recorded first-chance exception is `System.Runtime.InteropServices.COMException`,
HRESULT `0x8000FFFF`, during dispatcher shutdown. **No production defect, fixture-only
cause, or .NET11-specific regression has been established. No production fix was made.**

The exact metadata, code-only stack and stage excerpt are in
[evidence.json](net11-exit-reproduction/evidence.json). Full dump, full trace,
build logs, generated fixture, deployed payload and screenshots remain ignored.
No second original-sequence run or controller variation was performed: complete
stowed bytes were obtained on the first attempt, meeting the stop condition.

## Source and execution boundary

- Local branch `t3/net11-exit-diagnostics` fast-forwarded from parent
  `f9271158c315ef7b1101050cb10560abdacc722a` to research
  `e04c311feb111f7e789e241ec7146c5edab39979`. Parent production files remain net10.
  RC delivery `e55180fb898db22d6f3e2f5049e83d90d9d11844` was read through Git objects.
- Diagnostic production source is an independent extraction of exact
  `f6ed3ec063df8701bb57115abb40c4f7bcffc386`. Kernel is exact
  `0ae5e4e000a41a07f6696746706df11c06e8835b`. This checkout initially had only the
  submodule Git pointer and staged deletions; the initial index diff was saved
  ignored, then the checked-in bundle restored the specified kernel. It is clean.
- Existing installed SDK `11.0.100-rc.3.26522.148` was selected by the extracted
  global.json and absolute `C:/Program Files/dotnet/dotnet.exe`; no SDK, runtime,
  application package version or global tool was installed/changed. Host-only
  builds completed: first 1548 warnings / 0 errors, final metadata-complete build
  1546 warnings / 0 errors. These are fixture warnings, not production warning counts.
  No 487-test run, full solution build, AOT build or additional analyzer migration.
- 625 production C# files compared: 619 equal after BOM/newline normalization;
  six differences are App offline entry/trace initialization, GlobalDependencies
  offline composition, FontCatalog's prior enumeration probe, MainWindow stage
  markers, PlayerWindow stage/callback markers, and TrayLifecycle dispose markers.
  Existing production exception handling is unchanged, including the pre-existing
  App.OnUnhandledException `Handled=true`. New listeners never set Handled, swallow
  an application exception, change a cleanup order, or call new shutdown behavior.
- All **134 resolved package identities and SHA512 values equal** the sealed RC
  player graph. Frozen payload has **901 files / 363 DLLs**; deployed DLL names and
  hashes match both directions. The only recipe consulted was the just-built
  fixture's recipe, to fill missing content without replacing existing payload;
  every added source was under this diagnostic root or NuGet cache. No old recipe
  or old layout supplied kernel DLLs. File hashes are in
  [frozen-files.json.gz](net11-exit-reproduction/frozen-files.json.gz), source hashes in
  [source-comparison.json.gz](net11-exit-reproduction/source-comparison.json.gz).
- Loaded `coreclr.dll` SHA256 is
  `46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`, identical to RC3.
  Diagnostic UI DLL is `8c3e8cb206cee9c779ce407bb97de492ef476dcb09da51b988ba2858db6fe6a8`.
  Its informational version contains the surrounding research checkout's `e04c311f`
  Git stamp; this is **not** the source identity claim. Extracted source bytes,
  expected substitutions, package graph and file hashes establish provenance.
- Native libmpv/ffmpeg input came from the existing preparation helper's exact
  `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets/.../x64/` files;
  only these binaries were copied, never a registered layout or user state.
  No protected Caa6a7be layout/login, PID9608, Dev registration, credentials or real
  business sending was used. No other worktree was launched, modified or cleaned.

## Gate repair and offline red/green

[prepare_runner.py](net11-exit-reproduction/prepare_runner.py) makes a new independent
runner from the sealed RC script. The exercised runner is ignored at
`artifacts/exit-reproduction/replay.ps1`, SHA256
`289165f01dc05b5a882c8c2fba6681505be161773f93c47108270c6fd5041b90`.
The retained Process.Handle is acquired before UI actions; target creation time,
canonical exe path, PID and deployment hashes are fixed then. WaitForExit reads
the **target** unsigned exit code, never the observer's code.

[exit_gate.py](net11-exit-reproduction/exit_gate.py) requires a known normal code,
retained handle, observed termination and complete bounded event querying. Unknown
code, query failure, timeout, forced termination, managed unhandled notification,
exact native fault or unresolved matching WER report cannot PASS. Acceptance is
named `PASS_BOUNDED`, not proof of indefinite future silence.

Application Error association uses XML EventData.ProcessId, exact canonical AppPath,
exact ProcessCreationTime, pre-launch RecordId bookmark and process-lifetime time
window. The provider Execution PID is ignored. WER is associated through an exact
fault's shared report ID plus package/path; unrelated fault reports and PID reuse
do not contaminate this process. An orphan matching report is unresolved/BLOCKED.
Queries run every 500 ms for 30 seconds after observed termination, recording UTC,
monotonic timestamps, query success and first visibility. Later exact evidence
revokes an earlier bounded result. Thirty seconds is a finite policy, not a proven
upper bound on WER publication latency.

The old `accepted.json` remains unchanged in RC Git history, raw SHA256
`42652c0186d209a7500488f47e7dc8c4ff00b8088c353d3c54b0b83d27453caa`.
It is not overwritten by the new verdict. Historical net10 “normal” records also
lack a target exit code: the test correctly refuses to retrospectively certify
them. The positive control is an explicit zero-code/complete-observation record.

`python docs/performance-acceptance/net11-exit-reproduction/verify_gate.py`:
legacy-acceptance mutation **10 run / 9 failed / 0 errors**; repaired gate
**10 run / 0 failed / 0 errors**. This is a behavioral mutation experiment, not
a deliberately broken import/build. See [gate-tests.json](net11-exit-reproduction/gate-tests.json).

| Requirement | Evidence |
| --- | --- |
| Sealed RC fault red; history preserved | `test_sealed_false_green_is_blocked_and_bytes_unchanged` |
| Normal zero-code positive control | `test_normal_zero_complete_observation` |
| Historical normal without target code rejected | `test_historical_normal_record_without_exit_code_is_not_certified` |
| Unknown/nonzero code rejected | `test_unknown_and_nonzero_are_never_pass` |
| PID reuse, unrelated PID/path, old event and shared unrelated report | `test_pid_reuse_unrelated_pid_path_and_old_event` |
| Delayed publication and orphan WER | `test_delayed_publication_revokes_provisional_pass` |
| Provider PID not faulting PID | `test_provider_pid_is_not_fault_pid` |
| Missing handle/query/termination/observation, forced or managed failure | `test_missing_handle_query_timeout_forced_and_managed_failure` |
| Canonical path and bookmark | `test_canonical_path_and_bookmark` |
| Actual new crash red despite managed-unhandled=0 | `test_owned_live_shutdown_crash_is_blocked_despite_zero_managed_unhandled` |

## One owned original-sequence attempt

Identity `Mang.BiliCopilot.ExitDiag.XBC1.original1`; PID **38208**, creation UTC
`2026-10-30T17:08:44.0226859Z`. Main HWND **11275372**, explicit-exit controller
**12848372**, final player/font HWND **10883616**. The controller remains an extra
fixture window, not claimed as a production feature.

Same preparation/Player15/Main4/20-cycle/font sequence, one player alive when
ExitMain invokes the original RequestExitAsync. The unchanged v3 observer completed
183 samples; this is **not an accepted performance run**. No comparisons or wins
are inferred. Player15 and Main4 passed. Twelve captured Player/Main/font images
were inspected: they show the synthetic silent-WAV surface and expected fixture
controls; no authenticated home or real video-quality acceptance follows.

All times below UTC on 2026-10-30 (local date is October 31); all stage rows use
OS thread **39912**, managed thread **2**. The evidence retains full timestamp
precision and monotonic counter values.

| Stage | UTC |
| --- | --- |
| Explicit exit, players=1 | 17:12:58.7517580 |
| Settings save start → complete | 17:12:58.7595866 → .7610261 |
| Player native destroy start → complete | 17:12:58.7640801 → .7838198 |
| DesktopWindowXamlSource dispose start → complete | 17:12:58.7887959 → .7896584 |
| All players close complete | 17:12:58.7910177 |
| Tray dispose start → complete | 17:12:58.7920254 → .7943438 |
| Tray/notification release start → complete | 17:12:58.7954580 → .7967996 |
| Main close start → complete | 17:12:58.8194441 → .8782410 |
| App.Exit start | 17:12:58.8788135 |
| Fixture controller Closed event | 17:12:58.8948448 |
| App.Exit returned | 17:12:58.9224134 |
| Dispatcher ShutdownStarting | 17:12:58.9292517 |
| FirstChance COMException / 0x8000FFFF | 17:12:59.2072423 |
| Application Error 1000 / record 6525 | 17:13:06.1033549 |
| WER 1001 / record 6526 | 17:13:12.9144788 |
| Retained handle observed terminated | 17:13:14.6387526 |
| Event observation finished | 17:13:44.7506704 |

No ShutdownCompleted marker. No instrumented focus/activation/padding callback
occurs after the explicit-exit marker. Earlier callbacks did observe closed/disposed
owners and their existing guards remained active. This does not exclude other
uninstrumented callbacks. Completion markers only establish that those calls
returned; they do not certify native release health or absence of swallowed
exceptions in existing cleanup code.

## Exception evidence and what remains unknown

First-chance record 628 contains only type/HRESULT and code metadata:
`WinRT.ExceptionHelpers.<ThrowExceptionForHR>g__Throw|41_0`, source sequence-point
line **121**, IL offset **26**, native offset **95**. It identifies the WinRT HRESULT
throw helper, **not the application caller**. Its Exception.StackTrace at the
first-chance event has one frame; no full current managed-thread stack was captured
by that listener. No message, body, URL, account, arguments or locals were logged.

[capture_owned.py](net11-exit-reproduction/capture_owned.py) attached only after
exact PID/executable validation under this checkout's new layout. It passes
exceptions back as DBG_EXCEPTION_NOT_HANDLED except the initial attach breakpoint,
sets DebugSetProcessKillOnExit(false), and captured the stowed second-chance event.
System DbgHelp wrote a full-memory dump; no system-wide WER registry changes,
global debugger installation or dump upload occurred.

Dump: `artifacts/exit-reproduction/original1/crash-full.dmp`, **1,550,491,329 bytes**,
SHA256 `eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d`.
[parse_dump.py](net11-exit-reproduction/parse_dump.py) reads its Memory64List with
an mmap, verifies PID/thread/exception and extracts exactly the stowed-address
array. It does not scan heap strings or guess a CLR object layout.

Stowed V2 SE02: thread39912, HRESULT8000FFFF, binary form1, nested LEO1 captured.
All 30×8 stack bytes are present. The following abbreviated chain is ordered as
stored, with the complete 30 addresses in evidence; these are **actual stowed
frames**, not stack-word guesses or the old dump's reporting unwind:

```text
combase.dll+0xc4063
CoreMessagingXP.dll+0xb17bc
CoreMessagingXP.dll+0x38ba2 / +0x3873b / +0x52fe3
CoreMessagingXP.dll ... message-dispatch frames
user32.dll+0x152e6 / +0x14b6c / +0x3f593
ntdll.dll+0x165d94
win32u.dll+0x1304
user32.dll+0x119df / +0x11968
CoreMessagingXP.dll ... +0xad141 / +0xa7761
Microsoft.UI.Xaml.dll+0x79566e / +0x5be83b
0x7ffe90e1034f [outside captured ModuleList; not symbolized as managed code]
```

Module versions/RSDS identities and actual loaded file hashes accompany the stack.
CoreMessagingXP12.6.4.0 outer fault +0xb17fc and combase10.0.26100.9711 match the
sealed RC's reporting signature; that alone does not establish identical causes.
One bounded 30-second official CoreMessagingXP PDB download obtained only
1,441,034 / 3,411,968 bytes and timed out (curl28); the partial PDB is rejected.
No repeated symbol download or claimed symbolic source-level resolution.

**Collection disturbance:** synchronous metadata writes and FirstChance listeners
alter timing; the native debugger pauses at events and full dump creation delays
termination. The inherited inner UI observer's 15-second disappearance check
reported `explicitExit=false`, while its `unhandledCount` was zero. That observer
timeout is not evidence of a production hang. The outer retained-handle gate then
observed the actual nonzero exit and exact AppError/WER pair, shared report
`babd1140-1684-4cc9-9890-6acb730a0d9b`, and correctly reported BLOCKED.

The reliable localization is **after App.Exit returned, during dispatcher shutdown,
following settings/player/tray/main cleanup and the fixture controller Closed event**.
Neither controller causality nor player destruction ordering is proven. There is no
basis for a minimal production patch yet; do not remove App.Exit, reorder destruction,
swallow COMException, change runtime/packages or blame ms-appx/Visor cancellation.

Missing evidence is now specific: resolve the final JIT address and nested LEO
using a matching RC3 DAC/SOS/ClrMD against this already-retained full dump; obtain
matching complete CoreMessagingXP/XAML/combase symbols to name the shutdown caller;
if the dump still cannot recover the originating managed caller, an independently
authorized future listener would need a code-only **current thread StackTrace** at
FirstChance (in addition to Exception.StackTrace), with its timing cost stated.
No new live run is needed merely to repeat the now-captured stowed signature.
Only after the owning callback/contract is established should a minimal lifetime
guard or fixture-only correction be proposed, with the identical original exit
sequence red before and target-code/event gate green afterward. Phase passes
must never override an exit failure.

## Cleanup, checks and delivery

Owned target38208, collector7960, observer launcher30904/interpreter37336 are absent;
the owned package registration is removed. Runner cleanup completed. Sixteen idle
MSBuild nodes from this task's initial build were stopped only after checking their
recorded parent chain (33584→32064→workers), creation-time window and exact RC3 SDK
executable path; no global build-server shutdown was used. Player yield recorded
`released=true`; main and outer yield recorded `released=false` (no remaining lease),
preserved exactly. Cleanup is not clean-exit certification.

Read and applied the requested AGENTS/docs/agents and debugging, WinUI workflow/UI
testing/review, code-testing/run-tests, code-review and verification skills. Reviews
were performed directly, honoring the explicit no-subagent instruction. Standards
review: no tracked production diff, no new suppression/package/runtime changes;
Spec review: historical bytes intact, exact owned native failure is non-green,
full dump stays ignored, one run only, no controller workaround or root-cause claim.

Reproducible offline checks from repo root:

```powershell
python docs/performance-acceptance/net11-exit-reproduction/verify_gate.py
python docs/performance-acceptance/net11-exit-reproduction/provenance.py
python docs/performance-acceptance/net11-exit-reproduction/parse_dump.py
python docs/performance-acceptance/net11-exit-reproduction/exit_gate.py artifacts/exit-reproduction/original1
git diff --check
```

The gate command intentionally exits **2 / BLOCKED**. Provenance/parser commands
verify evidence, not clean exit. Preparation scripts describe a fresh isolated
diagnostic checkout; do not reexecute them over the retained evidence or treat
them as authorization for another application replay. Two prelaunch preparation
failures (archive long paths and recipe PackagePath child parsing) and an incremental
build adding the final tray marker were retained locally; none was an extra native run.

**Preserved acceptance limits:** old native BLOCKED, #49 pause FAIL, AOT BLOCKED,
authenticated home unverified. Local commit only: no push, PR, issue update or
parent-branch integration. This report does not promote RC3 to accepted production.
