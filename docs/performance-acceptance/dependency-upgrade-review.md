# Independent dependency / .NET 11 review

Reviewed 2026-10-30. Decision: **HOLD the aggregate stable candidate; do not merge it into the parent baseline. Keep RC experimental.** This is an evidence/acceptance hold, not a finding that a particular upgraded package has a proven production defect. No numeric performance threshold was supplied or invented.

## Scope and method

- Baseline `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`; stable `60abad7834f7037501fcb99696494d3dcebb8977`; RC `3367dd98a1285feb7e912eab22454be4397dbe32`.
- This independent review checkout fast-forwarded to the stable candidate. Source checkout `t3-d48c749d` remained unchanged. No subagents, production edits, UI deployment, desktop/registration/account operations, remote writes, or PR/issue operations.
- Read AGENTS, docs/agents, code-review, receiving-code-review, winui-code-review, verification-before-completion; inspected full acceptance report, README, replay/archive/inventory scripts, manifests, production diff, relevant existing tests and raw evidence. run-tests was read; no .NET tests/builds rerun because the unresolved questions concern evidence provenance and native performance rather than a new code edit. Historical TRX results below are not fresh test execution.
- Recomputed native rates directly from raw cumulative CPU/timestamps, list medians from individual iterations, and archived hashes independently of summarize.py. Requeried official NuGet indexes for all 66 changed IDs plus H.NotifyIcon (four transient failures succeeded on retry). Frozen metadata remains the basis for historical latest-at-check-time claims.

## Standards findings

### P2 — Final RC binary provenance is historical, not an immutable retained production artifact

`dependency-upgrade/evidence/net11-fixed-provenance.json` points at the source checkout's shared Debug output. On this review, its DLL and runtimeconfig exist but neither matches the recorded RC hash: that path now holds the stable build. Stable Debug/Release hashes in `fixed-production-provenance.json` both match actual files. The separate native RC fixture's five recorded hashes (app, runtimeconfig, coreclr, tray, Canvas) still match.

This does not invalidate the historical RC compile/TRX logs, and does not turn the native fixture into final-RC evidence. Before any final RC adoption/redistribution, retain its output in a commit-specific immutable directory with a closure ledger, or explicitly label the final production binary as no longer retained. Do not claim the existing shared path can verify final RC bytes. No native rerun is needed solely to repair this archival statement.

### P3 — Five archived launch JSONs cannot reproduce their original byte hashes

`evidence/artifact-hashes.json` covers 448 entries. This checkout contains 352 corresponding raw/decompressed artifacts; 96 are not archived there (large/local artifacts are explicitly documented). Of the 352, 347 reproduce the original hash and byte count. Five do not: `baseline-controls-launch.json`, `baseline-launch-2.json`, `baseline-launch.json`, `controls-diagnostic-launch.json`, `controls-fixed-launch.json`.

Verified the original local files in `t3-d48c749d/artifacts/dependency-upgrade`: all five match the ledger. They contain LF JSON plus a CRLF terminator; Git normalization changes the archived bytes (both committed blobs and checked-out files differ from originals). This is not evidence of altered measurements. Suggested parent fix: gzip these originals as byte-preserving evidence, or record separate original/normalized hashes and explicitly document normalization. Do not silently replace original ledger values. Accepted idle/list payloads and closure ledgers did not have these mismatches.

No new blanket suppression, existing analyzer removal, lifecycle teardown deletion, or documented repository standard breach was identified in production changes. Skill heuristics are not treated as hard repository rules.

## Spec / performance findings

### P1 acceptance hold — Restored-window CPU increase remains unresolved

The final stable runs each have 183 rows, exactly 61 per phase, all scale 1.5; clean explicit-exit evidence records zero unhandled exceptions. Formula independently used: `(last.cpuMilliseconds - first.cpuMilliseconds) / (last.utc - first.utc).TotalSeconds`. There are 60 intervals, not 61 independent experiments.

| Phase | base | base2complete | up2retry | up3 |
|---|---:|---:|---:|---:|
| Visible CPU ms/s | 55.28792 | 64.04232 | 61.35683 | 75.19376 |
| Hidden CPU ms/s | 20.56830 | 19.02484 | 23.61694 | 18.50399 |
| Restored CPU ms/s | 53.44615 | 50.40178 | 65.76479 | 63.48083 |

The reported rounded CPU and private-memory numbers reproduce. Restored upgraded rates exceed both baseline rates. Keep this signal open; do not call the aggregate upgrade a performance win or a passed performance budget. Nor can two runs per variant establish a specific production defect or identify a package culprit.

