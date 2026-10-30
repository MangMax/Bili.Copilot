# Dependency performance diagnosis replay

Run from the repository root, SDK 10.0.403, existing winapp 0.7.1, an exclusively available local desktop at 150% DPI. Do not change the user's DPI, start protected identities, or reuse historical layouts. No installation or credentials are needed. Kernel must be the recoverable `0ae5e4e` from the existing dependency-upgrade bundle. See the adjacent report for conclusions and limitations.

## Native graphics group

`prepare-native.ps1 -Destination <fresh absolute directory> -RunId <unique id> -Variant candidate|win2d-icons` calls the reviewed full-source offline Player/Main fixture. The only package edits are Win2D 1.4.0→1.3.2 and, because NU1605 prevents a single-package test, FluentIcons.WinUI 2.2.344→1.2.315. `win2d-only-red` reproduces that restore rejection. No warning is suppressed. Kernel has no Win2D declaration; its AppSDK 2.6.4 stays aligned with the host. All source is copied from this checkout, not a historical binary.

Build each fixture sequentially:

```powershell
dotnet build <fixture>/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:SolutionDir=<absolute-repo>/src/
./docs/performance-acceptance/dependency-performance-diagnosis/freeze.ps1 -Fixture <fixture>
python docs/performance-acceptance/dependency-performance-diagnosis/graphs.py <fixture> <graph.json>
```

Freeze **before** building the next variant. The freezer copies output, fills missing recipe content once, refuses overwrites/path escapes, removes recipes and hashes every file. The replay checks every frozen DLL against the deployed DLL, including recipe-only DLLs. A recipe never overwrites frozen reference assemblies. Retain the original output alongside the frozen directory; the replay uses its recipe only to check for missing content and validates the built app identity.

Place fixtures in `artifacts/dependency-performance-diagnosis/player-a` and `player-b`. `run-native.ps1 -Workspace <repo> -Tag <fresh-tag>` runs A/B/B/A with four independent activations, fresh layouts, Player15 before Main4, notifications disabled, SettingsPage, 5-second warmup, 2-second settling, 61 rows per visible/hidden/restored phase, 20 same-HWND loops, explicit exit and owned deregistration. No build or other benchmark may run during this sequence. The same arm's identity is removed before reuse; do not reuse a surviving registration. Stop and inspect any failure; do not continue with an unsafe closure. Runs and logs are in `artifacts/dependency-upgrade`, environment/order in `artifacts/dependency-performance-diagnosis`.

Window captures use direct window mode. The first attempt's screen capture failed `foreground_not_target` before Main; this red evidence is retained and excluded. Do not drop low/high CPU rows or treat the 60 intervals as independent trials. `analyze.py <native-evidence-root> <run names...>` derives full and half rates from endpoint cumulative CPU and timestamps.

For relevant cover/WIC/remount checks, `prepare-controls.ps1 -Destination <fresh> -RunId <unique <=8 chars> -Assets <player>/obj/project.assets.json` aligns the minimal fixture to the actual selected graphics packages. Build Harness.csproj Release x64 with `-p:SourceRoot=<repo>`, run the owned fixture and its generated run-controls.ps1, then explicitly close and deregister. Keep un-cancelled WIC await/stream lifetime and PR27/43 safeguards. Simulated loss callbacks do not certify hardware device loss.

After an inconclusive graphics group, `prepare-appsdk.ps1 -Workspace <repo>` creates C using the same graphics as B and AppSDK 1.8.251106002. It temporarily edits **this worktree's** clean kernel central declaration, captures the exact patch/hashes and resolver graphs, builds/freezes C, and restores the original bytes in `finally`. Do not interrupt it with a second build. Then prepare/build the C controls against its assets, and use `run-controls.ps1 -Fixture <controls> -Layout <fresh-layout> -Evidence <fresh-results>` for both B/C; this wrapper freezes their output, checks every DLL, runs the seven checks, closes and deregisters the owned process. Finally run `run-native.ps1 -Workspace <repo> -Tag <fresh-tag> -Arms b,c,c,b`. No third native group is part of this diagnosis.

## Nonempty-middle LCS

```powershell
python docs/performance-acceptance/dependency-performance-diagnosis/prepare-lcs.py <fresh-directory>
dotnet build <fresh-directory>/Lcs.csproj -c Release
./docs/performance-acceptance/dependency-performance-diagnosis/run-lcs.ps1 -Executable <fresh-directory>/bin/Release/net9.0/win-x64/Lcs.exe -Evidence <fresh-results>
python docs/performance-acceptance/dependency-performance-diagnosis/analyze-lcs.py <fresh-results>
```

The generator extracts exact committed rectangular, pre-shortcut jagged and final shortcut sources; only namespaces change. The ledger records both source and generated hashes. Each process validates all three implementations before measurement, checking ordered keys, exact collection event sequences, exact resulting object references and unchanged prefix/suffix anchors. Each of five workloads at 256/1024/2048 items asserts both middles are nonempty. Maximum middle is 1024 (about 4 MiB matrix), not an unbounded stress test. Inputs exist before measured regions. Timed work is Compute+Apply on a fresh List; list setup and behavior assertions are outside timing/allocation regions. Five warmups precede twelve retained samples per case, no forced GC. R/J/S/S/J/R interleaves six independent processes, two per variant. Tiered compilation remains normal; this is not a fully stabilized Tier1 claim.

Production algorithms, final package declarations and default SDK remain unchanged. Retained experimental edits live in ignored fixture outputs; the AppSDK helper restores its temporary kernel declaration byte-for-byte. Do not interpret reused historical 487-test logs as new execution, or these offline checks as authenticated-home, AOT, #49, physical DPI or long-leak acceptance.

The optional focused `run-lcs-tiered-off.ps1` uses the same executable with two process-local tiering environment switches, restores them in `finally`, and stores separate evidence/config hashes. Do not merge its timing rows with the default-runtime dataset. After all sampling, run `audit-deployments.py`, `verify.py`, `archive.py`, then `verify.py` again to verify decompressed original bytes. `tables.py` and `chart.py` render the retained explicit run allowlist; they do not scan old green runs.
