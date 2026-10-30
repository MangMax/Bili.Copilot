# RC exit full-dump analysis: late player timer callback

2026-10-31. **The original managed callback is recovered. Native acceptance remains
BLOCKED; no fix or green run is claimed.** The retained LEO is a
`System.Runtime.InteropServices.COMException` (`0x8000FFFF`) thrown from
`PlayerInteractivePanel.OnTempControlTimerTick`. Its first `ViewModel` read goes
through XAML `DependencyObject.GetValue`; the failing ABI return reaches the
CsWinRT HRESULT throw helper. The timer callback's ABI wrapper catches the managed
exception and forwards it to native error reporting. This is an application timer
running during dispatcher shutdown, not evidence that the final `Application.Start`
JIT address itself threw or that a framework shutdown event handler was the origin.

The same production control starts `_tempControlTimer` on Loaded but omits it from
Unloaded cleanup. That is the concrete lifetime defect to test first. **The dump
does not identify which player/window instance owned this invocation**, whether its
Unloaded handler completed, or the exact internal XAML condition returning the
HRESULT. It therefore does not establish that a one-line Stop fully repairs exit,
that the extra fixture controller was irrelevant to triggering timing, or that
native-window/XAML destruction must be reordered.

## Evidence boundary and checkout

Only the explicitly authorized full dump was opened:

`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction/original1/crash-full.dmp`

Size **1,550,491,329**, SHA256
`eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d`;
PID **38208**, exception thread **39912**, x64, `0xC000027B`, one SE02/LEO1.
Every dump replay validates its hash. Only structural exception/CCW/GC-handle
metadata, code, module identities and code stacks are emitted. No exception message,
Data, locals, business object fields, heap-string scan, other dump or live process
attachment was used. The known `_wrappedObject` is followed only after its holder
type/backreference and DAC handle target validate. No arbitrary stack words become
managed frames.

Independent branch started at `f9271158`, fast-forwarded to
`c4ff558d5438a1755d3f98b6b3742c122b90e915` for the diagnostic documents. The local
parent `feat/performance-45` remained `f9271158c315ef7b1101050cb10560abdacc722a`;
merging its latest local head reported already up to date. Production stays net10.
The initially unexpanded kernel checkout lacked the required tree; imported the
repository's dependency-upgrade bundle and restored detached
`0ae5e4e000a41a07f6696746706df11c06e8835b`, clean. No other worktree was modified.

Read AGENTS/docs/agents and the requested systematic-debugging, diagnosing-bugs,
WinUI review and verification skills. Their live reproduction/fix steps were
deliberately excluded by this task's offline-only authorization. No subagent,
desktop action, app launch/build, 487-test run, profiling, registration, login,
production edit, runtime replacement, push, PR, issue or parent integration.

## Matching DAC and tool provenance

The dump records `coreclr.dll` **11.0.26.52348**, base `0x7ffef0860000`, RSDS
`0BA84159E8304EC79276E74F5DA22F6A/1`. The frozen RC3 module has SHA256
`46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`.
PE timestamp, image size and RSDS match the dump. This is actual loaded-runtime
evidence, not an inference from global.json.

The existing local RC3 `mscordaccore.dll` is x64/AMD64 (`0x8664`), file version
**11.0.26.52348**, SHA256
`6bf2f1d90753b2c72deb6654dc32ba73959bfa85e64058ae38c341feb726c819`.
It is byte-identical to the frozen DAC. Both runtime/DAC version resources name
commit `5254d8bcf59917edc1e1abc33bdf2442b77b1c70`. ClrMD opens it with
`CreateRuntime(dac, ignoreMismatch: false)` and successfully resolves the CLR,
RefCounted GC handle, exception object and JIT methods.

The analyzer runs on existing SDK **10.0.403**, targeting net10, without changing
global SDK selection. **Microsoft.Diagnostics.Runtime 4.2.751201** was obtained
from the official NuGet flat-container endpoint, SHA256
`17c84b6aa5eacda66775f3550aebd99686c27099ebfe7ae78b37efa2ed6c8aea`.
`dotnet nuget verify` verified Microsoft author and NuGet repository signatures.
Package source commit: `2e7e5d74cddb2be0ac523aedead10b90a95e71c3`.
**Iced 1.21.0** is a third-party disassembler from NuGet.org, with its repository
signature verified; it decodes only the bounded JIT code range. Dependencies and
tools live in ignored `artifacts/exit-full-analysis`; package hashes/versions and
the lock file accompany the replay scripts. Python **3.13.17**, curl **8.21.0**.

