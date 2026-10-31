# Shared list resize work

Follow-up to fixed review `f81fa979`: [short User P1 investigation and retained attempts](p1-results.md). The original report below remains historical evidence; the follow-up records the smaller shared cache, JPEG/WebP/AVIF native batch, and separate unresolved two-pane diagnosis.

Baseline: `90e215f0`. Shared fixes implemented; native functional acceptance passes for the representative matrix. Performance is **not all-pass**: the user/avatar family still crosses the original 200ms serious-stall line, and the separate two-pane layout diagnostic remains unresolved. See [results.md](results.md), [comparison.html](comparison.html), and [review.md](review.md).

## Page → template → shared path coverage

The full machine-generated call-site inventory is [inventory.md](inventory.md). It lists every XAML collection container, its layout and descendants, and every image use including non-list readers/gallery/player surfaces. Run `python docs/list-resize/inventory.py` to regenerate.

The C# reference audit additionally finds `RecommendationCoverImage.Cache`, a static BasicCoverImage used only for cache-file helpers; it is never mounted and does not enter the new load/unload path. No additional programmatically constructed visual lists/images were found outside these declarations and event-handler type references.

| List family | Production entry points / templates | Shared layout and image path | Native representative |
| --- | --- | --- | --- |
| Recommendation / popular | PopularMainBody → Recommend/Hot/RankVideoCardStyle | AppRepeater + VideoLayout; RecommendationCoverImage only for recommendation, BasicCoverImage otherwise | Existing actual PopularMainBody command-driven harness; matrix Recommend/Hot |
| Video partition / search | VideoPartitionMainBody / VideoSectionDetailControl → Partition/SearchVideoCardStyle | AppRepeater + VideoLayout + BasicCoverImage | Matrix copies each exact typed DataTemplate and production style |
| Video collections | UgcFavoriteBody, history/search-history, ViewLaterBody, UserSpaceVideoSearchSection | Same AppRepeater + VideoLayout; distinct video styles, BasicCoverImage | Shared policy coverage; these distinct styles are not individually native-tested |
| Direct favorite video list | VideoFavoriteBody | Plain ItemsRepeater + VideoLayout | Already platform cache policy; not affected by AppRepeater override |
| Live | LiveRecommendMainBody, LiveSubPartitionMainBody, LiveSectionDetailControl, LiveHistorySection; followed live in LivePartitionMainHeader | AppRepeater + LiveLayout (history uses VideoLayout; followed live horizontal StackLayout), LiveCardControl presenters, BasicCoverImage | Real RecommendLiveCardStyle + RecommendLivePresenter; other live presenters share policy, not separate template tests |
| Article | ArticlePartitionMainBody, ArticleSectionDetailControl, ArticleHistorySection | AppRepeater + ArticleLayout (history uses VideoLayout), ArticleCardControl presenters, BasicCoverImage cover and avatar | Real PartitionArticleCardStyle + PartitionArticlePresenter |
| User/account | UserSectionDetailControl, FansBody, FollowsMainBody | AppRepeater + UserLayout + UserCardControl, 48px rounded avatar / DecodeWidth 100 | Exact user-search typed template and production UserCardControl |
| Dynamic | VideoMomentSectionDetailControl / VideoMomentSpaceControl; ComprehensiveMainBody / ComprehensiveMomentSpaceControl | AppRepeater + MomentLayout or default StackLayout; MomentCardControl and image/gallery descendants | Real VideoMomentCardStyle / VideoMomentPresenter; comprehensive rich content is same cache policy, not separately exercised |
| PGC | AnimeTimelineMainControl, EntertainmentIndexMainControl, PgcFavoriteBody, PgcSectionDetailControl | AppRepeater + PgcLayout, SeasonCardControl, BasicCoverImage or BlurCoverImage | Real IndexSeasonCardStyle / IndexSeasonPresenter and blur surface |
| Player lists | VideoRecommendSection, VideoPlaylistSection, VideoSeasonSection, PgcSeasonsSection, PgcEpisodesSection | AppRepeater + StackLayout/UniformGridLayout, existing video/episode/season templates | Real PlayerPlaylistVideoCardStyle in StackLayout |
| Ordinary message lists | NotifyMessageBody; comment main/detail | AppRepeater + StackLayout/default; text and optional avatar | Real NotifyMessageCardControl with text-only notification data; comments share policy, not separate native template tests |
| Other small lists | AI quick items; PGC filters; FavoriteButton; VideoDescriptorControl | AppRepeater + StackLayout/WrapLayout | Same global policy; nonvirtualizing WrapLayout cannot gain virtualization from cache length |
| Platform lists | Sidebar ItemsView, direct ItemsRepeater in chat, image gallery, pinner, emote, player overlays | Platform defaults; no forced 20-viewport cache | Inventory and source inspection; no blanket migration or claimed performance gain |
| Small nonvirtualized collections | Player chapter marks and SponsorBlock category settings | ItemsControl with small collections; no AppRepeater cache | Inventory/source inspection, not a claimed performance improvement |

