# Short User resize follow-up

Fixed review integrated: `f81fa979` (candidate `1797c94a`, parent `90e215f0`). This follow-up changes only the implicit AppRepeater vertical cache from 2 to 0.5. The image lifecycle and notification OneWay fixes remain included. No image decoder, source URL, crop, Blur implementation, package version, or seek code is replaced.

The `cache1` and `final-*` stage names below are retained historical attempts, **not final acceptance**. The later single-process JPEG/WebP/AVIF batch exposed WebP heartbeat maxima of 202.66/206.20ms at cache1. This red rejected cache1. Final validation uses the `half-*` stages and the unchanged 200ms gate; its results are recorded separately below.

## Causal investigation

All new User comparisons use the same production template, fixed 1.5 DPI, 120-step physical window-width trajectory, stable realization/image starting gate, matching ScrollCompleted correlation ID and 100ms scroll checkpoint. Timing includes the existing 800ms settle after resize. Process sampling is external to the UI. Builds finish before the owned app starts; each runner observes its exact PID/path for at least 32 seconds after closing and then unregisters only its owned layout and yields.

The existing AotDev process was observed in the background and never operated on. This is not an isolated-machine benchmark or a statistical confidence interval. Raw arrays and all attempts, including failures, are retained.

| Local NuGet run | User50 heartbeat max ms | User500 heartbeat max ms | Initial realized 50 / 500 | Resize draws 50 / 500 |
| --- | ---: | ---: | --- | --- |
| baseline, cache20 | 75.54 | 1395.43 | 50 / 103 | 0 / 79 |
| lifecycle only, cache20 | 72.13 | 1147.23 | 50 / 103 | 0 / 79 |
| lifecycle only, full Rebuild | 68.47 | 864.40 | 50 / 103 | 0 / 79 |
| reviewed candidate, cache2 | 170.62 | **222.16** | 22 / 22 | 20 / 20 |
| unchanged large-source repeat (`small-source`, failed preparation) | 194.49 | 148.23 | 22 / 22 | 20 / 20 |
| cache2, only source changed to 100×56 PNG | 142.08 | 195.19 | 22 / 22 | 20 / 20 |
| cache1, original 1600×900 PNG | 123.10 | 125.62 | 16 / 16 | 9 / 9 |

The fixed review's independent User50 79.27→209.59ms and earlier 84.79→267.47ms remain valid adverse samples. This run does not erase them: cache2 still worsens short-list tails relative to cache20, although not every sample crosses 200ms. Cache1 reduces that burst and is tested against the unchanged 200ms gate. Its short-list maximum remains above the fully retained baseline: this is a bounded-work tradeoff, not a claim that every metric improves.

Only-lifecycle cache20 runs retained failed `RemountImagesPresent` checks at the fixed 1-second checkpoint (both counts in the first attempt; User500 after full Rebuild). They are not functional all-pass evidence. Cache1 User50/500 functional checks passed in the focused run. The old baseline's blank remount image remains a visual negative.

## Decode / surface / realization timeline

`p1-instrument-loader.py` builds a **diagnostic source copy**, from the exact public commit in the NuGet nuspec (`3f1086f6cfa8c1e0c3a64b930a8577dc5496c616`). It adds in-memory timestamps at the actual CanvasBitmap.LoadAsync submission/await, surface creation, DrawImage and ItemsRepeater preparation/clearing boundaries. Its namespace and template registration are adapted for the host; an internal logger is replaced with a diagnostic sink. It is **not the shipping NuGet binary**, and its timings are not used for final performance acceptance. The source hashes and copied files are retained. No decompilation, dump, or process-memory inspection was used.

In `loader-trace-template` User50, the largest heartbeat gap is 1552.496→1842.588ms (290.092ms). Thirteen `ElementPrepared` events occur at 1556.538–1639.440ms; new surface calls then occur at 1650.763–1779.799ms. The first native decode is submitted at 1840.605ms, near the **end** of the gap. Across resize, native decode submission takes at most 0.501ms; asynchronous decode lifetime at most 53.088ms; individual surface creation at most 0.309ms (remount/recreate 0.378ms); individual draw at most 3.751ms. Overlapping async durations must not be summed into UI stall time.

