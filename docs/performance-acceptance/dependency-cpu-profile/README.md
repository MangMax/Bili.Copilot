# Replay and evidence

Read [the report](../dependency-cpu-profile.md) first. This is a bounded diagnostic experiment, not a performance certification. Use a released local desktop at existing 150% DPI. Never use protected identities or a current project recipe to redeploy historical outputs.

`replay.ps1` reads the original baseline frozen directory from `$env:TEMP/bili-dep-d48-base-player/frozen-output`, the original closure ledger from sibling `t3-d48c749d`, and the candidate A frozen directory/ledger from sibling `t3-340954c4`. These historical inputs must still exist and pass the source hash gate. It never reconstructs missing input from current projects. New `Mang.BiliCopilot.Cpu.D3d.<run>` identities and layouts are used per run; rerun names must be fresh. All 357 DLLs are compared in both directions before Player/Main begins. Only this worktree's ignored output directories and task-owned registrations/processes are changed.

Build the cached-library collector **before** native sampling:

```powershell
dotnet build docs/performance-acceptance/dependency-cpu-profile/ProfileHost.csproj -c Release -o artifacts/dependency-cpu-profile/host --ignore-failed-sources -p:NuGetAudit=false
```

The net9-targeted collector rolls forward to installed stable net10.0.14 here; verify the actually loaded collector runtime if replaying elsewhere. Neither application's self-contained net9 runtimeconfig is changed. Do not install a global tool or alter machine profiling policies. Analysis-only additions to `Program.cs` after collection record GC reasons/times; original capture binary/runtimeconfig hashes are archived separately from the final parser binary hashes.

Accepted order (one independent process per condition; use **fresh** run names when repeating):

```powershell
./docs/performance-acceptance/dependency-cpu-profile/replay.ps1 -Arm up   -Mode profile   -Run r2-up-profile
./docs/performance-acceptance/dependency-cpu-profile/replay.ps1 -Arm base -Mode reference -Run r3-base-ref
./docs/performance-acceptance/dependency-cpu-profile/replay.ps1 -Arm up   -Mode reference -Run r4-up-ref
./docs/performance-acceptance/dependency-cpu-profile/replay.ps1 -Arm base -Mode profile   -Run r5-base-profile
```

The reference condition uses the same file-boundary watcher and two endpoint counter snapshots but no EventPipe session. Profiles attach only to the validated owned PID after the hidden phase writes 122 rows. Buffer is 64 MiB, duration 61 seconds, SampleProfiler + informational GC/JIT/Loader, rundown enabled. No UIA polling inside measurement, no forced GC, no network/auth events or memory dump. The frozen fixture's own once-per-second process-property sampling is retained and is visible in the stacks.

After every native process has exited, parse and reduce (replace PID with that run's actual launch PID):

```powershell
artifacts/dependency-cpu-profile/host/ProfileHost.exe analyze artifacts/dependency-cpu-profile/r2-up-profile/profile/cpu.nettrace 18884
artifacts/dependency-cpu-profile/host/ProfileHost.exe analyze artifacts/dependency-cpu-profile/r5-base-profile/profile/cpu.nettrace 2552
python docs/performance-acceptance/dependency-cpu-profile/summarize.py artifacts/dependency-cpu-profile/r2-up-profile artifacts/dependency-cpu-profile/r3-base-ref artifacts/dependency-cpu-profile/r4-up-ref artifacts/dependency-cpu-profile/r5-base-profile
python docs/performance-acceptance/dependency-cpu-profile/verify.py
python docs/performance-acceptance/dependency-cpu-profile/verify.py --archive
```

Large `.nettrace`, `.etlx` and sample JSONL remain under ignored `artifacts/dependency-cpu-profile/<run>/profile`; SHA256 and byte count are in each summary. `archive.py` has a strict small-file allowlist. `evidence/ledger.json` maps byte-preserving gzip files to original paths/hashes, avoiding Git line-ending normalization. The committed summaries contain filtered owned-PID stacks, counts, thread CPU, phase timestamps and trace hashes. `verify.py --archive` works without historical layouts or the large traces. Never aggregate External/wait samples as CPU work or translate stack counts into exact CPU milliseconds.

No further native rounds are required by this delivery. Native on-CPU attribution remains blocked by available tools/privileges; do not append package combinations, repeat known-failing WPR starts or demand an unbounded whole-product proof.