## Dependency and root causes

The actual `Richasy.WinUIKernel.Share` 0.0.12-preview4 nuspec pins upstream commit `3f1086f6cfa8c1e0c3a64b930a8577dc5496c616`. Source was fetched from that exact public commit, not decompiled. Files are retained in `artifacts/list-resize/dependency`.

* `AppRepeater.xaml` forces `VerticalCacheLength=20`. It wraps an actual `AppItemsRepeater`; `Stretch` adjusts measured width but does not enumerate the complete collection. Layout resources are not mutated by page resize handlers. This source finding is not a native proof that simultaneous shared layouts are safe; see the unresolved diagnostic below.
* `ImageExBase` uses `CanvasBitmap.LoadAsync`, with no cancellation token on the native await. `DecodeWidth` is the subsequent surface size, not a WIC decode-size request. `BasicCoverImage` preserves image aspect ratio in that surface and relies on the base UniformToFill brush for clipping. There is no size-change decode handler. Application startup disables the optional background path. Its `LocalDecodes` counter only increments in that background path, so zero is **not** evidence of no decode.
* Source changes use the existing URI/cache/request-identity path. Recommendation is a separate WIC pipeline; the prior timer, global two-slot budget and token-free native awaits are unchanged.
* Native baseline `matrix-red-1` fails `Hot/50/RemountImagesPresent`. The pinned `LayoutControlBase` removes its Loaded/Unloaded handlers on the first unload; `ImageExBase` clears its brush reference and surface, while retaining its initialized composition sequence. Reconnecting only the events and brush therefore still fails: `TryInitialize` skips the discarded surface. `ReloadableCoverImage` repairs repeated lifecycle entry, reconnects the existing template brush, and recreates that surface with the existing dimensions and tracked alpha mode; Basic and Blur share it. It does not replace download, cache, decoding, crop, or effects.

## Controlled feedback loop

Single-variable diagnostic uses the existing actual PopularMainBody/IncrementVerticalScrollControl and fixed local 1600×900 image. `common-red-hot` retains the package's 20-view cache; `common-cache-probe` only sets 2. At 50 items, realized 50→20, resize p95 67.2→19.2 ms and heartbeat max 131.7→62.9 ms. This is diagnostic evidence, not a 60fps promise.

The broader matrix uses production card classes, copied unchanged typed page DataTemplates, production layouts, real Basic/Recommendation/Blur images and a real IncrementVerticalScrollControl. Data/service adapters provide local immutable fixture values; they do not replace layout or image rendering. It is template-path acceptance, not authenticated page navigation. Native commands are invoked in an owned window only. Resize uses external-thread SetWindowPos, 120 steps, fixed 1.5 DPI and the same width trajectory. Background process samples are outside the UI. Performance runs do not overlap builds.

Each family checks short/long lists, finite viewport, applied cache, realized visible items while scrolling down/up, settled images after scrolling, incremental command at the end, collection append, unload/remount and screenshots. Baseline failures are retained and reported per assertion. Raw timing arrays are authoritative; measurement is not a frame-rate test. A visible container at a 100ms checkpoint does not prove every compositor frame is free of placeholders.

