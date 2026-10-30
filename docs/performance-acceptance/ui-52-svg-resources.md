# #52 SVG, paired resources and actual monitor acceptance

2026-10-30. Tested production base **9608cc8f6ac2ff04324dae402161b0ed8f20e6ce**, independent worktree `t3-741c2a32`. Prior acceptance-only commit `a69635d08fde3597c26c37fefc693502e9d028d4` was cherry-picked as `bc42983`; its report, original screenshots, failures and limitations remain unchanged. This run implements the previously missing experiments, not another execution of the old 23 checks. **Do not treat this as full #52 closure or native-memory acceptance.**

## Results

| Requirement | New evidence and result |
| --- | --- |
| Production MyImage vs direct DefaultSVGRenderer vs direct WinUI SvgImageSource | `Svg()` uses identical semantic input and 240×100 size, separately for a nonrectangular star path and a text-only `café`. All return SvgImageSource; direct WinUI SetSourceAsync returns Success. Star visible in all three; text invisible in all three. This isolates the visible-text limitation to the platform, not the charset fix. |
| Same-input repeated resource control | `MeasureResources()` completes six matched pairs, alternating limited/full then full/limited, following one warmup per mode. **84 scalar samples**, seven boundaries per load. Requests are 240×480 versus 1200×2400; source metadata remains 1200×2400; display remains 240×480 DIP at 96 DPI. Process/managed proxies only; no native decoder attribution or optimization-benefit pass. |
| Actual monitors and full portrait | Two actual 2560×1440 monitors. `MoveMonitor()` moves only the owned window left and back right, confirms MonitorFromWindow matches each target, and keeps the complete portrait visible. Both actual window DPIs are 96, XamlRoot scale 1.0. **Cross-monitor movement passes; different-DPI re-decode and 200% remain environment-unavailable under unchanged settings.** |

Final runner exit **0**, all five experiment checkpoints Ready; [results](evidence-52-resources/results.json). This is an experimental harness result, not a full app/unit-suite count. Final Debug x64 build: four inherited CS8632 warnings, zero errors; [build](evidence-52-resources/build.log). No 403-test suite, Sandbox feature enablement, Release/AOT or missing-MSVC retry.

## SVG root cause and exact inputs

[Original three-route screenshot](evidence-52-resources/svg-comparison.png) was viewed at full content. Each star has 5,699 nonwhite interior pixels; all three 240×100 interior crops are byte-for-byte identical. Each text-only crop has **zero nonwhite pixels** and the same RGB hash. The visible white backgrounds and gray borders are fixture containers, not SVG rectangles or a replacement for text. [Raw pixel comparison](evidence-52-resources/pixel-review.json) records original file hashes, exact crop bounds and RGB hashes. No original pixels were edited.

The MyImage path is the unchanged production Markdown control → MyImage → shared HTTP loader → ReadSvgAsync → a recording ISVGRenderer decorator → the **unchanged DefaultSVGRenderer**. The decorator saves the exact string and delegates immediately; it does not replace rendering. The server returns ISO-8859-1 bytes with `image/svg+xml; charset=iso-8859-1`. Direct DefaultSVGRenderer receives the same string without loader/HTTP. Direct SvgImageSource receives the same text encoded as UTF-8, without loader or DefaultSVGRenderer.

Files `path.http-latin1.bin` / `text.http-latin1.bin` preserve raw response bodies; `*-MyImage.txt`, `*-DefaultSVGRenderer.txt`, `*-SvgImageSource.txt` preserve route inputs. Their corresponding `.utf8.bin` files are identical across routes. For direct WinUI these are the actual submitted bytes. For DefaultSVGRenderer/MyImage they are the equivalent UTF-8 encoding of the recorded string, corroborated by the renderer's default StreamWriter implementation; there is no intrusive byte interception inside production. Latin-1 text has byte E9; each route receives Unicode `café` and the UTF-8 encoding C3 A9. `analyze.py` asserts these equalities. The original small SVG body and its route-specific representations are committed, not just hashes. [SVG input hashes](evidence-52-resources/svg-input-hashes.json) identify those tested files; the startup `fixtures.json` SVG entry is the unused inherited server fixture, not either new SVG input.

Source inspection: [DefaultSVGRenderer.cs](../../src/Desktop/BiliCopilot.UI/Controls/Markdown/DefaultSVGRenderer.cs) writes the string through StreamWriter, rewinds, awaits SvgImageSource.SetSourceAsync, and assigns that source. It has no text conversion or independent SVG engine. [MyImage.cs](../../src/Desktop/BiliCopilot.UI/Controls/Markdown/TextElements/MyImage.cs) uses the Content-Type charset through ReadSvgAsync and forwards the decoded string. [Source hashes](evidence-52-resources/source-hashes.json) cover all linked Markdown sources.

