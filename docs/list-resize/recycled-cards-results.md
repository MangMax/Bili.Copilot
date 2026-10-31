# Recycled list card binding repair

This is a focused continuation of candidate `711660de` and blocking independent review `e7667c1a`, both retained in this branch. The cache remains **0.5**. No production dependency, gitlink, image decoder, global cache or scheduler changes.

## Failure and repair boundary

The actual path is `AppRepeater` → package `ItemContentPresenterData` → outer `ContentPresenter` → the production card DataTemplate → `UserCardControl` (or another card). The valid repeater index belongs to the outer presenter, not the inner card.

Native red reproduction confirms three visible user cards remain blank after the original 30 fast scroll steps and the additional 100ms/1s waits. Unique user IDs, names, descriptions, commands and 500 different local avatar PNGs reproduce the same failure. On those cards:

* `ItemsSourceView.GetAt(index).Content` / presenter `Content` and card `DataContext` are the current item.
* `ViewModel`, avatar `Source`, name/description/ID and primary command are missing.
* The generated outer DataTemplate `IDataTemplateComponent` is absent on the bad root, while working roots retain it and the correct data root. Some reuse transitions never unload; the bad cards continue to receive current DataContext values without ever receiving a ViewModel.

This locates the break at the compiled root binding handoff during ContentPresenter template reuse, rather than source loading or UserCardControl's unload callback. Generated template code and bounded runtime component/lifecycle probes support this boundary; this is not a claim to have isolated a particular line inside the closed WinUI native implementation. Simply changing `x:Bind`'s update mode would still depend on that missing connector.

The production repair uses pathless `{Binding}` for the whole current item at the affected AppRepeater template roots. It tracks DataContext without relying on the missing compiled template connector and does not add reflection property paths. Internal typed property bindings remain unchanged. The same failure was observed for Video, Live, Article and Moment cards; the patch covers their AppRepeater entry points and all three User entry points (search/fans/follows), plus Popular's nested whole-item handoff. No unrelated page bindings or direct ItemsRepeater templates are rewritten.

## Verification

The final same-binary user pair (`final-red50`, `final-green50`) changes only the old/fixed DataTemplate. The strengthened old template fails; both fixed User50/500 pass all 62 snapshots, and their 12 final visible avatar pixel checks each pass.

| PNG resize run | User50 max heartbeat | User500 max heartbeat | cache during resize |
| --- | ---: | ---: | ---: |
| repair (`resize-green`) | 121.90ms | 121.17ms | 0.5 |
| same repair, full-cache control (`resize-cache20-policy`) | 78.11ms | 859.30ms | 20 |

The control was rebuilt only to correct its override lifetime: the earlier `resize-cache20` assigned internal cache20 directly, then production remount correctly reapplied policy0.5, making the final row cache and expected remount assertion misleading. That complete run remains archived (62.89/862.14ms, remount-cache failures). The corrected control sets the same attached policy to20, and verifies cache20 through remount. Its User500 `RemountImagesPresent` still fails at the fixed one-second check; this additional control red is retained without inferring a cause or changing the production gate. Both control runs are the repaired build with a cache override, not the untouched parent. The resize input keeps the original PNG, dimensions, name/description and trajectory; the adapter ID text is seven digits instead of the historical five, identically in both current arms. Native tests use packaged production controls/templates with offline adapter models, not authenticated pages. Unique identity: `BiliCopilot.Recycle9F8D.Owned`.

The final strengthened oracle checks every intersecting expected card (including partially visible cards), the outer index/item, VM object identity/current ID, actual text controls, avatar URI, and exact main command object. It never filters out null ViewModel, Source or command. Final settled avatar pixels are checked against each item's independently generated color. Immediate image readiness is reported separately from binding correctness.

`final-red50` and `final-green50` use the same final binary and differ only in the selected old/fixed user template. `red-original50` is the unmodified candidate fixture replay; `red-unique50` proves the failure is not masked by identical stub values. The earlier diagnostic `outerCorrect` used presenter DataContext incorrectly and is retained as such; final checks correctly use ItemsSourceView.GetAt(index).Content. Its other red observations (correct presenter Content/card DataContext but null VM) remain independent of that diagnostic mistake.

