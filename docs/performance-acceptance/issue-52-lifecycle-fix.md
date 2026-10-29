# Issue 52: inline image lifecycle correction

Scope: repair the real HTTP/MyImage blank-image failure reported by [ui-52.md](ui-52.md). Production changes are confined to `MyImage.cs`; no loader limits, renderer, kernel, XAML, credentials, deployment policy, or production identity changed.

The independent checkout started at `a236aaa2266156804651223743444a0418af4822`. Acceptance-only commit `4c5dc7ceab440c038b176ff98b724dfdc01c4e6e` was cherry-picked as `78d527d`; all original reports and `evidence-52` remain intact. Integration `1b1c1945103de1f1fa423b7d7c26ffd22dfaccc0` was merged as `5201e08c3b8e5e361437ab8045f709f3fb499996` before final verification. Kernel remains `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`.

## Root cause and minimal change

[Fresh red lifecycle trace](evidence-52-fix/red/lifecycle52.json) records sender, OriginalSource, host/child identity, visual parent chains, UTC ordering, and a deferred observation. For HTTP host **13716049**, sender is that Border and OriginalSource is **null**, not an identified bubbling child. The initial Loaded callbacks see `IsLoaded=false`; the deferred observations see true. Once Source is BitmapImage, further Loaded/Unloaded callbacks see the same Border still loaded and parented to RichTextBlock **2279925**. The old handler replaces children **22547203**, **24678187**, **54940109**, leaving Source null. Deferred Unloaded observations still show the connected host. In contrast, the genuinely removed preceding host **52377683** has `IsLoaded=false` and no visual parent.