Nonstationarity is visible without discarding samples: restored first-half/last-half rates are base **42.70/64.17**, base2complete **44.73/56.08**, up2retry **55.43/76.12**, up3 **52.43/74.53** ms/s. The up3 visible halves are **97.07/53.35**. Five-second warmup and two-second settling are matched recipe conditions, not proof of a steady process. Thermal/cache/background activity and order remain uncontrolled. Do not retrospectively select just the lower half or pool rows to manufacture significance. Memory ranges overlap and no forced GC/trim was used.

### P2 evidence gap — Pure prepend/undo does not exercise the new jagged LCS allocation path

`src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs:109` now skips storage if either trimmed middle is empty. That is semantically sound: the original reconstruction loop could not execute in that case. Prefix/suffix matching, key extraction, tie direction (`>=`), descending removals, ascending insertions and reference retention remain unchanged.

For **both nonempty** middles, jagged storage still has quadratic element count, but allocates a row object per current-middle row plus the outer reference array. It can allocate more and exercise GC differently than the former single rectangular array. The measured prepend-two/undo workload takes the empty-middle shortcut, so its 112-byte saving cannot validate the performance of this changed nonempty-middle path. This is a coverage gap, not a demonstrated timing regression. A small managed comparison with representative replacement/reorder middles is enough to investigate; native UI reruns are unnecessary for that question. Do not describe the change as general LCS memory optimization.

Existing `RecommendListDiffTests` cover empty/identical lists, insertion/removal, mixed changes, moves/reversal, duplicate occurrences, original references and incremental notifications. Snapshot tests still assert copied items/offset/null source. The unchanged long-prepend allocation test allows nearly 4 MB; it cannot detect +104 or -112 bytes. No new exhaustive tie-case or nonempty-middle allocation evidence is present in the original 487. The old 487 are useful behavior gates, not coverage of all package APIs, authenticated navigation, AOT or native lifecycle behavior.

## Dependency selection and compatibility assessment

Independently reconciled all 146 inventory rows against actual manifests: **85 app/test referenced, 3 kernel sample-only, 58 unused central**; **69 changed declarations/pins / 66 unique IDs**, area counts src55/tests4/kernel10. Compare exact old/selected version expressions (do not normalize only one side of bracketed Moq versions). No selected-manifest mismatch or captured metadata error. Official rechecks agree with captured latest stable, or latest preview where no stable exists, for all 66 changed IDs. This is timestamped metadata, not a promise that latest never changes.