## Implemented changes

`Styles/Repeater.xaml` adds one implicit AppRepeater style; `RepeaterCachePolicy` sets only its named inner repeater's vertical cache at property change/load. There is no tree scan on resize, no collection replacement, no item cap, no copied per-page scheduler and no horizontal cache change. Direct ItemsRepeater/ItemsView retain their platform policy. `ReloadableCoverImage` handles image lifecycle across recycling. A native recycling assertion additionally exposed the notification card's once-bound primary command retaining the previous item; its existing `x:Bind` now uses `Mode=OneWay`. No dependency versions, player/seek/danmaku packages, credentials, or other registered applications are modified.

## Failed experiments and limits

The first candidate (`green-final`) only reconnected image lifecycle events and the brush. All 14 Basic-image remount assertions still failed. That run is retained as a failed candidate, not final acceptance. The final candidate additionally recreates the discarded surface. Independent Basic and Blur tests compare the original and same-source remount PNG bytes, repeat four unload/source-replacement cycles, and require different pixels after source replacement. Large-image dimensions, a rounded avatar and disk-cache reuse are separate assertions.

`green-extra-verified` passed the image checks but failed `Notify/50/RemountPrimaryNavigationBinding` and `Notify/500/RemountPrimaryNavigationBinding`. It is retained as a failing functional candidate. `green-extra-navigation` repeats the same six scenarios after changing only that production command binding to OneWay. The test compares the actual rendered button command to the current item's command; it does not invoke external navigation.

`SharedLayoutKeepsIndependentViewportContexts` is **unresolved**, not a passing check. A synthetic simultaneous two-pane user-card fixture raises an unhandled XAML `MeasureOverride` COM E_FAIL. Increasing the first pane from 220 to 400 DIP, assigning an independent layout to the second pane, using an observable object collection, and setting finite viewport heights did not eliminate it. Thus these experiments do not establish shared Layout as the cause. The probe has been separated into `RunSharedLayoutDiagnostic`; it is excluded from the four passing image-semantics checks, not silently counted as a pass. Partial assertions, exception traces, precise owned process paths, WER and exit observations are archived. Production layout instances are unchanged.

`red-final` is invalid: it was launched from stale/mixed outputs after a failed host build and recorded a XAML access violation. It is not used for performance comparison, and the build provenance does not prove the cause of that crash. Valid baseline stages are `red-settled` and `red-extra`. Earlier diagnostic crashes and partial stages remain archived; no dump, process-memory inspection or decompilation was used.

Baseline scrolling sampled immediately after an asynchronous `ScrollTo`, so its `ScrollRealizedVisible` failures do not demonstrate persistent blank viewports. Final green scrolling waits for the matching `ScrollCompleted` correlation ID, then samples after 100ms. Resize timing remains matched; old/new scroll latency is **not** a matched comparison. Green checks cover large jumps and 24 continuous forward/reverse steps, settled images, end-of-list command dispatch, append, remount, and the primary navigation command binding. They do not establish zero placeholders on every compositor frame or full authenticated navigation/context-menu behavior.

The original Basic/Blur pipeline still decodes the full source using CanvasBitmap. Reducing realized offscreen cards reduces this work but does not add a WIC thumbnail decoder or a new global scheduler. No claim is made for animated-image playback, real-network download/save flows, forced GPU-device loss, cross-monitor DPI changes, AOT performance, or every distinct presenter listed in the inventory. The original-size image and HTTP/disk-cache checks are narrower regression evidence, not substitutes for those paths.

## Final verification

`verify.py` recomputes max/p95 from the raw samples, checks the matched 22 scenarios, fixed DPI, cache values, complete trajectories, all final functional assertions, four image-semantics checks, >=31-second exits and empty exact-path WER. It returns **2**, deliberately, for the remaining User/50 and User/500 heartbeats of 267.47 and 275.59ms. It does not turn those into passes or use the old popular-page threshold to exclude other families. Full native details are in [results.md](results.md).