A separate 500-item red/green pair is also replayable with `ExtraRed -Family User500` / `MatrixGreen`; this adds only a test-selector branch, with no production changes.

The original PNG resize workload is separate from the 96×96 unique-avatar rapid workload. It retains 120 native width changes, 16ms requested heartbeat, 800ms settle, and the original **200ms** gate for both User50 and User500. A heartbeat is not composition frame time and passing this gate does not mean 60fps or universal performance improvement. User50 is slower than the fully retained cache20 control in prior evidence; do not replace this limitation with the gate result. All historical red timings remain in the candidate/review archives.

## Requirement coverage

| Requirement | Evidence |
| --- | --- |
| User50/500 red → green, all visible current items including null | `EveryVisibleUserCardMatchesCurrentItem`: `final-red50`/`final-red500` fail, `final-green50`/`final-pair-green500` pass; each red/green pair has the same Harness.dll hash. Both red final viewports contain 3/12 unbound cards. |
| Distinct current IDs, avatar URI/pixels, text and exact primary command | `RapidTrajectoryComplete`, `SettledVisibleAvatarsReady`, `VisibleAvatarPixelsMatchCurrentItem`; 62 snapshots/run, 500 distinct deployed avatar hashes, 12/12 settled pixel samples per fixed run. |
| Same failure in other card families | `EveryVisibleRelatedCardMatchesCurrentItem_Search`, `_Live`, `_Article`, `_Moment`: original rapid cases fail, repair cases pass. `_Notify` and `SettledRelatedImagesReady_*` pass; notification entrance is unchanged. Related models use VM/command reference identity; only User has unique per-item avatars, so no stronger related pixel claim is made. |
| User50/500 unchanged resize gate | `UserResizeOriginal200msGate_50`, `_500`: both pass in `resize-green`; raw 120 width steps and heartbeat arrays retained. |
| Basic/Blur remount and original size | `BasicRepeatedUnloadSourceReplacementAndRemount`, `BlurRepeatedUnloadSourceReplacementAndRemount`, `NonListOriginalSizeAndRoundedAvatarPreserved`: pass in `covers-green`. `ImageDiskCacheReusedAcrossRecycledInstances` uses the existing offline HTTP adapter, not a public-network measurement. |
| Exact undo and recommendation sizing/cancellation | `UndoExactReferenceSequence`, `UndoExactViewportAnchor`, `FinalPhysicalPixelDecode`, `PhysicalCoverageWithoutUpsampling`, `SourceClearedDuringResizeRejectsOldResult`, `UnloadCancelsPendingResize`, `RemountLoadsCurrentSize`: pass in `undo-green`. |
| Owned exit/cleanup | `OwnedRetainedExitAndWER`, `OwnedCleanupAndYield`: pass for all 15 final validation/control processes, including expected functional reds. `audit.json` checks all 22 own runs' source snapshots, PID/path absence >=31s, WER, cleanup, recorded build intervals and unchanged initial kernel state. |

`final-summary.json` records 11 successful final native runs, two expected old-template reds and two retained cache20 control reds. These are scoped native cases, not a rerun or replacement of the historical 572-test evidence. Functional assertions were strengthened and recomputed against the saved raw outputs; native workloads were not rerun merely to change the report count.

## Retained limits

The six-family diagnostic process completed Notify/Search/Live/Article, then hit unhandled `E_FAIL` mounting Season. A new-process Season-only run also failed before scroll assertions. Moment was then tested in its own process. These red runs and partial data are preserved; no fixture-error explanation is asserted and no unrelated lifecycle/layout repair is included. Notification had no binding failures in this focused run, so its template entrance is not changed; its existing internal ActiveCommand OneWay fix remains. Season has no completed new rapid validation, and its entrance is not changed. Comment, filter and other non-card templates are not claimed covered by these native cases. Overall all-page/multi-pane acceptance remains open.

Minimal next step for the retained E_FAIL: fix one template, data set, size and lifecycle, then vary only shared/independent layout or single/dual pane, with exception phase recorded. Do not infer a shared cause or multi-pane safety from this repair.

Earlier own diagnostic runs inherited the review runner's 32-second PID/path absence check without a retained exit code. Final acceptance runs explicitly retain the process handle and record HasExited/ExitCode, exact PID/path, >=31-second WER observation, owned package count zero and yield. Early records are not retroactively upgraded. Pixel-check script's first PowerShell array-expression error is retained separately from its corrected rerun.