This locates the large gap in the realization/Loaded work preceding decode completion, rather than proving that full-resolution decode alone caused it. Small-source results and the reduction from 20 to 9 resize draws support reducing the offscreen realization burst. The precise breakdown inside XAML/template/Loaded work is not claimed. No global image cache or new scheduler was introduced.

## Format realism

The production user-search adapter requests 96×96 via `CreateUserProfile(..., 96)`; `ImageAdapter.ToImage` appends `@96w_96h_1c.jpg`. The user's web markup instead prefers 50×50 AVIF, then WebP, then an img fallback. Browser `loading=lazy` and `decoding=async` are not equivalent to this WinUI pipeline.

Anonymous requests for the official default-avatar URL with the project's 96×96 suffix returned `image/jpeg` and JPEG magic; the equivalent `.webp` suffix returned `image/webp` and RIFF/WEBP magic, and `.avif` returned `image/avif` and ftypavif magic. Response headers, hashes and the local fixed samples are retained. These samples supplement the original large PNG; they do not replace its red evidence. Native batch evidence establishes AVIF loading on this machine through the existing CanvasBitmap path, not universal codec availability on every supported Windows installation. This change does not switch production image formats.

## Intermediate cache1 representative matrix (retained)

`final-main` has 592 passing functional assertions; `final-extra` has 222. All 22 scenarios pass the unchanged 200ms resize heartbeat gate. Each has 27 matching ScrollCompleted checkpoints, zero pending visible images at those checkpoints, append/end-of-list-command/remount checks and current primary-command binding. These checkpoints do not establish that every compositor frame is free of placeholders.

| Family | User-visible template path exercised | Heartbeat max ms, 50 / 500 | Realized at stable start, 500 |
| --- | --- | ---: | ---: |
| Recommend | RecommendVideoCardStyle | 39.31 / 42.17 | 7 |
| Hot | HotVideoCardStyle | 42.00 / 65.21 | 7 |
| Partition | video partition template | 43.07 / 44.75 | 7 |
| Search | video search template | 43.92 / 44.21 | 7 |
| Live | RecommendLivePresenter | 108.12 / 79.40 | 10 |
| Article | PartitionArticlePresenter, cover + avatar | 144.16 / 175.22 | 7 |
| User | user-search template + UserCardControl | 143.40 / 136.69 | 16 |
| Stack | player playlist template | 41.03 / 62.83 | 6 |
| Moment | VideoMomentPresenter | 42.76 / 48.91 | 7 |
| Season | IndexSeasonPresenter / Blur | 70.75 / 50.53 | 11 |
| Notify | NotifyMessageCardControl | 38.13 / 37.48 | 5 |

Article500's 175ms is higher than the previous cache2 archive's 139ms, while remaining below 200ms and far below the original cache20 937ms. Cross-session variance and background activity preclude claiming monotonically better timings for every individual template. Long-list realization remains bounded, with the original cache20→candidate improvement preserved; this is not a 60fps guarantee.

## Source coverage versus native coverage

| Requirement / path | Source coverage | Native evidence |
| --- | --- | --- |
| Shared AppRepeater policy | Same attached policy and 40 existing XAML uses; only the implicit style value changes | 11 representative families × 50/500; `AppliedCache`, `RemountCache`, `ScrollRealizedVisible`, `ScrolledImagesPresent`, `ContinuousLoadingCommand`, `AppendPreserved` |
| Other live/article/history/favorite/fans/follows presenters | Same policy, inventory in `inventory.md`; no claim that every page was opened | Distinct representatives above, not authenticated navigation through all presenters |
| Comprehensive dynamic, comment/reply, AI and small WrapLayout lists | Same source policy where AppRepeater is used; WrapLayout remains nonvirtualizing | Not separately exercised; no blanket latency result for these presenters |
| Direct ItemsRepeater / ItemsView / ItemsControl | No new policy applied | Source inventory only, not a performance claim |
| Basic/Blur recycling and source replacement | Fixed reviewed lifecycle implementation retained | `BasicRepeatedUnloadSourceReplacementAndRemount`, `BlurRepeatedUnloadSourceReplacementAndRemount`; decoded pixel hashes equal for same-source remount, different for replacement |
| Non-list large image, rounded avatar, crop and cache | Existing decoder/surface/crop/source/cache code retained | `NonListOriginalSizeAndRoundedAvatarPreserved`, `ImageDiskCacheReusedAcrossRecycledInstances`; original-size visual additionally repeated in a taller fixture window |
| Notification recycled command | Fixed reviewed OneWay binding retained | `Notify/50/RemountPrimaryNavigationBinding`, `Notify/500/RemountPrimaryNavigationBinding` |
| Implicit style priority | Existing explicit-style limitation unchanged | `pane-fixed` records `StylePrecedence`: implicit=1, explicit=20, PASS **before** the separate pane failure |

