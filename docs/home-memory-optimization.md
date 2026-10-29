# Recommendation home memory experiments

This follow-up starts at `5c1b52bb365c8888018969c2380623c9c7409bb5`. It retains deferred font enumeration and display-sized WIC decoding with Win2D output for the recommendation cover only. It does not change AppSDK, Win2D, .NET, other cover templates, or danmaku rendering.

## Protocol and limitations

Evidence directory on the test machine: `C:/Users/hp/AppData/Local/Temp/BiliCopilot-tray-spec/home-memory-optimization`. `plan.md` records the initial matrix; `sample.ps1`, `measure.ps1`, CSV/JSON samples, screenshots, build logs, TRX and withdrawn implementations are retained there. Screenshots containing personalized recommendations remain local.

All valid runs use Release x64 **JIT**, .NET runtime 9.0.22, the same isolated PFN `Mang.BiliCopilot.TrayAcceptance.T3_y1mf40g4r9srg`, and PopularPage → Recommend. The window is maximized on a 3840-pixel-wide screen, captured client 3840×2088 at 150% DPI. Each run starts a new process with warm OS/disk caches, waits approximately 84 seconds, then samples for 120 seconds at 2-second intervals. Process path, HWND, DPI, visibility, minimized state and debugger state are checked. No forced collection, working-set trimming, content hiding or window reduction is used. No build/test runs overlap sampling.

PSAPI extended counters report private working set, private commit and total working set separately. Memory figures are **MiB (1,048,576 bytes)** and medians; CPU is process CPU seconds divided by elapsed seconds, expressed as a percentage of one core. It is not whole-machine CPU utilization. The historical 192.31 MiB private-WS / 240.04 MiB commit run had a different process-age window; its post-sampling ~11 MiB managed heap diagnostic is not a comparable baseline.

Online recommendation content could not be frozen. Different images, network/cache state, shared-machine activity and startup work limit attribution. The repeated original baseline itself varies by 11.16 MiB. These are practical exploratory runs, not a statistical proof or a guarantee of 150–180 MiB on other content/machines. Opening a font picker intentionally pays the deferred enumeration cost.

Foreground state is also recorded separately: visible/not-minimized does **not** mean foreground or prove absence of occlusion. Font-only, cache, native and the second/third combination runs were partially or entirely background-visible. Their differences are exploratory, especially CPU; they are not tightly matched foreground A/B results. Process path is enforced by the sampler; `stateValid` in the raw summary checks window dimensions/DPI/visibility/debugger conditions, not foreground equivalence. Use `foregroundSamples`/`allForeground` as well. A third run's focus screenshot did not keep the app foreground. A fourth attempt used verified `ui focus`, which failed with `foreground_not_target` before sampling; no valid fourth sample exists. ShellExperienceHost owned foreground during a diagnostic check. No other workflow was terminated or forcibly interrupted to secure the desktop.

`cache-01` failed path normalization before valid sampling. `baseline-02` was rejected because registering the old manifest did not switch the active package location; no native-image samples are mislabeled as baseline. The successful original repeat used `winapp run folder`, with the original DLL hash checked. Original artifacts were not overwritten.

## Implementation and decisions

Observed medians (all memory columns MiB; FG = foreground samples / total):

| Variant | Private WS | Private commit | Total WS | CPU, one core % | FG |
|---|---:|---:|---:|---:|---:|
| Original baseline 01 | 200.39 | 243.50 | 378.38 | 5.03 | 60/60 |
| Original baseline 03, repeat | 189.23 | 239.13 | 365.20 | 14.00 | 55/61 |
| Home cache 2 only | 195.71 | 239.84 | 373.05 | 11.97 | 30/61 |
| Font deferral only | 175.97 | 229.34 | 350.25 | 11.09 | 0/61 |
| Display-sized WIC + Win2D only | 173.96 | 225.88 | 355.56 | 7.75 | 60/60 |
| Native Image alternative only | 198.64 | 239.90 | 378.23 | 8.25 | 40/61 |
| Combined 01 | 177.25 | 226.85 | 351.42 | 10.77 | 61/61 |
| Combined 02, background-visible | 174.47 | 226.91 | 351.36 | 8.99 | 0/61 |
| Combined 03, background-visible, later age | 177.94 | 229.05 | 355.08 | 9.18 | 0/61 |