Production clean Rebuild: exit 0, one existing protobuf unused-import warning, zero errors. No new analyzer package was installed; WinUI analyzer and Native AOT publication are not claimed. Source snapshots, binary hashes, recorded build times, native times and kernel initial state are retained. The first diagnostic build has an exit/log but no start-time receipt; non-overlap verification applies to builds with recorded intervals. The binaries were built from the uncommitted repair snapshot: raw git-head receipts therefore name the pre-repair merge, while production-input SHA256 values pin the actual edited build inputs. The abnormal initial kernel HEAD/index are untouched, and all compilation uses a detached source tree under this checkout's artifacts. Parent/source worktrees were read only. No AotDev, Caa6a7be/Dev/official package, existing instance, login data or trial package was touched. No push/PR/issue operation.

## Replay

Use a fresh checkout containing this commit and a read-only kernel repository containing `0ae5e4e`. The preparation script rejects an existing evidence directory and assigns a new owned identity. It unpacks only source/assets, not a prebuilt executable.

```powershell
python docs/list-resize/recycled-cards/prepare.py --kernel-repo C:/path/to/bili-kernel
# Exact existing package versions, x64; no dependency upgrade.
dotnet build artifacts/recycled-cards/host/Harness.csproj -t:Rebuild -c Release -p:Platform=x64 -p:NuGetAudit=false
powershell -NoProfile -File docs/list-resize/recycled-cards/run.ps1 -Stage red -Action ExtraRed
python docs/list-resize/recycled-cards/check.py artifacts/recycled-cards/red # expected exit 2
powershell -NoProfile -File docs/list-resize/recycled-cards/run.ps1 -Stage green50 -Action Focus
python docs/list-resize/recycled-cards/check.py artifacts/recycled-cards/green50
powershell -NoProfile -File docs/list-resize/recycled-cards/pixels.ps1 -Stage artifacts/recycled-cards/green50
powershell -NoProfile -File docs/list-resize/recycled-cards/run.ps1 -Stage red500 -Action ExtraRed -Family User500
python docs/list-resize/recycled-cards/check.py artifacts/recycled-cards/red500 # expected exit 2
powershell -NoProfile -File docs/list-resize/recycled-cards/run.ps1 -Stage green500 -Action MatrixGreen
python docs/list-resize/recycled-cards/check.py artifacts/recycled-cards/green500
powershell -NoProfile -File docs/list-resize/recycled-cards/run.ps1 -Stage resize -Action MatrixRed
python docs/list-resize/recycled-cards/check.py artifacts/recycled-cards/resize
```

Optional focused actions: `ExtraGreen -Family Search|Live|Article|Moment|Notify` (one process per family), `ImageChecks`, `ResizeChecks` (real Popular undo), and `MatrixRed -Family Cache20` (same fix, only internal cache changed; not an untouched parent build). Preserve functional checker exits separately from runner exits. Do not run builds concurrently with native sampling. The old Matrix's nonnull-image subset is not sufficient acceptance; the new focused checker is mandatory for the recycled-card regression.

## Handoff

The new production diff is 29 binding expressions in 26 XAML files, with no production C# changes. The candidate/review merge is `f2fe07d0`; production fix commit is `29acb496dc03749849414972227a7533194651d3`. Latest parent `feat/performance-45` was merged/rechecked at `90e215f0` (already contained). Committed source blobs match the pinned inputs after explicit CRLF-to-LF normalization; both raw and normalized SHA256 manifests are retained. This implementation agent did not start a subagent. Hand the frozen commit to the parent for the requested single independent review; this report is implementation evidence and self-review, not that independent approval.

The compact [evidence archive](recycled-cards-evidence.zip) contains the replay host/assets, per-run source snapshots and raw arrays, primary native screenshots, startup binary hashes, exact exits/WER/yield, build logs, the preserved E_FAIL/control reds, and a SHA256/length manifest. Entry points after extraction: `recycled-cards/final-summary.json`, `recycled-cards/audit.json`, and the final red/green directories. No runtime binaries or copied full source/dependency tree are archived.