This is actual production-template native acceptance with deterministic local data adapters, not a claim of complete authenticated application-page coverage. The original inventory and independent review's remaining coverage limitations continue to apply.

## Bounded two-pane diagnosis

`pane-fixed` replaces only the synthetic fixture's placement with a nested, finite 1000×400 DIP grid (400/600 DIP columns), retaining both production User templates, 500 items each, shared production UserLayout and production scroll wrappers. SizeChanged records confirm actual left=400×400 and right=600×400. It still raises unhandled COM E_FAIL before independent scrolling assertions. PID 54112 exited and was observed for 32 seconds; exact-layout WER is empty. The exception and partial files remain archived.

This does **not** eliminate template minimum-size behavior, shared-layout interaction, or a fixture defect as possible causes. It establishes only that explicitly supplying those finite pane sizes does not cure this failure. The fixed review already has baseline/candidate failures; no new production layout architecture was added. Precise next isolation: in this same finite fixture, replace only `templates["User"]` with a 220×84 text-only DataTemplate, then (if still failing) change only the right layout to a newly allocated UniformGridLayout with the exact UserLayout properties. Preserve both failures and measure inputs before attempting a production change. This follow-up is unresolved and is not included in the passing matrix count.

## Errors and visual limits retained

The failed Pillow preparation produced no small source; its following successful build/run therefore remains labelled an unchanged large-source repeat. A subsequent runner quoting error prevented launch. The diagnostic source build first failed because the package logger is internal; the next launch failed `TemplateRoot not found` before the diagnostic template was explicitly registered. All corresponding logs/partial outputs are retained. No failed build output was launched.

The first large-image capture in `final-extra` was clipped by the fixture viewport, despite correct original surface dimensions. It is retained and not used to claim full-image visual fidelity. RenderTargetBitmap also omits some composition-backed backgrounds; original-pixel cover/avatar crops prove content and clipping, not full-window theme correctness. Owned-window screenshots supplement these captures.

## Real-size supplements and exact undo

The 96×96 JPEG baseline has User50/500 heartbeats 69.60/851.12ms (0/79 resize draws, 50/103 realized). The paired cache1 run has 128.34/115.02ms (9/9 draws, 16/16 realized), all functional checks passing. The separate initial WebP supplement has 136.94/138.64ms with the same 9/9 draws and 16/16 realized. Because these use a different source image from the large synthetic PNG, they are not a pure codec-speed comparison.

`final-large-image` repeats all four image checks in a window tall enough for the original-size control. The new finite-fit assertion passes, and the 1200×675 original-pixel capture was inspected without the previous bottom clipping.

`final-undo-verified` uses the actual PopularMainBody and the real implicit cache style, without assigning cache in the probe. All three populated runs report the inner repeater's cache as `[1]`, preserve collection count/updates, and pass the unchanged responsiveness gate. The seven checks are `UndoExactReferenceSequence`, `UndoExactViewportAnchor`, `FinalPhysicalPixelDecode`, `PhysicalCoverageWithoutUpsampling`, `SourceClearedDuringResizeRejectsOldResult`, `UnloadCancelsPendingResize`, `RemountLoadsCurrentSize`. The earlier `final-undo` also passed these seven checks; the repeat adds the explicit policy assertion.

