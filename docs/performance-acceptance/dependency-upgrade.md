# Dependency upgrade / .NET 11 experiment

Completed local experiment; final decision and limitations are below. Base: `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`; kernel `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`. No remote mutations or changes to other checkouts.

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

Stage 3 compile checkpoint: WindowsAppSDK 2.6.4, Windows SDK BuildTools 10.0.28000.2957, Win2D 1.4.0, WinUIKernel preview4, FluentIcons 2.2.344, WebView2 1.0.4314.35 and Toolkit controls upgraded. Explicit stable references also align actual transitive MVVM 8.4.2, Animations/Extensions/Media 8.2.251219, WinUIEx 2.9.3, CsWinRT 2.3.1. No XAML edits. H.NotifyIcon 2.4.1 restore failed NU1202 because it only targets net10 Windows; net9 branch retains latest compatible stable 2.3.2. The index has no stable 2.4.0 (an attempted URL also returned 404); .NET 11 candidate will try 2.4.1.

Direct CsWinRT reference initially enabled unnecessary native projection generation and failed on absent local Windows SDK 22621 Platform.xml; downstream XAML missing-type errors were consequences. App consumes already-projected WinUI/Win2D assemblies. Set documented `CsWinRTGenerateProjection=false` while retaining source generators/analyzers and AOT configuration; this is not a diagnostic suppression. [Official package settings](https://github.com/microsoft/CsWinRT/blob/master/nuget/readme.md). Debug x64 now 0 warnings/0 errors; native acceptance remains pending. Updated kernel keeps its net8 Windows TFMs and compiles against the same AppSDK as the net9 host.

## .NET 11 isolated candidate checkpoint

Candidate branch `t3/dependency-upgrade-net11-candidate`, commit `73b744e` (full SHA in final handoff), based on stable `dbea98d`. App, Visor, Visor.Models and tests target net11; resource generator/UI.Models remain netstandard and kernel remains net8/netstandard. First restore failed NU1510 for the net9 Metadata workaround; candidate removes only that redundant direct reference. H.NotifyIcon 2.4.1 restores on net11. Production Debug x64 JIT: 0 warnings, 0 errors. Fresh net11 suite: 487/0/0, original protobuf unused-import warning during regeneration. No source-generator, XAML or protobuf compile failures remain in this candidate.

Native RC fixture builds with 1548 warnings / 0 errors versus baseline 1543 / 0. The five added VSTHRD103 diagnostics are synchronous file reads/writes in Fixture.cs, MainRuntimeChecks.cs and IdleBaseline.cs; no rule is disabled. Historical fixture warnings remain distinct from the production zero-warning gate. First 20-second READY wait timed out; PID35100 remained responsive, subsequent UI tree showed READY at scale 1.5. Retried same owned process: Player 15/15 passes, including H.NotifyIcon 2.4.1 create/disable/recreate. Main validation is still running. The new wrapper increases readiness allowance to 120 seconds outside measured phases; original fixture remains unchanged.

AOT has not been built: incomplete MSVC toolchain is the pre-existing blocker. JIT success does not prove native linking, trimming/AOT compatibility or AOT performance. No machine-wide SDK/toolchain or registry mutation occurred.

## Stable native / measurement checkpoints

Final stable dependency gates before the allocation follow-up: fresh VSTest 487/0/0, Debug x64 0 warnings/0 errors, explicit ValidateRuntimeDependencies exit 0, Release x64 JIT 0 warnings/0 errors. Main/test package graphs and DLL hashes are retained separately from fixture binaries.

Upgraded controls: initial MSIX registration failed because the task-only identity exceeded the manifest name limit (no process launched). Shortened to `BiliCopilot.DependencyUpgrade.Controls.D48Up`; PID36900 then passed 7/7. The wrapper explicitly pins actual production Win2D 1.4.0, CsWinRT 2.3.1, WinUIEx and Toolkit dependencies, preventing an old transitive graphics version from invalidating the replay. Full fixture build 121 warnings/0 errors, same count as the old fixture; incremental manifest rebuild 51/0. New native package checks preserve the un-cancelled WIC await/stream lifetime and reject stale results after decode. Two simulated device-loss callbacks are not hardware device-loss testing. Three-column cards and refreshed/undo header screenshot inspected; fixture status path clipping is not product-layout acceptance. Close succeeded, PID exited, registration removed and final yield released=true.

RC native completed: Player15/Main4 passed, 183 samples at scale1.5, 20 same-HWND loops, explicit exit with a live player succeeded, zero unhandled exceptions. PID35100 and its registration removed. Main yield returned released=false (lease already absent); this is retained exactly, not rewritten as true. All ten player captures plus SettingsPage inspected: synthetic blank media/fading controls/clipped right-edge danmaku limit visual conclusions, while the real SettingsPage renders text/icons correctly. No authenticated-home acceptance is claimed.

Three interleaved pure-list processes per variant (A,B,C,C,A,B,B,C,A), each 3 warmups and 10 measured prepend-two/undo pairs per size, exposed a small allocation regression: jagged LCS storage adds 104 bytes per pair at every size. Both changed middles can be empty after prefix/suffix matching; no LCS is needed when either side is empty. A separate follow-up will skip that allocation without changing key selection, matching ties, reference preservation or notification semantics. Pre-fix raw results remain `list-up-repeat*.json` / `list-net11-repeat*.json`; they must not be presented as the final fixed variant. RC did not improve this microbenchmark. No build/test duration is a UI performance metric.

## Final allocation / replay hardening evidence

The measured +104-byte regression was fixed in stable `0fceb503919cc81e255a67e30fe344fb6e94d2a6` and cherry-picked to RC `3367dd98a1285feb7e912eab22454be4397dbe32`. Empty changed middles skip LCS storage; existing insertion/removal/reference-preservation and `LongPrependAndUndoHaveBoundedAllocationAndPreserveReferences` tests remain unchanged. Fresh 487/0/0 on both TFMs. Final stable Debug and Release x64 JIT each 0 warnings/0 errors; explicit runtime-assets validation exits 0. RC Debug x64 also 0/0. RC native replay is specifically `73b744e`; the later pure-list-only commit has compile/test/microbenchmark coverage, not another RC native run.

Final pure-list interleaving repeats A,B,C,C,A,B,B,C,A with unchanged 3 warmups / 10 pairs per size. Allocations are deterministic across all three processes: original 9504/85104/421104 bytes, fixed net9 and fixed net11 9392/84992/420992 bytes at 100/1000/5000. The fix saves 112 bytes versus original and 216 bytes versus pre-fix jagged storage. This is small (112 bytes at every size), not a large-list memory claim. Timing ranges overlap between baseline and upgraded net9; RC shows no improvement here.

Post-sampling real subtitle settings check passes: initially zero choices, then 108 choices with `Fixture Missing Family` retained and selected on first display. The actual TeachingTip Loaded handler and font toolkit run. Screenshot inspected: selection/text visible; programmatic opening positions the tip against the top-left edge, so this is not acceptance of all pointer-driven flyout geometry. System font enumeration is real; no user/account settings outside owned LocalState were copied or modified.

One final-player attempt (`up2`) aborted before Main because the runner read capture.json during a non-atomic write. Saved exception text and owned cleanup; process was responsive, not a native crash. The hardened runner logs parse retries within the existing eight-minute deadline and waits for all fifteen results. The fresh `up2retry` then passed Player15/Main4, 183 samples, 20 same-HWND cycles, font check and explicit exit with zero unhandled exceptions.

Two baseline redeployments (`base2`, `base2retry`) crashed at startup with Windows Event1000, combase.dll, code 0xc00000fd. Investigation found 15 changed reference-project DLLs despite an unchanged app DLL. Their informational versions proved that recipe absolute paths injected current kernel `0ae5e4e` into baseline `98d0b9d`. Project --no-build **and** ordinary folder mode both consumed BiliCopilot.UI.build.appxrecipe. Full recipe, per-file hashes and crash events are retained. The minimal correction is deployment-only: freeze retained output, fill missing packaged content from the recipe without overwriting copied files, exclude the recipe from activation input, then validate every deployed DLL. This localizes the failure to the mixed deployment; no individual COM call is claimed as the root cause.

An intermediate frozen layout omitted recipe-only Assets/offline.wav (and other content/PRI), so playback checks timed out; its events and intentional cleanup are retained, with no Main samples accepted. The complete frozen baseline has all 357 DLL hashes matching and original kernel informational versions. Actual modules: WindowsAppRuntime1.8_8000.994.2142.0, Win2D1.3.2, runtime9.0.22, same libmpv v0.41.0-1227-ge471496c5. It passed Player15 and proceeds to Main. Upgraded `up2retry` was separately audited: all 354 DLLs match its final build, so the reference-evaluation problem did not contaminate that accepted run.


## Final decision and controlled results

All planned local dependency/RC experiments are complete. This is a compatibility delivery with **no demonstrated overall performance win**. The two final upgraded restored-window CPU rates are higher than both baseline runs; retain this as an open performance review observation, not a passed performance threshold. No numeric performance budget was specified.

- Stable production source: `0fceb503919cc81e255a67e30fe344fb6e94d2a6`; src tree `a5e099655ce21b460392e5a3d0b4f70feb1f8aec`. Later commits only add replay/evidence documentation.
- RC candidate: `3367dd98a1285feb7e912eab22454be4397dbe32`; SDK/runtime 11 RC3, not GA. Native RC evidence belongs to its predecessor `73b744e637c9dc18daf6538e5db71808cd868f9f`; the final pure-list fix was recompiled/retested on RC.
- Inventory: 146 declarations = 85 app/test references, 3 kernel sample-only references, 58 unused central declarations. 69 changed declarations/pins cover 66 distinct package IDs (src55, tests4, kernel10). Metadata has no unresolved items. The added Metadata direct pin upgrades a previously transitive 9.0.1 to 10.0.14; counts do not inflate every transitive change.
- SDK metadata rechecked 2026-10-30 12:00 UTC: latest stable SDK10.0.403/runtime10.0.14, current net9 runtime9.0.22, and SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148. See evidence/dotnet-channels.json and the checksum-pinned installer.

| Item | Actual latest attempt / evidence | Stable selection / disposition |
|---|---|---|
| H.NotifyIcon.WinUI | 2.4.1 restore NU1202 on net9; minimum asset TFM is net10 Windows | 2.3.2 latest compatible stable; 2.4.1 works in the RC candidate and native tray tests |
| New WinUIAnalyzer | 0.7.1, official NuGet index checked 10:51:19 UTC; 436 additional diagnostics | Not an original dependency; trial reference removed. Whole-app field-backed property/binding migration is outside this staged upgrade; no rule disabled |
| Kernel sample only | Hosting10.0.14 / Spectre0.57.2 latest metadata verified | Samples are outside app closure; old sample pins retained. RichasyKernel.Core sample pin already latest |
| AOT | Pre-existing incomplete MSVC toolchain | Not run or globally installed; JIT is not AOT validation |

| Final gate | Evidence | Result |
|---|---|---|
| Stable fresh suite after allocation fix | allocation-fix/allocation-fix.trx | 487 passed, 0 failed, 0 skipped |
| Stable Debug / Release JIT x64 | fixed-debug.log / fixed-release.log | Both 0 warnings, 0 errors |
| Runtime assets | fixed-runtime.log | Exit 0 |
| RC final suite / Debug JIT | net11-fixed/net11-fixed.trx / net11-fixed-build.log | 487/0/0; build 0 warnings, 0 errors |
| Final native controls | controls-final/results.json | 7/7; owned cleanup true |
| Final native player/Main | native-up2retry-* and native-up3-* | Each Player15/Main4; 183 samples; 20 same-HWND cycles; explicit exit true; unhandled0 |
| First subtitle font settings | native-up2retry-main/font-results.json and native-up3-main/font-results.json | Both 0→108 choices, saved missing family selected on first display |

Accepted fresh A/B uses this task's initial baseline and the fully restored baseline `base2complete`, versus `up2retry` and `up3`. Historical parent samples, RC, pre-fix up1, aborted up2, contaminated base2/base2retry and incomplete-content base2frozen are separate. All accepted phases have 61 rows, scale1.5, SettingsPage, notifications off, Player15 before Main, 5s warmup and 2s phase settling. Builds and microbenchmarks were not run during native sampling. Baseline DLL 48C69151… matches the first run; complete replay validates all357 baseline DLLs and all354 upgraded DLLs. Actual runtime/libmpv hashes match between stable variants; WinUI runtime1.8/Win2D1.3.2 versus runtime2.6.4/Win2D1.4.0 are recorded in loaded-modules JSON.

| Phase | Baseline CPU ms/s (2 runs) | Upgraded CPU ms/s (2 runs) | Baseline private MiB medians | Upgraded private MiB medians |
|---|---|---|---|---|
| visible | 55.29 / 64.04 | 61.36 / 75.19 | 472.10 / 464.02 | 469.18 / 478.12 |
| hidden | 20.57 / 19.02 | 23.62 / 18.50 | 470.76 / 463.22 | 468.06 / 477.29 |
| restored | 53.45 / 50.40 | 65.76 / 63.48 | 470.86 / 462.84 | 467.56 / 476.83 |

CPU is cumulative process CPU delta / actual elapsed seconds, not whole-machine percent. Restored CPU is 63.48–65.76 ms/s after upgrade versus 50.40–53.45 baseline: an observed increase in this narrow offline scenario, with only two runs per variant and no single-package attribution. Private-memory ranges overlap; GC is not forced. The sequence is not randomized, machine/thermal/cache state is not fully controlled, and the added font check runs only after sampling. No authenticated home, cold startup, physical DPI transition, hardware device loss, long-duration leak or general performance certification is implied. #49 pause remains FAIL; engine package versions are unchanged, so no claim that package updates solved it.

| Pure list size | Baseline run-median range ms | Fixed net9 range ms | Fixed net11 RC range ms | Baseline → fixed allocated bytes/pair |
|---|---|---|---|---|
| 100 | 0.01285–0.01380 | 0.01260–0.01410 | 0.01350–0.02060 | 9504 → 9392 |
| 1000 | 0.07780–0.08285 | 0.08000–0.08780 | 0.08050–0.09055 | 85104 → 84992 |
| 5000 | 0.18140–0.21495 | 0.18710–0.22455 | 0.23270–0.28550 | 421104 → 420992 |

Each range spans three independent process medians, each from ten pairs after three warmups. This does not establish fully warmed Tier1 JIT throughput. Fixed net9 timing overlaps baseline; RC is slower in the observed 5000-item workload. Only the small deterministic allocation saving is supported. Full per-iteration samples, source/runtime hashes, order ledger and replay code are retained.

Native red evidence is not discarded: two mixed-version deployment stack overflows were isolated by preserving original DLLs and removing recipe override behavior; a missing synthetic WAV and a partial-JSON reader race were corrected in replay code. Production code was not changed to hide these fixture failures. Every accepted result has explicit cleanup evidence.

Final cleanup audit: no task-owned DependencyUpgrade registrations or fixture UI processes remain. The final explicit desktop yield returned `released:false` (lease already absent), preserved verbatim in `evidence/final-yield.json`; this is not represented as a successful release. Other identities/processes were left untouched. Isolated SDK files remain ignored for reproducibility. No push, PR/issue mutation or parent-checkout merge was performed.