Combined 03's capture/focus wait moved sampling to process age 107.77–229.31 seconds, so it is auxiliary evidence rather than an age-matched foreground replication. These experiments do not demonstrate a CPU improvement. The repeated original baseline also had substantially different CPU, so the CPU differences cannot be cleanly assigned to implementation.

1. **Repeater cache: withdrawn.** The experiment overrides the home repeater's named inner ItemsRepeater to `VerticalCacheLength=2` after loading; shared package default is 20. No NuGet-cache edits or other-page changes. Its small apparent difference is within baseline variation, and reaching a package template's private name adds fragility. See local `cache.patch`.
2. **Display-sized decode: retained.** `RecommendationCoverImage` reads image dimensions, computes sufficient physical pixels for UniformToFill at current RasterizationScale, requests scaled WIC pixel data, then creates/disposes a CanvasBitmap and draws into CanvasImageSource. It does not load a full CanvasBitmap and shrink it afterward. Dimensions round up, never enlarge a small source, preserve EXIF orientation and allow portrait cropping. The package's decode hook is private and its drawing override runs after full decoding, so this experiment necessarily includes a replacement transport/control path; its difference cannot be attributed solely to one decoder operation.
3. **Font enumeration: retained.** Startup and player initialization no longer enumerate all fonts. The first actual danmaku/subtitle picker requests a shared asynchronous catalog. Concurrent requests share a task; failure/cancellation permits retry. View-model collections recheck after awaiting. Saved family names apply during playback without enumeration and remain selectable even if enumeration does not return that family. Picker handlers await completion, check loaded state and report failure without an unhandled async-void exception.
4. **Native Image: withdrawn.** A separate variant uses the same new transport path with `BitmapImage`, physical DecodePixelWidth and UniformToFill. It replaces, rather than supplements, the Win2D output. Its measured static memory was worse than the WIC/Win2D variant and within the original range. `RecommendationCoverImage.native.cs` preserves the experiment locally. Image-variant differences are not added together as independent savings.

The retained cover shares the existing cache naming/expiry/clear-cache contract via a nonvisual BasicCoverImage helper. Downloads are deduplicated and limited to eight concurrent requests; recycling cancels the consumer rather than another consumer's shared download. Source changes clear stale images; resize/DPI reloads preserve the current image until replacement. Unload cancels work, releases the image reference and unsubscribes root/surface/device events. Bitmaps, drawing sessions and streams are scoped. Shared CanvasDevice is never disposed. Device-loss callbacks marshal to the UI dispatcher and ignore recycled controls, with a short retry throttle. This follows [Win2D manual device recovery](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/handling-device-lost) and the [shared-device ownership contract](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_CanvasDevice_GetSharedDevice.htm). It does not establish that the original static scale effect leaked.

## Build, tests and skill review

The final strict UI build uses `dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:SolutionDir=<worktree>/src/`: **0 warnings, 0 errors**. Explicit SolutionDir is required by this repository's source generation. Temporary isolated package identity edits are restored byte-for-byte by `finally`; no identity edits are committed.

The Release xUnit/VSTest suite passes **317/317, zero failures/skips**, with `final-tests.trx` and log. Added tests cover shared pending/completed font loads, failed/cancelled retry, empty catalog; 150%/200% decode dimensions, portrait crop, no upsampling, fractional rounding and invalid input. These verify math/concurrency contracts, not GPU rendering or native UI.

WinUI design/workflow/testing/review skills were applied. Installed API and gallery lookups checked ItemsRepeater cache, Image stretch, BitmapImage decode options, BitmapDecoder oriented dimensions and CanvasDevice recovery. Native controls, existing card styles, Uri binding and accessibility names remain in place; recommendation images add no keyboard stop. Async handlers and resource/event ownership were reviewed. No .NET/WinUI/Win2D upgrade or broad MVVM migration was made.

A separate diagnostic build injected the already-cached WinUIAnalyzer 0.7.1 through an external targets file (no project package edit). It reports 432 unique existing WUI diagnostics and one existing protobuf warning, **zero WUI diagnostics in changed files**, zero errors. This is not a clean bill for the entire repository. The final strict build runs without that diagnostic-only warnings-as-errors override.

## Native UI checks and remaining gaps

Final combination layout: `home-memory-optimization/combined-layout`, same isolated identity and retained login/data. No original Mang.BiliCopilot.Dev package was uninstalled or cleared, no credentials copied, no production Visor manipulated. Player font settings were temporarily changed for validation, then restored to **Segoe UI** for both selectors; tray residency was restored to **on**, notifications remained **off**. The final running process is warm from navigation, **not** a cold memory sample.