- Stable is preferred where available (including moving Serilog off its dev channel). Preview-only AgentKernel connectors remain preview; the term stable candidate does not mean every dependency is GA. H.NotifyIcon is the only app/test referenced row deliberately below the recorded latest selection.
- Unused central declarations are not effective runtime upgrades; do not inflate counts by claiming every central/transitive entry was upgraded. Kernel Hosting/Spectre are sample-only, remain old pins, and are explicitly outside app closure; RichasyKernel.Core sample declaration is already latest. These exceptions are accurately described.
- H.NotifyIcon.WinUI 2.4.1 nuspec has only `net10.0-windows10.0.17763` dependency group, consistent with preserved NU1202 on net9. The captured index contains no stable 2.4.0; latest stable is 2.4.1 and net9-compatible stable selection 2.3.2 is justified. Pre-release 2.5 entries do not supersede that stable-channel decision. RC's 2.4.1 tray evidence belongs to its native fixture.
- The new WinUIAnalyzer 0.7.1 trial produced 436 additional diagnostics and was removed. It was not an existing baseline analyzer; removing that trial is **not disabling an existing rule**, and the 436 diagnostics are **not resolved**. Existing analyzer upgrades/fixes and their clean production gates are separate.
- Metadata 10.0.14 direct pin is a justified net9 alignment workaround: preserved MSB3277 logs identify Metadata/Immutable 9 versus 10 references after Drawing.Common upgrade; aligned build/graph retain 10. It is an explicit new pin, not an old direct dependency. RC removes its redundant direct reference after NU1510, while the unused central entry can remain. Keep pin and Drawing.Common upgrade together in any split.
- CsWinRT 2.3.1 targets condition the native projection-generation target on `CsWinRTGenerateProjection`, while AOT optimizer/compiler-visible properties are defined independently. `false` does not set `CsWinRTEnabled=false`, disable Roslyn generators or suppress warnings. No custom winmd/projection-input contract was found in the app project. The app consumes supplied projected assemblies; this is defensible for the reviewed JIT closure. It is not AOT validation. See [official property semantics](https://github.com/microsoft/CsWinRT/blob/master/nuget/readme.md).

Logging changes add IsEnabled guards and structured parameters without dropping state mutations; the removed `videos?.Select` is after `videos is null || videos.Count == 0` returns. Snapshot factory relocation retains copying and all known call sites/tests are updated. No behavioral regression was found in these source hunks. The constructor's version lookup is now skipped with information logging disabled, which is the intended lazy logging behavior.

## Source, deployment and SDK provenance

- Stable `60abad78` and `0fceb503` share src tree **a5e099655ce21b460392e5a3d0b4f70feb1f8aec**; src/tests/kernel have no later delta. Accepted up2retry/up3 source records identify `0fceb503`. Baseline initial label is `bcd47fd`, whose src diff against eff8d24 is empty; base2complete carries `bae42e81`, also pre-upgrade. These labels must not be mistaken for different production algorithms.
- Native RC is **73b744e637c9dc18daf6538e5db71808cd868f9f**, not 3367dd9. `git diff 73b744e 3367dd9` contains exactly one file, RecommendListDiff.cs (empty-middle allocation follow-up). Final RC has compile/test/pure-list evidence; it has no final-commit native replay. Reuse unaffected native observations with this explicit delta, not a claim that the exact final RC was natively tested.
- Rehashed retained 336,948,730-byte SDK zip: SHA512 exactly matches installer and official captured download record, `dae0a0450d92b5315034db134259acffe9ec0043d93dced4fb2a9b8700520e87a99005d6845ad80716f4a8f30df24ae5363ca7c9be391797b4f53170e9b2c088`. Executed isolated dotnet `--info`: SDK 11.0.100-rc.3.26522.148 and host/shared runtime 11.0.0-rc.3.26522.148 under the task-local sdk11 directory. This probe does not claim the stable checkout selects that SDK: its global.json pins 10.0.403. RC git diff pins SDK and app/Visor/Visor.Models/tests to 11; kernel remains net8/netstandard. Native RC runtimeconfig is self-contained net11 RC3 and retained coreclr hash matches. RC is not GA.
- Read closure ledgers: base2complete **357/357**, up2retry **354/354**, up3 **354/354** build/deployed DLL hashes agree. Complete baseline kernel Services.Media informational version is `98d0b9d`; upgraded is `0ae5e4e`. Loaded base/up coreclr hashes both **03A85B6B…28DD1C** and libmpv both **04E4D27D…F7842F4A**. Native AppSDK/Canvas change remains a real experimental variable (1.8/1.3.2 to package 2.6.4/1.4.0); loaded Xaml ProductVersion is 2.0, so distinguish package version from module product version.
- Excluded contaminated base2/base2retry (recipe reference override), missing-content base2frozen, aborted up2, pre-fix up1, historical parent samples and RC from final stable A/B. Full recipe/hashes/crash evidence support deployment contamination; successful frozen baseline removes that explanation from the accepted replay. They do not establish the exact COM faulting call or a new production package defect. Original accepted base has less comprehensive closure evidence than restored base2complete; do not generalize the latter's 357-file audit to the former.
- summarize.py scans all clean native runs; its output also contains RC/pre-fix runs. Final analysis must continue using the explicit four-run allowlist, not all rows in summary.json. Frozen recipe handling preserves retained DLLs and fills missing content; closure validation checks BuildOutput DLLs, not every conceivable extra file. Do not call it cryptographic attestation of the entire machine/runtime environment.

## Independently recomputed pure-list results

Three independent processes per variant, interleaved A,B,C,C,A,B,B,C,A; each size has three warmups and ten measured pairs. All final per-iteration allocations agree within each variant/size.

| Size | Baseline process-median range ms | Fixed net9 range ms | Fixed RC range ms | Baseline / fixed bytes per pair |
|---|---|---|---|---|
| 100 | 0.01285–0.01380 | 0.01260–0.01410 | 0.01350–0.02060 | 9504 / 9392 |
| 1000 | 0.07780–0.08285 | 0.08000–0.08780 | 0.08050–0.09055 | 85104 / 84992 |
| 5000 | 0.18140–0.21495 | 0.18710–0.22455 | 0.23270–0.28550 | 421104 / 420992 |

Supports only deterministic **112 bytes/pair** saving versus original (216 versus pre-fix jagged). Net9 timings overlap; RC shows no throughput advantage, and is slower in this observed 5000-item workload. Three warmups are not a Tier1 steady-state guarantee; raw iteration outliers remain. This workload includes no native packages and cannot identify the restored-window CPU cause.

## Kernel and existing lifecycle safeguards

Saved initial nested staged-deletion patch in ignored review artifacts, then restored only this review checkout's nested repository. `git bundle verify` succeeds with prerequisite **6501985d0f30c55c28b3f78c6e045c2536e1d389**. Fetched bundle locally; HEAD **0ae5e4e000a41a07f6696746706df11c06e8835b**. Both cancellation `2ca79bd` and pageSize `f02c33f` ancestor checks return 0. Relative to baseline kernel `98d0b9d`, only Directory.Packages.props changes (10 declarations). This establishes local recoverability, **not remote reachability**.

PR27/43 controls, remount hooks, WIC decode lifetime and Pinner unload protection have no source diff. WIC still awaits decode without cancelling its WinRT wrapper, checks cancellation afterwards and holds stream ownership through completion. MainWindow delta is logging only; tray exit workflow is unchanged. Recorded controls 7/7 and Player15/Main4/20 same-HWND loops support the tested native paths under new packages. They do not prove physical device-loss recovery, physical DPI transitions, long leaks, authenticated-home behavior, or all pointer-driven flyout geometry. The 361×203 assertion correction is fixture geometry at 150%, not a product decode change.

Parsed baseline/final stable/final RC TRX counters independently: each 487 passed, 0 failed, 0 skipped. Keep **#49 pause FAIL, AOT blocked by missing toolchain, authenticated home unverified**. No package evidence resolves them.

## Minimal discriminating next step and adoption boundaries

1. Do not rerun all builds or all packages. Preserve current evidence as enough for metadata selection, net9 tray exclusion, SDK identity/checksum, local bundle ancestry, logging/nullability/snapshot review, empty-middle allocation saving, and the already-tested native compatibility scenarios. Repair archival prose/hashes without UI work.
2. Split tool/analyzer/test migrations and their necessary source fixes from native-runtime upgrades and RC. Treat managed Drawing.Common + Metadata/Immutable as one compatibility group; keep kernel package closure recoverable. Separate AppSDK host/kernel alignment, Win2D, CsWinRT, Toolkit/WinUIKernel/WinUIEx/icons/WebView groups where dependency resolution permits. Do not force incompatible single-package combinations just to obtain a nominal A/B. Keep H.NotifyIcon 2.3.2 on net9; RC-specific 2.4.1 and TFM/SDK changes stay isolated. Any split needs its own resolved graph and focused gates; this review does not approve arbitrary cherry-pick combinations.
3. First native discrimination: same net9 runtime/libmpv, same production source and managed graph, compare the final candidate against **only Win2D reverted to 1.3.2**, provided restore confirms that coherent closure. Otherwise select the smallest resolvable native group, explicitly listing its delta. Reuse the recipe-free frozen-output validation. Run repeated interleaved process launches (e.g. A,B,B,A, two per arm to start) with identical Player-before-Main, Settings/notification/DPI conditions, no concurrent builds, and record start order/environment and early/late CPU segments. This is a discrimination experiment, not a newly invented acceptance threshold or a guaranteed sufficient sample size.
4. If that group changes the signal consistently, narrow within compatible boundaries or collect a focused CPU profile; if not, move to AppSDK host/kernel runtime group while holding everything else fixed. Avoid interpreting an inconclusive pair as exoneration. Only rerun relevant controls/remount/WIC checks when their graphics/runtime closure changes; tray lifecycle when tray/AppSDK closure changes. No reason to redeploy for logging or pure-list archival fixes alone.
5. RC adoption is not recommended now: performance benefit is unproven, final binaries are not immutably retained, native run predates the list-only fix, and AOT remains blocked. A future runtime comparison must hold source and tray version constant (2.3.2 works as the common selection), state unavoidable framework/package pruning differences, and repeat interleaved native runs. If exact-final-RC native certification is requested, it requires a fresh immutable final-RC fixture and targeted native acceptance; do not relabel 73b744e as 3367dd9.

Document wording corrections for parent: qualify “all planned experiments complete” as completion of the recorded run sequence, not closure of the restored-CPU signal; retain the accurate distinction between removed new-analyzer trial and unresolved 436 diagnostics; label final RC output as historical/overwritten and native as predecessor-only. No production changes proposed solely to make a benchmark pass.
