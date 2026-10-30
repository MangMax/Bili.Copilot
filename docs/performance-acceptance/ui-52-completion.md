# #52 supplemental acceptance — partial completion

2026-10-30. Run `completion-24785975`, production SHA `9608cc8f6ac2ff04324dae402161b0ed8f20e6ce`, initially clean checkout. **Do not close #52.** Production, dependencies and kernel were not changed. No push, PR/issue operation, installation, credentials, forced GC, DPI change or AOT retry.

Fresh independent source-linked Debug x64 harness, PID **4780**, DLL SHA256 **BA3DFD697D9D366049F715AF5009541F2746F77CA4E85C764E2B9492BDDF28F9**. Fresh project and package layout prevented old incremental DLL reuse. SourceRoot points to this checkout; production Markdown sources compile directly into Harness.dll. [Provenance and source hashes](evidence-52-completion/provenance.json). Exact package identity is BiliCopilot.Acceptance52T384527; no Dev/production package or PID 9608 was accessed.

Applied winui-ui-testing, winui-code-review, winui-dev-workflow, verification-before-completion and systematic-debugging. No tests or XAML were edited. Dedicated WinUIAnalyzer was not run.

Reproduction uses unchanged committed harness scripts:

```powershell
./docs/performance-acceptance/harness-46-47/prepare-52.ps1 -SourceRoot $PWD -Destination C:/Users/hp/AppData/Local/Temp/bili-52-completion-24785975
dotnet build C:/Users/hp/AppData/Local/Temp/bili-52-completion-24785975/Harness.csproj -c Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
winapp run C:/Users/hp/AppData/Local/Temp/bili-52-completion-24785975/Harness.csproj --no-build --arch x64 --output-appx-directory C:/Users/hp/AppData/Local/Temp/bili-52-completion-24785975-layout --detach --json
./docs/performance-acceptance/harness-46-47/run-52.ps1 -AppPid 4780 -Layout C:\Users\hp\AppData\Local\Temp\bili-52-completion-24785975-layout -Evidence $PWD/docs/performance-acceptance/evidence-52-completion -CasesCount 14
```

Choose new paths and the actual returned PID on replay. Build succeeded with four existing CS8632 warnings, zero errors; [build log](evidence-52-completion/build.log). Runner exited 0: **23/23 structured checks including cleanup** ([results](evidence-52-completion/results.json)). This is not a full app suite result; inherited 403 tests were not rerun.

| Item | Result and evidence |
| --- | --- |
| RepeatedAttach1Ready / RepeatedAttach2Ready | PASS: both new originals show complete fixture, pink logo and FINAL-OMEGA. Original seven-block/loaded assertions retained; readiness requires ImageOpened and positive layout. |
| LateProviderClear / LateProviderReattach | PASS: both new originals show complete pink logo; structured checks confirm current Image/Source retained after old provider completion and dispatcher flushes. |
| SlowBodyClear / SlowBodyReopen | PASS: both originals show complete landscape checkerboard; held local HTTP body is released after current image readiness. |
| Resize / cache reuse | PASS at actual scale **1.0**: 480→300 DIP, requested physical decode 480×240→300×150; source dimensions remain 2400×1200. Cached provider identity/source preserved. Full targets visible. |
| SVG charset | PASS input only: ActualRendererInput contains café; real DefaultSVGRenderer returns SvgImageSource. |
| SVG café visible glyphs | FAIL: original SvgCharset.png shows teal rectangle without letters. This is preserved separately from the structured PASS. Renderer-capability root cause not conclusively isolated by this run. |
| Distinct SVG path control | **NOT TESTED**; rectangle is not substituted for the requested discriminating path experiment. |
| Native decode memory / matched resource comparison | **NOT VERIFIED**. Process samples retained but no native allocation attribution or matched repeated control experiment completed. |
| Cross-monitor / 200% DPI | **NOT TESTED**. Current scale recorded without changing display settings; monitor availability was not inventoried. |
| Release / Native AOT | **ENVIRONMENT BLOCKED**, inherited toolchain finding; this run is Debug JIT only. |

All 22 new PNG originals were individually viewed. The six requested targets are fully visible. Portrait extends below the viewport and is not a full-image visual pass; other new bitmap/provider captures visibly contain their target. EmptyTextReattach/Detach correctly show empty content. Historical red traces and original six visual caveats are unchanged. Historical LateProviderClear review disagreement remains in integration-52-lifecycle-fix.md; this new screenshot is a separate observation, not a correction of the historical pixels.

Readiness follows existing oracle: stream publication after awaited SetSourceAsync plus loaded positive layout; URI provider ImageOpened; SVG renderer return plus positive layout. No arbitrary sleep was added before capture. Resize's existing 400 ms interval measures bounded request quiescence only.

[Terminal samples](evidence-52-completion/terminal-summary.json) preserve process private bytes, working set, process-lifetime peak working set and managed bytes. These belong to the entire instrumented harness including fixture byte arrays, diagnostics retaining Image references, HTTP buffers, XAML and JIT. They cannot establish native decode allocation, leak rate or an optimization benefit. DecodePixelWidth/Height are requests, not measured backing allocation. No global GC was forced. The known noncooperative SVG four-slot boundary was not rerun or relabelled a leak.

Cleanup independently confirms PID 4780 absent and exact temporary package absent in provenance.json. Runner called yield; actual return is **released:false**, preserved in yield.json, not rewritten as true. No owned app remains; **desktop allocation is relinquished**. Local feat/performance-45 merge returned already up to date at the tested SHA. This delivery closes the six new visual observations but leaves the explicitly listed experiments incomplete.