| Check | Result and concrete evidence |
|---|---|
| Recommendation covers, 150% DPI, maximized | PASS: combination start/end screenshots contain loaded ordinary covers with rounded top corners and fill crop; `combined-01-start.png`, `combined-02-start.png`. |
| Scroll/recycle and return to top | PASS through native UIA ScrollPattern: `final-scroll-down-result.json`, `final-scroll-top-result.json`, `final-scroll-down.png`, `final-scroll-top-tree.json`. New rows have covers, original top returned. This is not a frame-time benchmark. |
| First danmaku/subtitle font picker use | PASS through real native combo boxes: both populated and expanded; `final-danmaku-expanded.png`, `final-subtitle-expanded.png`. Shared task behavior additionally has unit tests. |
| Saved font after process restart | PASS: selected **Segoe UI Variable Text** in both pickers, normally exited PID 44764, launched PID 64680, opened a player and both pickers. `final-subtitle-persisted.json`, `final-danmaku-persisted.json` and `final-persisted-fonts.png` show the saved family. |
| Restore original font preferences | PASS: both read back **Segoe UI**; `final-restored-SubtitleFontComboBox.json`, `final-restored-DanmakuFontComboBox.json`, `final-fonts-restored.png`. |
| Tray hide and activation restore | PASS: close hid HWND 34808284 while PID 64680 remained alive; AUMID activation restored the same HWND/PID, with one matching process. `final-tray-restore.json`, `final-tray-restored-home.png`. Settings screenshot/tree confirm tray on and notifications off. |
| Mouse hover / second strictly foreground combination repeat | BLOCKED by native tool `foreground_not_target`; requesting foreground did not succeed. UIA invocation was a working alternative for functional checks, not proof that mouse input passed. A pending request for manual activation was superseded for font checks by successful UIA checks. |
| Actual 200% monitor/DPI transition, injected image/network failure, EXIF/portrait visual fixtures, GPU loss/surface recreation | NOT VERIFIED on the real device. Size/crop math at 150%/200% and code recovery paths are covered by tests/review, not claimed as native acceptance. |

The full-foreground combination observation is **177.25 MiB private WS**, **226.85 MiB private commit**, **351.42 MiB total WS**, **10.77% of one CPU core**. Relative to the full-foreground original observation this is 23.14 MiB less private WS; relative to the partially foreground original repeat it is 11.98 MiB less. Those comparisons still have uncontrolled online content. Auxiliary combination observations are 174.47 and 177.94 MiB. This supports retaining the candidate for this workload, but does not establish independent/additive font and decoder savings or guarantee the 150–180 MiB target. In particular, the all-background font-only observation needs a foreground/content-controlled repeat before attributing its entire difference to font enumeration.

Review notes: the recommendation-only image path adds a maintenance obligation for cancellation/cache/DPI/device lifecycle that the shared package previously owned; the explicit handlers and narrow template scope mitigate it, but injected failure/device-loss scenarios remain follow-up validation. No blocking build/analyzer issue was found in the changed code. The earlier tray acceptance gaps, including historical downloads and the full end-to-end matrix in `tray-residency-acceptance.md`, remain open. This report does **not** mark the entire project or original tray acceptance complete.

## Integration review follow-up

An independent WinUI code review of `cbeafc5` found that a second device loss within the three-second recovery throttle discarded the recovery request. The unchanged request key could then prevent an ordinary layout notification from retrying the cover. Integration replaces that early return with one coalesced, delayed recovery on the UI dispatcher, checks device identity/loaded state again after waiting, and cancels the wait on unload. This is a control-flow fix; consecutive GPU-loss fault injection has not been performed. The memory and native UI evidence above belongs to `cbeafc5`, before this exceptional-path correction; it is not a measurement of the final correction or a claim that hardware recovery passed.

Parent verification after the correction: Release tests **317 passed, 0 failed/skipped**; final Release x64 JIT build **0 warnings/errors** (`parent-build-final.log`). The first parent build compiled successfully but packaging correctly rejected missing ignored native assets; the same tested x64 libmpv/ffmpeg files were copied from the experiment worktree, then the unchanged build command passed. The earlier cold build also emitted an existing protobuf unused-import warning (`parent-build.log`). No native dependency/version change was committed. The installed isolated layout remains the measured `cbeafc5` build; the recovery correction has build/static verification only.
