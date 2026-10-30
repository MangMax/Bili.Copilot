# Dependency upgrade / .NET 11 experiment

Work in progress. Base: `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`; kernel `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`. No remote mutations or changes to other checkouts.

Scope L, high difficulty (native lifetimes, generator compatibility, controlled measurements). Authorized review/rollback boundaries:

0. Inventory official metadata and reproduce baseline; retain historical fixtures unchanged.
1. Test tooling and analyzers: fresh suite and production build, record diagnostics.
2. Managed dependencies, including kernel: compile/regression gates and recoverable bundle.
3. WinUI, graphics, tray dependencies: compile and owned native replay.
4. Isolated .NET 11 RC SDK/TFM candidate: verify official archive checksum, preserve stable branch if blocked.
5. Matched performance/native runs, final gates and evidence review.

Each stage is a local reviewable commit; finer splits and blocked latest versions will be recorded here. No blanket warning suppression or analyzer disabling is permitted. Existing warnings/suppressions are baseline, not new acceptance.

Historical 183 samples are not new A/B evidence. New runs must keep SettingsPage, notifications disabled, 150% scale, Player suite before Main, 5s warmup, 2s phase settling, visible/hidden/restored 61 samples each and 20 hide/restore loops. Pure list workload: 100/1000/5000, prepend two/undo, 3 warmups and 10 measured repetitions; no build or concurrent benchmark during UI sampling. No authenticated-home or general performance claims. AOT missing MSVC, hardware device loss/physical DPI, #49 pause FAIL and #56 limits remain explicit.

Only fresh owned identities may run; protected Caa6a7be, PID9608, Dev and historical layouts/data are untouched. No credentials or real account mutations. Raw logs reside in ignored `artifacts/dependency-upgrade`.

Initial checkout contained only `.git` in kernel with 474 staged deletions at f02c33f. Saved initial deletion patch in ignored artifacts, verified committed kernel.bundle and restored exact authorized baseline 98d0b9d. Main and nested status then clean. No changes in parent checkout.

## Baseline and tooling checkpoint

SDK 10.0.403; installed runtime used by roll-forward tests is 10.0.14. Fresh baseline VSTest: 487 passed, 0 failed, 0 skipped (`baseline/baseline.trx`). Debug x64 JIT full build succeeded with one pre-existing protobuf unused-import warning, zero errors; explicit runtime-assets validation succeeded. This differs from the parent's incremental zero-warning gate and is retained verbatim. Only ignored libmpv/ffmpeg assets copied from the known original repository.

Stage 1a (`bcd47fd`): Microsoft.NET.Test.Sdk 17.14.1→18.11.0, xunit.runner.visualstudio 3.1.4→4.0.1, coverlet.collector 6.0.4→10.1.0. Exact NuGet stable indexes checked; fresh suite remains 487/0/0 (`stage1/stage1-tests.trx`). No framework migration or test removal. Tests' existing EnableNETAnalyzers=false is unchanged.

Owned baseline Player identity: `Mang.BiliCopilot.DependencyUpgrade.D48BasePlayer`. Full-source fixture build: 1543 warnings, zero errors (standalone fixture diagnostics, not production diagnostics). First no-build activation resolved a directory without x64 and failed before launching; explicit Platform=x64 fixed evaluation. Player 15/15 passed. All ten captured images inspected: synthetic white media, transient faded controls, and clipped synthetic right-edge danmaku remain fixture limitations; no full visual-quality acceptance claimed. In particular before-destroy capture is only 52×23 pixels, so it proves no useful player layout. Main sampling follows this same process.

Official .NET 11 releases.json and download page rechecked 2026-10-30. Downloaded win-x64 SDK zip to ignored artifacts, verified SHA512 against release metadata, extracted locally and executed `--info`: SDK `11.0.100-rc.3.26522.148`, runtime `11.0.0-rc.3.26522.148`. This is RC, not GA. No machine-wide installation, registry or default SDK change. Actual compile compatibility is still pending.

Main baseline: 4/4, 183 samples, 20 same-HWND cycles, explicit exit true and zero unhandled exceptions. Settings screenshot inspected; source closure matches baseline (only docs/tests changed before activation), self-contained runtime 9.0.22. Owned registration removed, PID3360 exited, yield released=true.

Stage 1b: ten existing analyzer/generator declarations upgraded to latest stable, SDK pinned to 10.0.403. Initial build red: 442 errors, of which the newly introduced WinUIAnalyzer 0.7.1 reported WUI3001×309, WUI2011×74, WUI2010×52, WUI1001×1. Full candidate patch and red log preserved in artifacts. Enabling this new analyzer is BLOCKED by a separate whole-app MVVM/XAML migration; it was not present in the baseline, and its trial reference was removed rather than disabling any rule or existing analyzer. No compatible version is claimed. Existing analyzer upgrade revealed six diagnostics: guard three expensive logging arguments, remove redundant nullable access after a non-null check, move generic snapshot factory to a non-generic static class, and use jagged LCS storage. Existing snapshot/list/refresh tests cover preserved behavior. Fresh 487/0/0 and Debug x64 0 warnings/0 errors after fixes (`stage1-regression.log`, `stage1-tools-green.log`). No NoWarn or TreatWarningsAsErrors changes.

Controls baseline red is retained: first invoke was before UI readiness; ready retry ran all seven cases, six passed and cover physical-size assertion failed. This precedes native package upgrades; diagnosis is ongoing, not an upgrade regression claim.

Controls diagnosis: actual height 135.333328 DIP at 150% produces 203 target pixels; a 1600×900 source needs 361×203 to fill without distorting. Historical assertion incorrectly demanded width 360. New prepare wrapper expects exact 361×203 at verified 1.5 scale, preserving old fixture files. Seven native cases then pass, including WIC/unload/remount and two simulated device-loss callbacks. This rerun uses old native packages with stage-2 managed kernel packages, so is not an unchanged full baseline. Header/list screenshot inspected: populated three-column cards and undo/refresh header; fixture status path is clipped, not product UI. Owned PIDs14048/2040/24436 and registrations cleaned; yields true.

Stage 2: parser, logging, AgentKernel preview-only connectors and kernel managed dependencies upgraded. Markdig 1.5.1 has net8/netstandard assets; Protobuf 3.36.2 supports net8/netstandard; kernel TFMs remain net8/netstandard, compatible with the app's net9 consumer. First build had two MSB3277 assembly conflicts (plus original protobuf warning) caused by System.Drawing.Common 10 net9 assets referencing Metadata/Immutable 10 without package dependencies. Explicit System.Reflection.Metadata 10.0.14 brings aligned Immutable transitively; resulting Debug x64 build is 0 warnings/0 errors. Fresh suite 487/0/0; corrected the test logger's new ILogger notnull constraint, no test assertions removed. Kernel sample-only Hosting/Spectre references remain unchanged and outside the app closure.

Kernel managed commit `0235dc2d6eedd1de28ff7c59729f55512b9295ec`, local branch `integration/dependency-upgrade-d48`; bundle verification succeeds, prerequisite `6501985d0f30c55c28b3f78c6e045c2536e1d389`, includes merged cancellation/pageSize ancestry. Nested commit identity supplied per-command from the parent repository config (no global config edit). Later native stage may advance the same dedicated branch/bundle. No remote publication.
