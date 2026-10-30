# .NET 11 RC final-source exit crash: bounded offline diagnosis

2026-10-31. **Native verdict remains BLOCKED / HOLD; root cause is not established.**
No application replay, runtime/package change, build, performance sampling, authentication,
desktop interaction, deployment, push, PR or issue operation was performed.
#49 pause remains FAIL; AOT remains BLOCKED; authenticated home remains unverified.

## Scope and identity

Parent `f9271158c315ef7b1101050cb10560abdacc722a` remains net10. RC delivery
`e55180fb898db22d6f3e2f5049e83d90d9d11844` was read through Git objects, not merged
into the parent. Production `f6ed3ec063df8701bb57115abb40c4f7bcffc386` differs from
the parent in precisely five SDK/TFM configuration files. This investigation adds
only this report, small diagnostic scripts and whitelisted evidence.

The authorized dump was located using `rg --files --hidden --no-ignore -g crash.dmp`
within the explicitly supplied production artifact root. Exact path:

```text
C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-66927834/artifacts/net11-primary/f6ed3ec063df8701bb57115abb40c4f7bcffc386/native-crash/crash.dmp
```

Size **38,814,879 bytes**, SHA256
`07772c50693ad6849f387f11e21d3e327de453121d4c76432ec778dbe66f260f`.
The original stays ignored and was not copied into this checkout, committed or uploaded.
No other CrashDumps directory or old net9 dump was inspected. Reads in the source
worktree were restricted to the authorized artifact subtree; no files there changed.

The local kernel initially contained only its Git pointer: all tracked files were
staged as deleted, no unstaged or untracked files. Recovered the specified existing
bundle into a local diagnostic ref and checked out detached
`0ae5e4e000a41a07f6696746706df11c06e8835b`; final submodule status is clean.
No parent worktree was updated. Delivery is a local commit for the parent to review.

Read first: AGENTS.md, docs/agents, systematic-debugging, diagnosing-bugs,
winui-code-review, verification-before-completion, RC report/README/summary/raw
native-verdict and runner results, PR43 integration exit code, PR48 cancellation,
Pinner/WIC guards, and `new-runtime-capability-adoption-review.md`.
Live reproduction phases of the diagnostic skills were deliberately excluded by
the explicit offline-only instruction. No claim of a reproduced or fixed bug follows.

## What the dump actually contains

[evidence.json](net11-exit-crash/evidence.json) retains only structured metadata,
module identities, code addresses and exception fields. The scripts never scan or
print arbitrary heap strings, exception text, credentials, arguments or locals.

- Header: x64; flags `0x200121`; PID 11436; dump timestamp
  `2026-10-30T16:27:25Z` (second resolution). Stream types include ThreadList (3),
  ModuleList (4), MemoryList (5), Exception (6), SystemInfo (7), ThreadInfo (17 is
  **not present**), UnloadedModuleList (14), MiscInfo (15), plus 13/21/22/24.
  The directory also has unused type-0 entries. No Memory64List (9).
- MemoryList contains 50,759 captured ranges. Two additional ThreadList stack
  descriptors are also included in the reader (50,761 total descriptors); they
  do not cover the missing stowed stack. This is not a full-memory dump.
- Exception stream: thread **38200**, `0xc000027b`,
  `CoreMessagingXP.dll+0xb17fc`; parameters point to one stowed record.