The real command-driven PopularMainBody regression uses the actual shared style, not a probe assignment to its inner cache. Three populated-list runs (actual counts 70/220/520 after production incremental requests) preserve their starting count during resize and pass the original responsiveness gate. Seven additional assertions pass: `UndoExactReferenceSequence`, `UndoExactViewportAnchor`, `FinalPhysicalPixelDecode`, `PhysicalCoverageWithoutUpsampling`, `SourceClearedDuringResizeRejectsOldResult`, `UnloadCancelsPendingResize`, `RemountLoadsCurrentSize`. The recommendation source remains unchanged from the parent.

Two earlier runs (`shared-final`, `shared-pixel-diagnostic`) failed the old test's size oracle, not an undersized image: at ActualHeight 198.6666717529297 and DPI 1.5, the production request first rounds physical height to 299 and then preserves aspect ratio, yielding 532×299. The old oracle skipped that first quantization and expected 530×299. `prepare-undo.py` now checks the exact requested-pixel contract and separately requires sufficient coverage without upsampling. The failure and diagnostic are retained; production recommendation code and thresholds were not changed to pass it. Final stage is `shared-verified`.

Targeted .NET tests: 98 passed; fresh full suite: 572 passed, 0 failed, 0 skipped. The targeted names are a subset of the full suite, not an additional 98 distinct tests. Native UI assertions are separate because the headless suite does not instantiate the real image/layout controls. Fresh production Release x64 JIT full Rebuild passed: exit 0, 1 existing protobuf unused-import warning, 0 errors, 3m28.56s. No new warnings were suppressed. The larger host-only warning sets are retained in their logs and are not production build warnings.

The final original-pixel Live/Article/User/Moment/Season images were inspected alongside retained blank-image negatives. Basic/Blur same-source remounts also compare exact rendered PNG hashes; replacement sources must change pixels. RenderTargetBitmap captures can omit composition-backed card backgrounds, so they prove cover content/crop, not full-window theme fidelity. Owned full-window screenshots and raw captures are retained separately.

## Reproduction

Use a fresh `artifacts/list-resize/host`; scripts refuse overwriting a stage. First prepare the existing offline host with `./docs/resize-diagnosis/prepare.ps1`. Then:

```powershell
python docs/list-resize/prepare.py
python docs/list-resize/extend.py
dotnet build artifacts/list-resize/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
./docs/list-resize/run.ps1 -Stage red-main-new -Action MatrixRed
./docs/list-resize/run.ps1 -Stage red-extra-new -Action ExtraRed
python docs/list-resize/candidate.py
dotnet build artifacts/list-resize/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
./docs/list-resize/run.ps1 -Stage green-main-new -Action MatrixGreen
./docs/list-resize/run.ps1 -Stage green-extra-new -Action ExtraGreen
python docs/list-resize/verify.py red-main-new red-extra-new green-main-new green-extra-new
python docs/list-resize/prepare-undo.py
dotnet build artifacts/resize-diagnosis/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
./docs/resize-diagnosis/run.ps1 -Stage shared-new -Action ResizeChecks
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:NuGetAudit=false --logger 'trx;LogFileName=full.trx' --results-directory artifacts/list-resize/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:Rebuild -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:NuGetAudit=false "-p:SolutionDir=$PWD/src/"
```

Current reproduction includes the stronger ScrollCompleted and primary-command checks; archived baseline used the earlier scroll checkpoint. Exact source snapshots are kept with each native stage. Builds/tests do not overlap performance sampling. The owned runner records PID/path, observes exit for 32 seconds, queries matching Application WER events, unregisters only its own layout and yields the desktop. No protected installed app, credential layout, parent checkout, push, PR or issue is touched.

The initially abnormal kernel state was backed up before restoring **only this worktree's** copy to the required `0ae5e4e000a41a07f6696746706df11c06e8835b`; final kernel status is clean and its gitlink is unchanged. Missing x64 libmpv/ffmpeg/BBDown build assets were copied from the authorized source Assets directory, with equal SHA256 recorded in `runtime-assets.json`. No installed package was read or replaced. Evidence is packaged by `archive.py` with a per-entry hash manifest and verified by `audit.py`.