[identities.json](net11-exit-full-analysis/identities.json) records exact frozen
module hashes, RSDS and architecture. CoreLib has multiple CodeView entries;
the matching native-image entry is used, rather than incorrectly rejecting it
because its portable-PDB entry differs. No machine-wide tool installation.

## LEO recovery and three different stacks

The RC3-tagged [ComWrappers implementation](https://github.com/dotnet/runtime/blob/v11.0.0-rc.3.26522.148/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ComWrappers.cs)
defines a 64-byte-aligned dispatch block pointing to ManagedObjectWrapper, which
contains a holder GC handle. The exact PE-commit raw source URL returned 404;
the tag source is corroborated by the following runtime/DAC structural checks,
not assumed interchangeable solely because its name says RC3:

```text
LEO interface             0x1fb6ca435d0
aligned dispatch          0x1fb6ca435c0
ManagedObjectWrapper      0x1fb6ca43540
HolderHandle              0x1fb7cd99f88
holder managed object     0x1eb92c6c788
holder type               System.Runtime.InteropServices.ComWrappers+ManagedObjectWrapperHolder
holder._wrapper           matches 0x1fb6ca43540
DAC handle kind           RefCounted; target independently matches holder
holder._wrappedObject     0x1eb92c6aad0
exception type/HRESULT    System.Runtime.InteropServices.COMException / 8000FFFF
```

**Recovered exception's stored managed stack**, in stored order:

```text
7ffe93fcc14f  WinRT.ExceptionHelpers.<ThrowExceptionForHR>g__Throw|41_0(Int32)
7ffe9388a041  BiliCopilot.UI.Controls.Core.PlayerInteractivePanel.OnTempControlTimerTick(...)
7ffe92df3f02  WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Microsoft_UI_Dispatching_DispatcherQueueTimer__object.Do_Abi_Invoke(...)
```

These are ClrMD-decoded exception stack entries, not a currently executing throw
stack. The final ABI frame belongs to `Microsoft.InteractiveExperiences.Projection.dll`.
The native callback has returned by the reporting snapshot; thread39912's
`CurrentException` is null. Its current DAC stack instead contains native-transition
markers and `IApplicationStaticsMethods.Start → Application.Start → Program.Main →
Environment.CallEntryPoint`. The requested JIT address `0x7ffe90e1034f` resolves to
the ABI **Start** method at native start `0x7ffe90e10240`. Transition-marker duplicates
are not additional managed calls.

**First-chance record628** had only the throw helper in `Exception.StackTrace`,
IL26/native95/line121. That early stack is still being built; it was not a capture
of `new StackTrace()` on the current thread. The later LEO supplies the two caller
frames absent from that record. Do not append guessed frames to either stack.

**Native SE02 stowed stack** retains all 30 addresses. Matching CoreMessagingXP
publics resolve the timer callback and queue shutdown; the table below contains
nearest-public mappings, not native source-line claims:

| Original stowed location | Matching public + displacement |
| --- | --- |
| combase+0xc4063 | RoCaptureErrorContext +0xf3 |
| CoreMessagingXP+0xb17bc | DispatcherQueueTimer::TimerCallback +0x1cc |
| CoreMessagingXP+0x3873b | TimeoutHandler::ImportAdapter$ +0x5b |
| CoreMessagingXP+0x52fe3 | TimeoutManager::Callback_OnDispatch +0x1e3 |
| CoreMessagingXP+0xad141 | DispatcherQueue::Run +0x51 |
| CoreMessagingXP+0xa7761 | DispatcherQueueController::ShutdownQueue +0x111 |
| Microsoft.UI.Xaml+0x79566e / +0x5be83b | unresolved; exact public PDB unavailable |
| 0x7ffe90e1034f | managed ABI Application.Start, independently resolved by DAC |

This native stack is the stored stowed reporting/dispatch chain. It is not a
native backtrace of the earlier internal XAML `GetValue` failure. The small
[managed stack](net11-exit-full-analysis/managed-stack.txt),
[native addresses](net11-exit-full-analysis/stowed-stack.json), and
[symbol results](net11-exit-full-analysis/native-symbols.json) preserve the distinction.

## Actual code mapping and rejected misleading IL offset

Frozen `BiliCopilot.UI.dll`/portable PDB GUID
`EAD36DF5E4BA46DE9B2B85EF7DB7CAC9` match. The PDB document checksum for
`PlayerInteractivePanel.xaml.cs` is
`5cb8ddb4957042ed9490b252a5aa49475454927ed29a081135f48ac5165430de`, matching the
working source and retained source-comparison record (fixture equals production).
Git blobs at RC production `f6ed3ec` and parent `f9271158` are also identical.

The callback metadata token is `0x0600227b`. **ClrMD reports IL0, but the returned
IL map covers an older native body at `0x7ffe92df4040...`, not the exception PC in
the active body `0x7ffe938899c0...`. That IL0 is rejected as a direct PC→line mapping.**
The analyzer explicitly prints `mappingCoversIP=False`. The source-level operation
is established independently by the frozen IL and active machine code:

1. Callback IL starts with `get_ViewModel` before its null test (source line198).
2. Actual `LayoutUserControlBase<T>.get_ViewModel` IL loads `ViewModelProperty` and
   calls `DependencyObject.GetValue`.
3. Actual `IDependencyObjectMethods.GetValue` IL calls IInspectable vtable slot6,
   then `ExceptionHelpers.ThrowExceptionForHR` on the returned HRESULT.
4. The active callback body inlines that operation. At `0x7ffe93889bee` it loads
   vtable offset `0x30` (slot6), calls it at `0x7ffe93889c20`, tests EAX at
   `0x7ffe93889c40`, and branches on a negative result to `0x7ffe9388a03a`.
   At `0x7ffe9388a03c` it calls the resolved HRESULT throw helper; recorded
   exception PC `0x7ffe9388a041` is the call's final byte (return is `...a042`).
   This is the first GetValue, before a null result can be checked.

The getters/projection are **inferred inlined operations**, not additional
recovered stack frames. See [IL/sequence points](net11-exit-full-analysis/metadata.txt)
and [bounded disassembly excerpt](net11-exit-full-analysis/throw-disassembly.txt).
The method-level callback identification does not depend on the rejected IL map.

Frozen ABI Invoke IL has a catch path calling `SetErrorInfo(ex)` and
`GetHRForException(ex)`. The loaded WinRT.Runtime version **2.3.1.54291** identifies
CsWinRT commit `ff5227a4ffca06dfa6de8ca0a5c127c04f294291`; its exact
[ExceptionHelpers source](https://github.com/microsoft/CsWinRT/blob/ff5227a4ffca06dfa6de8ca0a5c127c04f294291/src/WinRT.Runtime/ExceptionHelpers.cs)
confirms HRESULT translation and language-exception forwarding. This explains why
zero managed-unhandled notifications do not certify clean native exit.

## Causal limits, framework comparison and next single variable

Stage markers put this same-thread first-chance exception after player/XAML-source,
tray and main completion markers, after the fixture controller Closed event and
App.Exit return, and after Dispatcher ShutdownStarting. No ShutdownCompleted was
recorded. They do not identify the offending panel's instance or demonstrate that
all queued work was cancelled. The earlier instrumented focus/activation/padding
callbacks do not cover this timer, so their absence after exit never excluded it.

The observed chain is **late application timer → dependency-property GetValue
failure → CsWinRT COMException → ABI error forwarding → native stowed fail-fast**.
Framework queue draining is consistent with the official
[DispatcherQueue shutdown contract](https://learn.microsoft.com/en-us/windows/apps/develop/dispatcherqueue).
This is not sufficient evidence for changing App.Exit, removing the fixture
controller, replacing RC3, or reversing native-window/DesktopWindowXamlSource
destruction. A matching XAML native symbol/internal object state would be needed
to name the exact native guard that returned E_UNEXPECTED; that detail remains
unresolved even though the originating managed callback is now known.

Primary-source known-issue searches used DispatcherQueueTimer/shutdown/crash and
GetValue/8000FFFF. Reviewed [WinUI issue10698](https://github.com/microsoft/microsoft-ui-xaml/issues/10698):
its MAUI/WPF template/arrange failure and `80004005` do not establish this cause.
No matching official issue/fix was established by this bounded search. Upstream
[DependencyObject implementation](https://github.com/microsoft/microsoft-ui-xaml/blob/main/dxaml/xcp/dxaml/lib/DependencyObject.cpp)
was checked only as context; main is not a source mapping for the captured 3.3.0.0
binary. In particular, do not claim a wrong-thread failure from this same-thread
trace or substitute upstream line numbers for missing exact native symbols.

Minimal proposed experiment, **not applied or run here**: add only
`_tempControlTimer.Stop()` as the first operation in `CleanupInteractionState`,
which is invoked from `OnControlUnloaded`; retain the existing Loaded restart.
This closes the concrete start/stop asymmetry while leaving the fixture/controller,
player cleanup order, App.Exit, runtime and packages unchanged. Do not use another
`ViewModel != null` check as a lifetime guard: that getter is itself the failing
native access. Do not catch/ignore COMException.

Future authorized red/green plan: keep the recorded full-dump run as the historical
red, then establish the same callback in an unchanged fresh baseline if a controlled
pair is needed. Change just the timer Stop and rerun the same fixture sequence,
with code-only per-panel Loaded/Unloaded/timer-stop identifiers to establish whether
the failing instance actually unloaded. Require retained target code0, complete
bounded event observation, no matching AppError/WER and no first-chance occurrence
of this callback signature. The gate must still reject this historical run.
If the same callback persists, determine whether Unloaded was skipped or a queued
tick survived before choosing a managed lifetime flag/earlier teardown hook as a
separate experiment. Do not stack both changes to obtain an unexplained green.
Any fixture-only result must stay separate from production certification.

## Symbol transport and replay

CoreMessagingXP PDB **3,411,968 bytes** completed; SHA256
`5e47226ec06ca15c4e8b7afb4fccc4d6e114c6e7dd48bb416acf7c4a8e71680d`,
RSDS GUID `8AF1C0CEA6304CF3ADFC047648FC2535`, DBI age1 (information-stream age2).
The earlier 30-second partial download was a timeout, not proof of unavailability.
combase **83,357,696 bytes** completed using bounded official HTTP ranges, with
each Content-Range/length checked before assembly. MSF block count and
GUID `4AC91D91223808F3AE6A3164A1B8B404` / DBI age1 match (information-stream age4).
SHA256 `9ecd353dbf659a029b637324e88a5b2a24771cd62b360392bb3a521dcc5c6b29`.
The first single transfer was stopped at 29,294,592 bytes; the complete ranged
transfer replaced it only after every part validated. A separate 4MiB transport
probe was not mistaken for a full PDB. Final symbol results are in
`native-symbols.json`.

The exact `Microsoft.ui.xaml.pdb/53A92AA483D857E31AEAAE3726CA602B2` endpoint returned
404, including tested casing, compressed `.pd_` and `file.ptr` alternatives.
No partial or alternate-age PDB was accepted. All symbol files remain ignored.
Transfers use only Microsoft symbol URLs; no dump bytes are uploaded.

CoreMessagingXP transport command (run once, exit0):

```powershell
curl.exe --fail --location --connect-timeout 15 --max-time 900 --silent --show-error https://msdl.microsoft.com/download/symbols/CoreMessagingXP.pdb/8AF1C0CEA6304CF3ADFC047648FC25351/CoreMessagingXP.pdb -o artifacts/exit-full-analysis/symbols/CoreMessagingXP.pdb
```

Replay from the repository root on this machine:

```powershell
# RestoreTools is only needed to restore the pinned task-local diagnostic packages.
./docs/performance-acceptance/net11-exit-full-analysis/replay.ps1 -RestoreTools
# Subsequent offline analysis:
./docs/performance-acceptance/net11-exit-full-analysis/replay.ps1
python docs/performance-acceptance/net11-exit-full-analysis/symbolize.py
git diff --check
```

The optional bounded `download_combase.py` is separate from offline replay.
Replay builds only the small analyzer, never the app. Evidence verification is
not an exit pass. **Native BLOCKED / #49 FAIL / AOT BLOCKED / authenticated home
unverified remain unchanged.**

Final checks: `replay.ps1 -RestoreTools` exit0 (all **23** restored NuGet packages
passed signature verification), then `replay.ps1` without restore exit0;
`symbolize.py` verifies both complete downloaded PDBs and reports XAML unavailable;
module/PDB/source identity checks pass. Production and parent SDK files are
unchanged. Source/package hashes and replay outputs are retained in the companion
directory. These checks validate the analysis, not the proposed timer fix.