- That record is **SE02 / V2**, size 56, binary form, actual thread **38200**,
  HRESULT **0x8000ffff**, exception address **combase.dll+0xc4063**.
  The bit-field thread value is restored by shifting left two bits, not mistaken
  for a second thread 9550. Layout follows the
  [Microsoft V2 definition](https://learn.microsoft.com/en-us/windows/win32/wer/stowed-exception-information-v2).
- Its original stack is declared as **30 × 8-byte addresses** at
  `0x2247edd20f0`. **None of these 240 bytes is covered by the captured MemoryList**,
  even after allowing adjacent ranges to be joined. Public symbols cannot restore
  these missing stack bytes.
- Nested type **LEO1** (`0x314f454c`), object `0x2247edd10d0`, is present, but its
  vtable pointer leads to an address outside the recorded module ranges; the full
  first three vtable slots are not captured. This is not a decoded managed
  exception type, message or managed stack. No guessed object-layout dereference
  or arbitrary-memory search was used to infer one.

Thus the outer stowed record and language-object pointer survive, but the original
throw-site stack and usable language-exception decoding do not. The HRESULT agrees
with WER; it does not identify a source-level cause.

## Fault-thread native stack, symbols and code lines

Used the already installed **Python 3.13.17**, **curl 8.21.0**, and system **DbgHelp
10.0.26100.9549**, SHA256
`2d2ec94891162a61d86789133623b47d64d74c15d6be99473b25ad7a6468cf39`.
No cdb/dumpchk/WinDbg executable was found in PATH, the checked SDK/tool locations
or WinDbg package location. No global installation or registry change was made.

`StackWalk64` used the exception CONTEXT, captured memory and PE exception tables;
missing image sections were supplied only from matching local images. Timestamp,
SizeOfImage, RSDS GUID/age and SHA256 are recorded in evidence. No stack-word scan
was passed off as an unwind. The initial dump-only attempt could not unwind past
KERNELBASE and produced invalid leaf guesses; those were rejected. The retained
walker stops before the first PC outside the module list. The final run yielded
30 module-mapped frames, with no failed stack-read callback before that boundary.
This is a partial native unwind, not independent debugger confirmation of every frame.

Whitelisted excerpt (top to bottom; the complete 30 frames are in evidence):

```text
KERNELBASE.dll       +0x10a708
combase.dll          +0xc3c99
CoreMessagingXP.dll  +0xb17fc
CoreMessagingXP.dll  +0x38ba2, +0x3873b, +0x52fe3
CoreMessagingXP.dll  ... message-dispatch-region addresses (names unavailable)
user32.dll          +0x152e6, +0x14b6c, +0x3f593
ntdll.dll           +0x165d94  [KiUserCallbackDispatcherContinue, public symbol]
win32u.dll          +0x1304
user32.dll          +0x119df, +0x11968
CoreMessagingXP.dll  ... +0xad141, +0xa7761
Microsoft.UI.Xaml.dll +0x79566e, +0x5be83b
[stop: next PC outside recorded module ranges]
```

These 30 current-thread frames are **not** the missing 30 original stowed frames.
The stack establishes native reporting/message-dispatch/XAML involvement; it does
not prove which callback originally failed or that disposal order was incorrect.
No app C# source line, managed exception type or managed call chain was recovered.
The boundary could include JIT/dynamic code; this walker cannot resolve such code
and does not claim it is corrupt. A compatible CLR DAC/SOS or ClrMD session and
sufficient captured CLR data would be required to try a managed unwind.

| Module | Dump file version | RSDS GUID / age |
| --- | --- | --- |
| CoreMessagingXP.dll | 12.6.4.0 | 8AF1C0CEA6304CF3ADFC047648FC2535 / 1 |
| combase.dll | 10.0.26100.9711 | 4AC91D91223808F3AE6A3164A1B8B404 / 1 |
| Microsoft.UI.Xaml.dll | 3.3.0.0 | 53A92AA483D857E31AEAAE3726CA602B / 2 |
| coreclr.dll | 11.0.26.52348 | 0BA84159E8304EC79276E74F5DA22F6A / 1 |
| BiliCopilot.UI.dll (offline fixture) | 1.0.0.0 | 4B45FC4EDC594CEFBFF4128DC493C2B1 / 1 |

The coreclr hash is `46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`,
matching the retained RC3 runtime. The fixture DLL is
`1e4cb5d683a9856c6853871b3a80b381d96f74e5458cb2ce32ad13913cb0d833`;
its portable PDB is
`eb68844411ff7792efe5ae044965082823cfb9fc9c4bdf70a4d1e9dfc417fa38`,
with matching #Pdb GUID. That confirms an available matching app PDB, not a
managed instruction pointer/sequence point to map. Production Release DLL hashes
must not substitute for this offline fixture's loaded DLL.

Only `msdl.microsoft.com/download/symbols` was contacted for symbols; no dump or
memory was sent. ntdll.pdb completed, SHA256
`c93f4614aa6f40fe370d40eb7ee9589ca0eda170ef2d704b5d278da71c02c249`;
GUID matches, DBI age 1 matches RSDS (PDB information-stream age is 4 and retained
separately). An offline MSF/CodeView parser resolved the single ntdll frame above.
No source lines are inferred from nearest-public symbols.

The initial Python download/symbol-load workflow was stopped when it failed to
return promptly; its exact blocking call was not isolated. Switched to curl with
hard wall-clock limits. combase: 30-second timeout, 1,031,433/83,357,696 bytes.
CoreMessagingXP: 30-second timeout, 1,572,106/3,411,968 bytes; one bounded 50-second
resume added 556,239 bytes and also timed out. **Stopped downloads there.** Partial
files are rejected by MSF block-count validation; their hashes in evidence describe
incomplete downloads, not usable matching PDBs. XAML/coreclr PDBs were not obtained.
All symbol bytes remain in this checkout's ignored `artifacts/exit-diagnosis/symbols`.

## Timeline and runner false acceptance

All following times are 2026-10-31, UTC+08. File times are explicitly not event
timestamps. Original data were read from candidate Git archives and the authorized
native-crash/file-times.json; neither was rewritten.

| Time | Evidence | Meaning |
| --- | --- | --- |
| 00:27:15.4798982 | font-results.json mtime | font result written |
| 00:27:16.9226353 | settings.png mtime | screenshot written |
| 00:27:17.3359093 | font-settings.png mtime | screenshot written |
| 00:27:17.5488245 | events.jsonl mtime, final explicit-exit(players=1) | exit request marker; no per-stage completion timestamp |
| 00:27:18.4303415 | Application Error 1000, PID 11436 | native crash event |
| 00:27:25.0000000 (1 s precision) | dump header | dump timestamp; not process exit timestamp |
| 00:27:25.0305758 | WER 1001 | same report ID, not another crash |
| 00:27:26.6203906 | main/exit.json mtime | misleading explicitExit=true result written |

Application Error minus events-file mtime is **0.8815170 s**; WER minus Application
Error is **6.6002343 s**. These are timestamp differences, not measured save duration,
cleanup duration, process-exit latency or universal WER publication bounds. The
precise process termination instant and code were not captured. In particular,
exit.json was written **1.5898148 s after the WER event's TimeCreated**, so the old
false green was not explained solely by waiting too little: it never consulted
the required evidence. TimeCreated also does not measure when a query first saw it.

Original `accepted.json` has explicitExit=true and PID11436; main/exit.json adds
unhandledCount=0. Neither contains target exit code. The authoritative retained
`native-verdict.json` has BLOCKED, cleanExitCertified=false, performanceAccepted=false
and runnerAcceptedJsonSuperseded=true. This report does not replace that verdict.

The later RC replay.ps1 addition still has a gap: it checks process disappearance,
sleeps 8 seconds, filters 1000/1001 by message identity/layout and checks the
**observer's** exit code. It does not retain the **target's** process handle/exit
code, and the event filter is not exact-PID matching. The live application was
not rerun to test that amended gate. This diagnosis documents the required repair;
it does **not** claim the runner has been fixed in this docs-only change.

Required acceptance contract for the next reviewed runner change:

1. Bind launch to target handle, PID, creation time, full exe path and deployment
   hash; retain the handle before exit. Capture the target's unsigned exit code
   after WaitForExit; only the defined normal code (normally 0) can qualify.
   Missing handle/code, timeout, forced termination or nonzero code is non-green,
   even with zero managed unhandled events. Do not substitute observer exit code.
2. Record monotonic and UTC exit-request, observed-termination and query times;
   capture Application-log bookmark/RecordId before launch. Match Application Error
   XML faulting ProcessId (not provider Execution ProcessID) plus image/creation
   identity. Link WER to that event by ReportId and exact app/package path; a WER
   event lacking a faulting PID must not be assigned its provider PID. Protect
   against PID reuse and unrelated runs of the same identity.
3. Continue a **bounded** post-termination collection interval (proposed 30 seconds,
   not claimed to be a proven maximum), preserve query success/visibility times and
   pending WER status. Query failure or pending/unresolved matching reports is
   INCONCLUSIVE/BLOCKED, not success. The event-time search covers the whole owned
   process lifetime through collection end, not just eight seconds after disappearance.
   A finite quiet interval is explicitly bounded acceptance, not proof that no
   future delayed event can ever arrive; later exact matching crash evidence revokes it.
4. Offline gate checks before another native run: this captured run must be red;
   nonzero/no-event and missing-code cases must be non-green; unrelated PID must
   not contaminate the run; delayed exact-PID/report-ID events must revoke a
   provisional result; only normal-code + complete bounded event observation can
   qualify. Do not edit the original accepted.json to hide the false-positive history.

## Source ordering: what is known versus inferred

Line references below are static source locations in f6ed3ec (the parent has the
same lifecycle source), **not crash stack source lines**.

- `MainWindow.xaml.cs:101` RequestExitAsync delegates to TrayLifecycle.ExitAsync.
  It sets appVM.IsClosed, yields, saves window statistics, awaits
  SettingsPageViewModel.CheckSaveServicesAsync, then awaits all snapshot players'
  CloseAsync. TrayLifecycle sets its shared exit task before starting work, prevents
  reentry and orders save → player cleanup → tray Dispose → exit callback.
  Its TryCompleteAsync logs cleanup exceptions and continues; a completed outer
  task alone would not establish successful cleanup of every resource.
- `PlayerViewModel.cs:34` sets the close task and cancels/disposes danmaku work
  before Window.Close; DisposeAsync joins initialization and shared disposal.
  Resource order is playback-event cleanup → player/history/client disposal →
  remaining client → window/XAML disposal → remove from Players. Existing cleanup
  catches mean a success completion is not a proof that every underlying operation
  succeeded. PR48 publication checks closed window/session state; it was not removed.
- `PlayerWindow.cs:163` destroys the native window; Destroying at :384 sets IsClosed
  and unregisters input/window handlers. DisposeAsync at :90 then disposes the
  DesktopWindowXamlSource. This is a concrete order worth instrumenting, but the
  current dump does not prove it violates the API contract. ResetFocus and delayed
  title-padding callbacks already check IsClosed/IsDisposed.
- Final main callback (`MainWindow.xaml.cs:121`) releases tray/notification resources,
  backs out navigation, closes other registered windows, removes/unhooks main,
  calls Close and App.Exit. Tray disposal is idempotent; queued notification work
  checks IsExiting. Calling App.Exit is not itself evidence of the bug.
- No call to VisorClient.ShutdownAsync exists in this exit chain (repository search
  found only its declaration). Visor command timeout/cancellation warnings cannot
  be promoted to a cause. The uncalled server Shutdown handler is a separate
  process path. The dump does not establish a Visor→dispatcher causal edge.
- Pinner.OnControlUnloaded guards the deferred Repeater against null. WIC awaits
  are uncancelled while streams remain alive, then reject cancelled/stale results.
  Neither historical guard was lost in the five-file migration. Other async UI
  callbacks could still be outstanding; there is no complete shutdown-drain marker.
- The offline MainRuntimeChecks creates a separate explicit-exit controller Window
  and keeps one player open. The controller is a local variable and is not added
  to appVM.Windows in that fixture code. This extra XAML window is a confounder
  compared with normal production tray exit, not proof it caused this crash.

The final explicit-exit marker is before RequestExitAsync. There are no timestamped
save-finished, players-finished, tray-finished, XAML-destroyed or dispatcher-drained
markers. Therefore the proposed complete chain **cannot be placed on the actual
crash timeline**, and neither a late callback nor a release-order defect is proven.

## Ranked hypotheses and smallest next experiment

These are investigation priorities, not established causes. No production fix is
justified yet; do not change runtime, remove App.Exit, add blanket catches or
rewrite disposal order based only on HRESULT.

| Priority | Hypothesis and evidence strength | Falsifiable single-variable follow-up |
| --- | --- | --- |
| 1 | A queued UI continuation fails during teardown. Moderate contextual support: same-thread language exception, dispatch/XAML reporting stack, async cleanup; original callback unknown. | First add metadata-only stage/queue-generation markers in an isolated diagnostic fixture. If one callback is observed after its owner closes and is the original throw, change only that callback's lifetime guard/drain; rerun the identical exit sequence. An earlier unrelated original throw rejects this hypothesis. |
| 2 | Player native-window/XAML ownership ordering, or the fixture's extra controller window, exposes teardown failure. Static-order evidence only. | Once better exception evidence exists, vary **one** factor: close only the fixture controller before requesting the same exit, keeping the player and runtime unchanged. Separately test player disposal order only if the original throw points there; do not combine both changes. |
| 3 | A settings/navigation/other unload callback fails independently of the above. Possible, but there are no stage timestamps or matching original frames. | Stage markers must show where the first error originates. Hold player/controller behavior fixed and vary only the identified callback; absence of a matching original exception lowers this hypothesis. |
| 4 | Runtime-specific behavior changes an existing WinUI lifetime defect. Weak: one RC crash and a different historical net9 failure do not isolate runtime. | No runtime replacement in this round. Only after an exact reproducer and source-level mechanism exist could an explicitly authorized matched-runtime comparison distinguish this; current evidence cannot. |

Minimum evidence acquisition before a code fix: complete original stowed-stack
bytes and language exception/managed frames, or a first-chance observation tied to
the exact callback, with stage timestamps and the corrected target-exit gate.
A fuller dump of a **new authorized owned reproduction** may capture the missing
240 bytes/CLR metadata; a full dump is sensitive and must remain local/ignored.
Do not attempt to recreate missing heap data from symbols or scan an unrelated dump.
Finishing the exact official CoreMessagingXP/combase/XAML symbols can improve
native frame names, but cannot recover absent original-stack memory by itself.

If a real throw site is established, propose one minimal ownership/cancellation
fix for review, retain a red run with the exact failure, then a green run with the
same runtime/package/fixture and corrected exit/event criteria. Existing Player15/
Main4 success is not that red/green proof. Do not rerun all 487 tests/build/package
matrices merely to accumulate unrelated green results.

## Historical net9 comparison

Only already documented evidence was compared. PID31584 had CoreMessagingXP
12.6.4.0 / `0xc000027b`, offset **0xadf5e**, and combase 10.0.26100.9711 /
**0x80131509**, offset 0xc4063. It failed before completing sampling/20 cycles/
explicit exit; RC11436 reached the explicit-exit request and has **0xb17fc /
0x8000ffff**. Each event pair belongs to its own one report. Shared reporting
modules/category do not establish a shared originating exception, an identical
root cause, or an exclusively .NET11 regression. No old dump was opened.

## Offline replay and verification

Run from this repository root, Windows x64 Python. Scripts are deliberately bound
to the authorized dump hash/PID/thread; they are not general-purpose heap tools.

```powershell
python docs/performance-acceptance/net11-exit-crash/parse.py
python docs/performance-acceptance/net11-exit-crash/walk.py
python docs/performance-acceptance/net11-exit-crash/identities.py
python docs/performance-acceptance/net11-exit-crash/pdb-publics.py
git diff --check
git -C externals/bili-kernel rev-parse HEAD
git -C externals/bili-kernel status --porcelain
```

The first three scripts need no network or symbol download. walk.py reads only
allowlisted image names from the authorized fixture activation, System32 and the
exact loaded WindowsAppRuntime.2_2.6.4.0 package; identities.py verifies RSDS against
the dump as well as hashes. If those binaries change, results must not be reused.
The last script reports unavailable/incomplete symbols explicitly; it does not
download or silently accept them. Its only successful symbolic resolution here
is ntdll; lack of symbols is not a failed crash gate or a reason to turn it green.

Official symbol requests used these exact identity URLs with curl.exe
`--fail --location --connect-timeout 8 --max-time 30 --silent --show-error --output <ignored-file>`:

```text
https://msdl.microsoft.com/download/symbols/combase.pdb/4AC91D91223808F3AE6A3164A1B8B4041/combase.pdb
https://msdl.microsoft.com/download/symbols/CoreMessagingXP.pdb/8AF1C0CEA6304CF3ADFC047648FC25351/CoreMessagingXP.pdb
https://msdl.microsoft.com/download/symbols/ntdll.pdb/C031D45B0DA18A6E74D06ED6AB2DD9E11/ntdll.pdb
```

ntdll was obtained by Python urllib before the curl fallback. CoreMessagingXP's
one resume used `--continue-at - --max-time 50`. These are historical reproduction
commands, not an instruction to repeat downloads indefinitely. Full identities,
hashes, symbol status and 30 code-only frames are in the small evidence JSON.
The four retained offline scripts were run successfully; this verifies extraction
and matching, **not a clean application exit or a repaired crash**.