Microsoft's [Windows App SDK 1.8 SvgImageSource documentation](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.svgimagesource?view=windows-app-sdk-1.8) identifies Direct2D as its SVG implementation. The official [Direct2D SVG support list](https://learn.microsoft.com/en-us/windows/win32/direct2d/svg-support) includes path but omits text; unsupported elements are ignored. Both documents were checked on this run. Together with identical direct-WinUI failure and working path, this supports a **platform `<text>` capability limitation**. Correct charset decoding passes; visible café glyphs do not. No app-only SVG regression was demonstrated, no replacement rendering library or production fix was added. This does not claim support for all other SVG features.

## Resource experiment and limits

Both modes consume the identical existing portrait BMP, **8,640,054 bytes**, SHA256 `C8E7C4A9CEED39B2A252995DCCF471115BEC48A8BFE65C784FAFE940A2770047`. The fixture is reproducible via the existing Http52.Bmp generator; the large BMP is kept in scratch, not committed again. [Fixture manifest](evidence-52-resources/fixtures.json).

Both use a new production MarkdownTextBlock/MyImage inline in the same StackPanel/ScrollViewer, same 240 DIP width, same loopback server, same shared loader and metadata BitmapDecoder, and fresh BitmapImage per load. Limited mode is the real default production path. Full mode uses only the existing IImageProvider fixture seam to run the same loader and decoder, requesting original physical dimensions and the identical display dimensions. This control bypasses MyImage's limiting calculation via the provider seam; it is not a second production implementation or a claim that all provider lifecycle internals are identical. The Http52 fixture holds its synthetic byte arrays in both modes. No GC.Collect or retained Image/source dictionaries are used. Records contain only scalar values and copied snapshots; the old Diagnostics.cs/Image52.cs are excluded from the new host.

Readiness requires awaited source publication, positive current source/layout, then two CompositionTarget.Rendering events and a renewed check of the current Source. Every loaded sample also checks the current ready bitmap. Each pair records: before load; loaded immediately, +250 ms, +1000 ms; cleared immediately, +250 ms, +1000 ms. Clear removes the document, flushes dispatcher/layout and presentation before starting the cleared clock. Timers define observation windows only, never functional readiness. Actual +250 boundaries are 256–271 ms loaded / 256–271 ms cleared; +1000 boundaries are 1003–1016 ms loaded / 1001–1014 ms cleared. UTC, actual elapsed time, private bytes, working set, GC.GetTotalMemory(false), cumulative managed allocation, GC collection counts, actual DPI, Source type, source dimensions and decode requests are saved at each boundary. Before/cleared snapshots contain zero images.

[Raw 84 samples](evidence-52-resources/records.json), [paired loaded/cleared deltas](evidence-52-resources/paired-deltas.csv), [machine summary](evidence-52-resources/resource-summary.json), [standalone comparison chart](evidence-52-resources/resource-chart.html). The chart uses loaded +1000 ms minus its own before-load boundary, not unmatched terminal samples.

| Median within-load change at +1000 ms, MiB | Limited | Full-size control |
| --- | ---: | ---: |
| Process private bytes | +20.607 | −17.344 |
| Process working set | +20.139 | −5.207 |
| Managed heap estimate | +32.107 | −24.199 |

These medians **do not show that full-size decoding is cheaper**, nor establish a benefit for limiting. Natural gen-2 collections occurred in some loads, and working-set/allocator reuse, HTTP buffers, JIT, XAML/composition and managed fixture activity contribute. Pair-specific raw deltas and GC counts show the confounding directly. A request of 240×480 versus 1200×2400 proves the requested dimensions, not backing allocation size. Cleanup not immediately lowering process memory is not a leak finding. Six pairs are not a sustained soak or peak-allocation measurement.

[Tool inventory](evidence-52-resources/inventory.json): winapp 0.7.1, dotnet and WPR available; no WPA/xperf/PerfView/VMMap/cdb/WinDbg/dotnet-counters/dotnet-trace on PATH, no WPT/Debuggers directory in the standard inspected Windows Kits location, no global dotnet tools. WPR lists Heap and VirtualAllocation profiles, so **WPR itself is not claimed incapable of recording native allocations**. No native allocation stacks were captured/analyzed here; the earlier harness documents WPR policy-enablement failure, and there was no installation, policy change, broad system recording or production-process profiling. Process counters cannot attribute bytes to WinUI/WIC decoder objects. Native attribution remains unverified; the completed proxy experiment is retained rather than marked a native pass.

## Screens, presentation and execution history

[Inventory](evidence-52-resources/inventory.json) records DISPLAY1 at x=−2560…0 and DISPLAY2 at x=0…2560. Both 96 DPI readings were corroborated inside the per-monitor-aware owned process with GetDpiForWindow and XamlRoot, and independently in the UIA captures. Actual monitor handles change `65655 → 131073 → 65655`; image HTTP request count stays 3 and decode request stays 240×480. That is expected for equal-DPI movement, not evidence for a 100%→150%/200% DPI transition. No display setting was changed.