On the user's request, JPEG/WebP/AVIF are additionally run together from one build and one owned process, with matching 96×96 public CDN content and the same width/scroll/settle protocol. This batch supplements, rather than discards, the earlier format runs. Its complete results are recorded after execution.

## Remaining validation

Final cache0.5 native matrix, targeted/full tests, production build and final self-review are recorded below after execution. This ledger does not yet claim pending checks passed.

## Final cache0.5 native evidence

All 22 representative family/count cases pass the original 200ms resize heartbeat gate, with 120 width steps, 27 matched scroll completions, 1.5 DPI, zero pending visible images at the checkpoints and passing remount/append/command assertions. Use `half-main` for its 14 non-User cases, `half-extra` for six cases, and `half-user-png` for the original large-PNG User pair. `half-main` also contains a passing JPEG User supplement; do not substitute it for the PNG pair.

| Family | Heartbeat max ms, 50 / 500 | Initial realized, 50 / 500 |
| --- | ---: | --- |
| Recommend | 50.77 / 43.46 | 7 / 7 |
| Hot | 41.31 / 45.53 | 7 / 7 |
| Partition | 40.51 / 58.67 | 7 / 7 |
| Search | 39.50 / 41.32 | 7 / 7 |
| Live | 93.65 / 131.41 | 7 / 7 |
| Article | 55.70 / 85.43 | 7 / 7 |
| Stack | 83.01 / 44.61 | 6 / 6 |
| Moment | 42.61 / 42.52 | 7 / 7 |
| Season | 68.53 / 62.71 | 11 / 11 |
| Notify | 38.58 / 40.56 | 5 / 5 |
| User | 136.23 / 123.62 | 13 / 13 |

The PNG User pair has eight resize draws each and scroll heartbeat maxima 82.15/76.29ms. The initial cache20 baseline has 50/103 realized and 0/79 resize draws; reviewed cache2 has 22/22 realized and 20/20 draws. The smaller cache bounds the realization/image burst without changing the loader or restoring all-cache retention. It does not eliminate the short-list cost relative to fully retained baseline.

### Same-process format batch: retained red and final green

Each batch loads all three actual 96x96 public CDN files in one owned native process. All formats report zero ImageFailed events and only 96x96 decoded dimensions. Formats run in a fixed JPG/WebP/AVIF order, not randomized; these samples establish functionality and the unchanged responsiveness gate, not a general codec-speed ranking. Network fetch is outside timing; the native path uses fixed local files.

| Format | Cache1 red batch, User50 / 500 ms | Cache0.5 final batch, User50 / 500 ms |
| --- | ---: | ---: |
| jpg | 168.97 / 151.46 | 119.66 / 158.06 |
| webp | 202.66 / 206.20 | 165.86 / 148.37 |
| avif | 148.26 / 152.52 | 147.09 / 96.37 |

The final batch has 13 realized items and eight resize draws for each count/format. AVIF is usable through the current native pipeline on this machine; no production format switch was necessary to resolve this measured regression.

### Preparation failure and provenance boundary

The inline source-switch command before `half-main` raised `SyntaxError: unterminated string literal`; PowerShell continued into the build. P1Formats.cs was edited during that build. Although the subsequently captured source contains Cover.png, the compiled User avatar remained JPEG, verified visually. That snapshot is not proof of the compiled avatar default. This discrepancy and the failed command are retained in `half-main-preparation-error.txt`; the final original-PNG pairing comes from the subsequent full clean rebuild (`build-half-final`) and `half-user-png`. No sources were edited during that rebuild. Other templates in `half-main` retain their original source inputs.

`half-extra` repeats all four native image semantic checks in the corrected tall window; the original-size fit assertion passes. Two-pane remains unresolved as described above. No performance scenario overlaps this task's builds. Final validation and integration records follow.

### Reproduction in a separate owned worktree

Extract `independent-evidence.zip` under `artifacts` (its `list-review/host` is the starting fixture), and `p1-evidence.zip` under `artifacts` for the fixed public avatar samples and retained evidence. Use fresh stage names; the runner refuses existing outputs or an already registered owned identity. The diagnostic loader uses the pinned public source and its separate script; it is not required for final acceptance.

