# Stable runtime acceptance, external observer protocol v2

Use PowerShell, Python 3 (standard library only), SDK pinned by root global.json
and winapp 0.7.1. Run only with exclusive desktop ownership at existing 150% DPI.
Do not change DPI, use credentials, register Dev, or overwrite an old destination.

This protocol preserves Settings, notifications disabled, Player-before-Main,
five-second warmup and two-second phase settling. After settling, the fixture
writes one atomic phase marker and awaits 63 seconds. The external sampler
collects 61 readings at one-second monotonic deadlines, then writes completion.
The three-second margin is outside the measured first-to-last counter interval.
There are no per-sample callbacks, UI updates or process enumeration on the UI
dispatcher. The original `integration-pr27-pr43/IdleBaseline.cs` is untouched.

Counters: GetProcessTimes, GetProcessMemoryInfo (PrivateUsage/WorkingSetSize),
GetProcessHandleCount, Toolhelp thread snapshot filtered by target PID. Thread
enumeration is still observer work, but occurs externally. Per-sample wall
overhead is recorded; no claim of zero overhead or native CPU attribution.
CPU ms/s uses cumulative kernel+user deltas divided by monotonic elapsed time.
Do not compare these samples by subtraction with protocol v1.

## Prepare

Use the historical `dependency-upgrade/prepare.ps1` with a fresh absolute
destination and RunId; then run `prepare-observer.ps1 -Fixture <destination>`.
The full-source host substitutes offline dependencies/launch and instruments
font enumeration; it is not an authenticated production launch. Keep all
fixture source and build logs for provenance.

Three closures are needed:

1. Final net10: current source, H.NotifyIcon.WinUI 2.4.1.
2. Runtime arm net10: identical source, fixture-local H.NotifyIcon.WinUI 2.3.2.
3. Runtime arm net9: identical source, fixture TFM net9 and tray 2.3.2;
   fixture-local direct System.Reflection.Metadata reference (net9 workaround).
   Copy tracked Visor.Models source to a separate fixture dependency directory,
   target net9 and point only this fixture's ProjectReference at that copy.
   Keep production projects unchanged. Kernel stays at 0ae5e4e.

Build each serially, passing `-c Release -p:Platform=x64 -p:PublishAot=false
-p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false` and the
checkout's absolute `src/` as SolutionDir. Immediately call
`freeze.ps1 -Fixture <path> -BuildOutput <exact bin/x64/Release/TFM/win-x64>`
**before** another build can overwrite a referenced output. It copies missing
recipe content once, removes recipes and freezes hashes of every file.

## Replay

After all builds finish, run `replay.ps1 -Fixture <absolute path> -Run <fresh-id>`
in order net9, net10-common, net9, net10-common. Run final net10/2.4.1 separately.
Never run builds or benchmarks concurrently. The runner creates a new identity
`Mang.BiliCopilot.Runtime.N854.<fresh-id>`, checks the frozen DLL set, activates
a recipe-free copy and compares the complete DLL set both ways. It runs Player15,
starts the owned external observer, runs Main4/20 cycles/font-first-display and
explicit exit. Finally it records exact owned-process/package cleanup and yield.
It refuses reused run directories and registered identities. Failed runs remain
as red evidence and are not accepted performance samples.

Controls: use historical preparation with `-Kind controls -UseCurrentPackages`,
target net10, build with `-p:SourceRoot=<checkout> -p:Platform=x64`, and use its
owned runner. NativeAOT is not run because MSVC is unavailable.

## Evidence and limitations

Raw working evidence is `artifacts/net10-adaptation`; portable small evidence
is archived with hashes. Package graph changes include framework-specific asset
selection, not merely coreclr.dll substitution. Report every unavoidable graph
difference; do not label the comparison a perfectly isolated JIT experiment.
No source algorithm was adapted: there is no algorithm microbenchmark benefit
to combine with runtime measurements. JIT/GC, native library, scheduling and
machine-state effects cannot be uniquely separated without native CPU stacks.

Two processes per arm are a bounded compatibility/performance observation, not
a population estimate. No threshold was supplied. #49 pause stays FAIL; no
authenticated-home, cold-start, physical device-loss/DPI transition, long leak
or AOT acceptance is implied. Historical JSON/hash and overwritten RC output
issues remain parent follow-ups.