[Portrait complete](evidence-52-resources/portrait-complete.png), [left screen](evidence-52-resources/dpi-left.png), [right screen](evidence-52-resources/dpi-right.png) show all 24 checkerboard rows and both ends of the 1:2 portrait. The new host narrows its layout to 240 DIP so the complete 480 DIP tall image fits; production image behavior is unchanged. Their 240×480 image-area RGB bytes are identical. [Resources complete](evidence-52-resources/resources-complete.png) shows the empty post-cleanup host. All five final originals were individually opened and reviewed; four attempt-b originals were also viewed. No disagreement between structured final evidence and visible final pixels was found.

Failure history is preserved:

- `attempt-a`: first SVG ready timeout, no recorded renderer input or final Source. Cleanup confirmed owned PID 52172 and package absent, yield returned true. No confirmed crash cause; do not relabel it as success.
- `launch-before-build`: orchestration mistakenly requested `--no-build` before build had completed; winapp correctly refused missing AppxManifest.xml. No app activated in this attempt. The original cleanup record's PID 0 is a sentinel, not an owned app, and its processAbsent=false is the old runner querying that sentinel. Subsequent launch waited for successful build; final runner guards reject missing PID and existing layout/evidence/identity before reuse.
- `attempt-b`: SVG, full portrait and monitor movement completed. Resource pairs 1–5 and the full half of pair 6 were sampled; the limited half of pair 6 hit a NullReferenceException when the test read Source after its earlier readiness/frame wait. The generated host snapshot and failed records are retained. The final host uses asynchronous TCS continuations and checks current Source again after presentation, and validates each loaded boundary. No production fix or claim of a resolved production lifecycle defect follows from this harness correction.
- Final fresh `741-c`: five checkpoints complete, six resource pairs, no failure or readiness-recheck records, exit 0. This final bounded pass does not erase prior transient behavior.

## Reproduction, provenance and cleanup

Use new scratch/project/layout/evidence paths and an unused test identity. Run sequentially and check build exit before launch:

```powershell
./docs/performance-acceptance/harness-52-resources/prepare.ps1 -SourceRoot $PWD -Destination $project
dotnet build "$project/Harness.csproj" -c Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed; do not launch' }
./docs/performance-acceptance/harness-52-resources/run.ps1 -Project "$project/Harness.csproj" -Layout $layout -Evidence $evidence
if ($LASTEXITCODE -ne 0) { throw 'Experimental run failed; retain evidence' }
python ./docs/performance-acceptance/harness-52-resources/analyze.py $evidence
./docs/performance-acceptance/harness-52-resources/review-pixels.ps1 -Evidence $evidence
```

`review-pixels.ps1` deliberately guards the observed original capture size; inspect and update crop coordinates for another size/DPI, rather than blindly reusing them. It uses existing System.Drawing, and analysis uses Python standard library; Pillow was absent and was not installed. The first analysis attempt stopped on that missing module before results; the final scripts have no Pillow dependency. After the completed UI run, runner preflight-only safeguards were strengthened and syntax checked; an existing-layout invocation was verified to reject before any UI launch ([guard verification](evidence-52-resources/runner-preflight.json)). The compiled experiment and measurement code were unchanged after the final run.

Final source-linked, fresh project/output/layout **Debug JIT x64**, owned PID **38584**, identity `BiliCopilot.Acceptance52Resources741`. Harness.dll SHA256 **92BF33AAFC9B164075B2CA916EBE7B3B8D6B0D2FA775EA7CE8599FD023D49FD7**, matching the loaded module and deployed file. [Runtime modules](evidence-52-resources/runtime-modules.json) additionally records actual WinUI, Direct2D, WindowsCodecs and CoreCLR versions/paths/hashes. [Harness hashes](evidence-52-resources/harness-hashes.json) and [source hashes](evidence-52-resources/source-hashes.json) identify the compile inputs; [API query](evidence-52-resources/api-svg.json) is scoped to restored 1.8 references and retains its machine-SDK fallback caveat.

Applied winui-ui-testing, winui-code-review, winui-dev-workflow, systematic-debugging, verification-before-completion, code-testing and run-tests; read winui-design before fixture layout work. Review: no production XAML/C#/dependency/kernel change; fixture buttons have AutomationIds; temporary event subscriptions detach in finally; native/managed UI references are not saved in metric records; source rendering stays awaited on the UI context. Deliberate white SVG backgrounds and synthetic text are test oracle content, not product styling/localization. Dedicated WinUIAnalyzer is absent and was not installed; its checks did not run.

Kernel remains `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. Local parent `feat/performance-45` was synchronized before commit at tested `9608cc8` (already up to date). `src/`, dependencies and kernel have no diff from the tested base. No production/Dev package, PID 9608, previous PID 4780, credentials, login, installation, child agent, push, PR or issue operation.

Owned attempts 52172, 46400 and 38584 are absent; exact temporary package absent. [Final cleanup](evidence-52-resources/cleanup.json) records the final owned process and package removal. Final runner's actual UI yield response is **released:false**, preserved verbatim in [yield.json](evidence-52-resources/yield.json), not reported as true. No owned app remains and the desktop slot is relinquished. Release/Native AOT, different-DPI transitions, native allocation attribution, long-run leak/throughput and full production-app behavior remain unverified.