Run `python docs/list-resize/p1-prepare.py final all`, then `p1-formats.py`, `p1-large-image.py`, and `p1-undo.py` in that order. Build once with `p1-build.ps1 -Stage <fresh-name>`. With that same build, invoke `p1-run.ps1` sequentially using distinct stage names and actions `Focus` (all three formats), `MatrixRed` (the final PNG User-only pair), `MatrixGreen` (main families), `ExtraGreen` (remaining families plus image checks), and `ResizeChecks` (exact undo). The reused button labels avoid changing the fixture viewport; their final handlers are preserved in the source snapshots. Wait for each runner's exit observation/unregister/yield before starting another. Do not rebuild during timed scenarios.

`p1-verify.py` validates the recorded final stage names; `p1-audit.py` also displays historical reds. `p1-pixels.ps1` compares exact decoded pixels for same-source/replacement Basic and Blur images and retains unscaled avatar crops with the original blank baseline as negative. Original-size, Blur, AVIF avatar and owned-window final captures were visually inspected. `p1-test-build.ps1` runs targeted/full tests and the Release project Rebuild after owned performance cleanup. `p1-archive.py` preserves all attempts and verifies every archived file's SHA-256.

### Test and build preparation records

Targeted VSTest execution passed 113/113; full execution passed 572/572, with no failures or skipped cases. Their TRX counters were parsed independently (`trx-validation.json`); targeted cases are a subset, not additional distinct tests. The filter and exact build command are preserved in `p1-test-build.ps1`.

The first production Rebuild failed the runtime dependency guard because this worktree lacked `Assets/libmpv/x64/libmpv-2.dll`; its log and exit=1 remain in `production-build.*`. Only local runtime assets were prepared using the existing source worktree `t3-6a3c2729` as a read-only source. Copying stopped at an already-present file rather than overwriting it; the resulting three destination files were individually checked against the known SHA-256 values. The preparation guard and verification are retained. `runtime-assets.json` records paths/hashes; payloads are not in the evidence archive. A fresh Rebuild is recorded separately as `production-build-retry.*`. This setup does not change package references or any installed application.

## Final verification and handoff

- Final native verifier: PASS (`verification.log`). The three actual Popular runs contain 70/220/520 entries, report implicit cache `[0.5]`, preserve count and zero collection updates, and have heartbeat maxima 58.42/44.76/40.75ms. All seven exact undo/physical-pixel/source-change/unload/remount checks pass.
- Final original-pixel comparisons: PASS (`pixels-half/checks.json`), including Basic and Blur same-source equality and replacement inequality. Positive and blank negative avatar crops, full-size non-list image, Blur and AVIF native captures were inspected.
- Targeted tests: 113 passed, 0 failed, 0 skipped. Full tests: 572 passed, 0 failed, 0 skipped. TRX counters verified, not summed as distinct coverage.
- Production Release x64 Rebuild with PublishAot=false: PASS on retry, 0 errors and 1 unused-protobuf-import warning, elapsed 2m09.59s. Original missing-runtime-asset failure remains retained. No AOT publication or installed-package update was performed.
- Every recorded owned run exited and was observed for at least 32 seconds with exact PID/layout-path WER checks; no matching WER entries. Recorded build/test intervals do not overlap owned run intervals (`exit-and-build-overlap-validation.json`). Earlier builds without timestamp JSON remain documented by sequential execution, not retroactively fabricated timestamps.
- Fixed review `f81fa979` integrated. Latest parent `feat/performance-45` is still `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`; merge reports already up to date. Only this worktree's initially abnormal kernel was backed up and restored to `0ae5e4e`; the backups remain under `artifacts/list-p1/initial`, with hashes in provenance. Parent/source worktrees were read-only.
- Source self-review: [p1-self-review.md](p1-self-review.md). `git diff --check` passes. This is one implementer handoff for review, not an independent approval. Two-pane E_FAIL remains explicitly unresolved with the bounded next isolation above.
- Complete retained raw evidence: [p1-evidence.zip](p1-evidence.zip); comparison: [p1-comparison.html](p1-comparison.html). Archive script verifies each entry against its SHA-256 manifest. No push, PR, issue update or release.