The mechanism is inline layout detach/reattach with queued lifecycle notifications. Upstream [ParagraphNode](https://github.com/microsoft/microsoft-ui-xaml/blob/0a03f68d586d1c100f753ca3221597c3ee550f4a/dxaml/xcp/core/text/BlockLayout/ParagraphNode.cpp) removes embedded elements when rebuilding its layout; [InlineUIContainer](https://github.com/microsoft/microsoft-ui-xaml/blob/0a03f68d586d1c100f753ca3221597c3ee550f4a/dxaml/xcp/core/text/TextLayout/InlineUIContainer.cpp) attaches/detaches its child through the embedded host. These upstream sources corroborate the observed mechanism; they are not claimed to be symbols for the installed binary. Managed identity hashes are diagnostic identifiers, not native allocation identifiers.

`MyImage` now coalesces a dispatcher lifecycle check. Loaded starts work after the state settles, so a synchronous cached provider is accepted. An Unloaded with a currently loaded host schedules the same check instead of destroying an already reattached image. A genuinely unloaded host still immediately cancels the operation, removes size/root subscriptions, and replaces its child without mutating a provider-owned Source. The deferred check releases if unloaded and starts/reconciles if loaded. Existing operation identity and cancellation checks still reject old completions; no cancellation framework was added.

Resize exposed the same timing problem. [Resize diagnostic](evidence-52-fix/resize-diagnostic/lifecycle52.json) shows MarkdownTextBlock, Grid and RichTextBlock changing **480→300** while the inline host reports false. `RefreshForDisplay` therefore skips that SizeChanged. The deferred lifecycle check now rechecks display constraints before the already-loaded guard can suppress work. The existing width/DPI equality guard prevents a self-triggering load loop.

## Correct measurement and readiness

Project-scoped `winapp find-api` output is retained in [api](evidence-52-fix/api). It resolves Microsoft.UI.Xaml from restored Windows App SDK **1.8.251106002**, excluding the installed mismatched 2.6 runtime metadata; its separate machine Windows SDK fallback caveat is retained verbatim.

The historical Geometry52 equality `PixelWidth == DecodePixelWidth` was invalid on this runtime. Source pixels remain 2400×1200, 1200×2400 and 2000×2000 after reduced decode requests. Geometry52 now separately asserts positive source dimensions/aspect, display aspect, physical requested decode bounds (4096 per axis, 4,194,304 pixels), and display-size × actual DPI agreement within rounding tolerance. It does **not** claim measured native decode allocation or native peak memory.

Readiness is not an immediate observation. The stream bitmap is published by production only after awaited `SetSourceAsync`; acceptance requires positive source dimensions and positive loaded layout. URI provider images retain the ImageOpened requirement. SVG requires the actual DefaultSVGRenderer return, SvgImageSource and positive layout; exact ISO-8859-1 decoded input `café` is saved separately. Source event probes subscribe before SetSourceAsync, then assign the preloaded source to an Image: both bitmap and SVG source events fire, while Image.ImageOpened does **not** fire again. SVG SetSourceAsync reports Success. The original boundary experiment, which assigns the bitmap before loading, still waits for ImageOpened. These different paths are explicitly recorded, not relabelled as the same event.

The SVG's visible output is a teal rectangle; **café glyph pixels are absent** with the unchanged DefaultSVGRenderer. Correct input decoding is not a visible-glyph pass. SvgImageSource rasterization defaults are non-finite on this runtime (recorded as text); serializing them as JSON numbers caused the earlier harness reporting failure.

## Preserved execution history

All attempts use a fresh owned layout and evidence directory. Failed directories are not overwritten; pre-final harness snapshots preserve their assertions.

| Directory | Outcome / meaning |
| --- | --- |
| `red` | Unchanged production reproduces blank HTTP image, with full event-origin/parent-chain diagnostics. Close exceeded the original five-second bound; later verified exited and exact temporary package removed. |
| `green-a` | First lifecycle correction preserves visible bitmaps and restores all eight original lifecycle checks. ImageOpened-based HTTP checks still fail; runner also matched an immediate SVG status. Close exceeded five seconds; later verified exited and removed. This directory's name is not a passing claim. |
| `green-b` | Subscribing before Source assignment confirms no Image.ImageOpened replay for preloaded stream bitmaps. Terminal-only runner added. SVG reporting still fails on non-finite JSON numbers. Cleanup passes. |
| `green-c` | Corrected source/layout readiness and JSON measurements: 25/26 pass; resize exposes missed SizeChanged. |
| `resize-diagnostic` | Three geometry cases pass; resize red records ancestor width changes while inline is temporarily unloaded. |
| `final` | Production lifecycle + resize correction: 27/28 pass. New same-inline HTTP test samples IsLoaded immediately after its own Ready status triggers layout; the subsequent test revision flushes the dispatcher before that assertion. Failure retained. |
| `final-verified` | **28/28 structured checks pass, exit 0**, including cleanup, two new same-inline tests and source-event probes. All 27 PNGs were opened individually; visual caveats below. |
| `screen-verified` | Supplemental direct screen capture fails with `foreground_not_target`; cleanup passes. No screenshot captured. |
| `screen-focused` | Followed tool advice with explicit focus on owned Next52. It again refuses with `foreground_not_target` and requests manual activation. Stopped this capture path; cleanup passes. No policy changes or other-window input. |

## Reproduction and verification

Use a fresh destination/layout/evidence path each time, with exclusive authorized local desktop access:

```powershell
./docs/performance-acceptance/harness-46-47/prepare-52.ps1 -SourceRoot $PWD -Destination $project
dotnet build "$project/Harness.csproj" -c Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
$launch = winapp run "$project/Harness.csproj" --no-build --arch x64 --output-appx-directory $layout --detach --json | ConvertFrom-Json
./docs/performance-acceptance/harness-46-47/run-52.ps1 -AppPid $launch.ProcessId -Layout $layout -Evidence $evidence -Slots -Boundary
```

The runner uses local owned PID/path guards, terminal status checks, per-case screenshots/UIA, owned Close, exact temporary package removal and UI yield. It does not launch the full application, use authenticated services, or touch PID 9608.

Final Release command:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=final-release.trx' --results-directory docs/performance-acceptance/evidence-52-fix/verify
```

**Passed: 401, Failed: 0, Skipped: 0**, exit 0. [TRX](evidence-52-fix/verify/final-release.trx) was parsed as XML and contains 401 UnitTestResult records. These headless tests do not execute MyImage; the desktop harness supplies that coverage.

Final Debug x64 build, using this checkout's absolute `src/` as SolutionDir, exits 0 with **0 warnings, 0 errors**: [log](evidence-52-fix/verify/final-debug-build.log). The first build's missing runtime asset failure remains in `debug-build.log`; only existing ignored x64 libmpv/ffmpeg assets were copied from the authorized main checkout into this checkout. No asset download or identity deployment was used. Harness builds retain four existing nullable-context warnings from linked loader code.

| Requirement | Evidence |
| --- | --- |
| Default HTTP bitmap display/aspect/physical decode request | `Landscape`, `Portrait`, `Square`; real loopback HTTP → shared loader → MyImage. |
| Resize, no self-triggered reload | `Resize`, `ResizeQuiescent`: 480→300 DIP, 450×225 physical request at 1.5 scale, three forced layouts plus bounded 400 ms quiescent interval. |
| Initial synchronous provider and cached source preservation | `CachedProvider`; original `InitialTemplateLoadedReady`, `SameInstanceUnchangedTextAttachReady`, `RepeatedAttach1Ready`/`2Ready`/`3Ready`, `LifecycleBoundariesReady`. |
| Actual same-inline host unload/remount | `InlineHttpReattach`: released child while unloaded, same Border reattached, exactly one additional real HTTP request. |
| Old provider result rejected across same-host remount | `InlineLateProvider`: same host, exactly two calls, released old Image cannot replace current Image/Source. |
| Document unload/clear/reopen and late provider | `LateProviderUnload`, `LateProviderClear`, `LateProviderReattach`, `LateProviderReopen`; original `EmptyTextReattach`. |
| Real slow HTTP body invalidation | `SlowBodyUnload`, `SlowBodyClear`, `SlowBodyReattach`, `SlowBodyReopen`; body held after 64 bytes, current landscape displays after transition. Headless `CancelledBodyCannotReachDecoderEvenWhenStreamIgnoresCancellation` independently asserts late decoder rejection. |
| SVG charset and actual renderer | `SvgCharset`, `ActualRendererInput`, `Boundary-svg`; `PreloadedSourceEvents-svg` distinguishes source readiness from ImageOpened. Visible glyph limitation above. |
| Shared four slots / recovery | `ConcurrentHTTP`, `Slots52`, `SlotsHeldAfterUnload`, `SlotsRecoveredAfterRelease`; controlled old SVG results never enter the current tree after release. Not a production leak claim. |
| Unchanged response/decode constraints | Release `OversizedDeclaredResponseIsRejectedBeforeConsumption`, `FourDecodesHoldSlotsAndCancelledWaiterNeverSendsRequest`, `DecodeFitsDisplayDpiAndPixelBudget`, `SvgTextDecodesCharsetOrBom`, `CancellationReachesHttpAndDecoderAndReleasesTheirSlots`. |

WinUI review: production diff is lifecycle wiring/state reconciliation only; no binding, XAML, theme, accessibility, localization, kernel, loader limit or auth changes. Async work remains awaited on UI context; real unload unsubscribes root/parent events and rejects old operations. No new strong global references or blocking UI waits. Existing project analyzers ran during Debug build. Dedicated WinUIAnalyzer is absent and its extra checks did not run; no dependency upgrade was introduced for this scoped fix.

Remaining acceptance limits: only local 150% DPI, synthetic BMP fixtures, and bounded runs. No 100%/200% monitor migration, native allocation/decoded peak, sustained throughput/leak/soak, authenticated cookie-isolation, full-app playback, theme/accessibility matrix, Release app or Native AOT runtime claim. The SVG text-glyph gap remains explicit. Historical #46/#47 reports are not rewritten as new passes.

## Visual review and cleanup

[Per-image review and hashes](evidence-52-fix/visual-review.json) records all 27 final default-window captures. Landscape, portrait, square, resize, cached provider, same-inline HTTP remount, same-inline late provider, slot recovery and renderer boundary have visible output. Tall portrait/square and the eight-image stack extend below the scroll viewport as expected. The narrow legacy table cell still wraps.

`RepeatedAttach1Ready` and `RepeatedAttach2Ready` captures are incomplete; `LateProviderClear`, `LateProviderReattach`, `SlowBodyClear`, and `SlowBodyReopen` captures omit their image despite successful structured Source/layout assertions. These PNGs are **not** full visual passes. A capture/composition interaction is suspected, not proven. The supplemental screen-DC capture and explicit owned-window focus were refused by WinApp with `foreground_not_target` (manual activation required). Those failures remain separate; they do not change the 28/28 structured result, and the structured result does not erase these visual gaps.

[Cleanup](evidence-52-fix/cleanup.json) independently verifies all nine owned process IDs absent and only the owned temporary package absent; each runner invoked UI yield. The successful final-verified run returned `released:true`; subsequent failed screen-capture runs and the redundant final yield returned `released:false` (no further release reported), preserved verbatim. Red/green-a's original five-second cleanup failures are not rewritten. No PID 9608, Dev/production identity, authentication, parent checkout, kernel source, system policy, push, PR or issue mutation was performed. **Desktop ownership is released.**
