# Performance #45: integration of #52

Date: 2026-10-30. Requested integration and headless verification completed; **SVG charset compatibility finding remains open, and real WinUI acceptance is pending**. Do not close #52 based on this report.

## Provenance and preservation

- Operated only in `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`, initially clean at `3c526f5af07db90c0dbce6ee36d2093304f3236f`.
- Read AGENTS.md, docs/agents/{issue-tracker,triage-labels,domain}.md, full GitHub #52/#45 bodies and comments (both comment lists empty), incoming issue-52.md, and ui-46-47-diagnosis.md. Applied winui-code-review, winui-ui-testing, run-tests and verification-before-completion. No run-tests overlay exists; SDK 10.0.403, existing net9.0 xUnit 2/VSTest project.
- `t3/image-loader-performance-tests` resolves to the exact requested `05350824c67507db1aaae9c593c4a1c2d7b238d7`. Read its content through Git in this checkout, without accessing its worktree.
- Executed `git merge --no-ff 05350824c67507db1aaae9c593c4a1c2d7b238d7 -m 'Merge ticket #52 bounded Markdown images into feat/performance-45'`. Merge `47f72537fb0cf8fb160042ea07ef395639f8cdd1` has exactly the starting HEAD and requested commit as parents. No conflicts or production/test corrections.
- Delta from starting HEAD is exactly five incoming files: MarkdownImageLoader.cs, MyImage.cs, test project link, MarkdownImageLoaderTests.cs, and issue-52.md. This integration report is the only subsequent tracked addition.
- Verified ancestry for #46 `7d598c1`, #50 `6ed6d46`, #47 `175b2c0`, #48 `2eac5fa`, #51 `ed84c6a`, #49 `ba1e76a`, reattachment fix `d4592d3`, starting diagnostic HEAD `3c526f5`, and incoming #52 `0535082`. Existing integration and harness/evidence files are unchanged; starting HEAD remains an ancestor.
- Kernel gitlink and nested HEAD remain `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`, nested tree clean. Read-only `git ls-remote` confirms this exact published SHA on `fix/copilot-48-danmaku-cancellation`.

## Review finding: SVG HTTP charset is discarded (P2 / Warning)

`MarkdownImageLoader.cs:90` passes only `ContentType.MediaType` to the consumer, discarding `CharSet`. `MyImage.cs:207` then uses default UTF-8 `StreamReader`. Previously `HttpContent.ReadAsStringAsync()` honored the HTTP charset. Thus a valid SVG response with `Content-Type: image/svg+xml; charset=iso-8859-1` and non-ASCII text changes before it reaches the SVG renderer.

Controlled local .NET boundary experiment used ISO-8859-1 bytes for `<svg xmlns="http://www.w3.org/2000/svg"><text>café</text></svg>`. Old HttpContent decoding exactly matched the fixture; the new StreamReader path produced `<text>caf�</text>`. This establishes character corruption at the renderer input, not a claim of observed WinUI pixels. It does not depend on the historical harness readiness failures. Existing transport tests use ASCII `svg` and do not cover charset preservation.

Suggested focused correction for the owner: preserve response charset through the bounded loader's consumer boundary and select the matching text encoding while retaining cancellation, body limits and BOM handling; add a non-UTF-8 SVG boundary case. No fix or new test infrastructure was introduced in this merger task. Local experiment output: `tests/BiliCopilot.UI.Tests/TestResults/integration-52-svg-charset.json` (ignored evidence).

## Other reviewed behavior and limits

- Shared process-lifetime HttpClient/SocketsHttpHandler is dedicated to images, cookies disabled, credentials null, no account-client reuse or authorization headers. Four connections per server; pooled lifetime five minutes. Loopback test proves two requests on one connection without Cookie/Authorization replay; it does not measure real application reopening.
- Four shared default-load slots encompass response reading and consumer/decode completion. Linked 30-second deadline covers queuing and loading. Declared bodies above 32 MiB are rejected; actual reads stop at limit plus one byte. Response, response stream and seekable buffer are scoped with using/finally; failure/cancellation releases acquired slots. No second full-size byte array copy is used. MemoryStream growth, decoder scratch space, SVG complexity and animated image allocations are outside the advertised compressed-body/pixel limits.
- Cancellation propagates to HTTP, reading and WinRT operations. Provider interfaces have no cancellation argument; custom provider work can continue, but operation identity, cancellation and stable-host loaded state reject late publication. Noncooperative SVG renderers retain their slot until completion. Old finally blocks only clear their own operation. Unload detaches ancestor/root subscriptions, cancels pending work and releases the image reference without clearing a cached provider image's Source.
- Stable Border owns Loaded/Unloaded when Image instances are replaced. Provider precedence and explicit provided-image sizing remain; SVG routing is case-insensitive and missing MIME no longer dereferences null. The charset finding above prevents claiming full SVG compatibility.
- Bitmap sizing uses oriented dimensions, containing display bounds, nearest measured ancestor width and rasterization scale, then requests physical decode dimensions before SetSourceAsync. Limits are 4,194,304 pixels, 4096 per edge and source resolution. Resize/root changes cancel and reload default bitmaps; this can redownload during resizing and is not a cache or a measured churn improvement.
- No additional substantive issue established by this static pass. Actual layout ancestor behavior, EXIF handling, native decoding, cancellation ordering, cached-provider reuse and stable-host events remain UI acceptance questions. No new XAML, binding, theme or accessibility surface. Dedicated WinUIAnalyzer package absent; its diagnostics were not run. Existing compiler/analyzer settings were preserved.

## Fresh verification and test fidelity

Executed on merge `47f72537fb0cf8fb160042ea07ef395639f8cdd1`:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=integration-52.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -v:normal
```

- Full Release suite exit 0: **Passed: 362, Failed: 0, Skipped: 0**. Parsed TRX independently confirms total/executed/passed 362, 362 result records, 20 MarkdownImageLoaderTests all Passed; failed/error/timeout/aborted/notExecuted/notRunnable/pending are zero. Results: `tests/BiliCopilot.UI.Tests/TestResults/integration-52.trx`; parsed `integration-52-summary.json`; console `integration-52-tests.log` in the same ignored directory.
- Debug x64 build exit 0: **0 warnings, 0 errors**, elapsed 00:01:45.50; `integration-52-build.log` in the same ignored directory. Output: `src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll`. No restore, deployment or activation was needed.
- Explicit runtime asset gate exit 0, **0 warnings, 0 errors**, elapsed 00:00:01.07; `integration-52-runtime-gate.log`. This checks platform and existence of libmpv, ffmpeg and BBDown assets, not native DLL loading or application startup. Assets were reused in place, without downloads/copies/installation or guard bypass.
- The 20 image cases compile the actual production loader source. Controlled handlers/streams exercise declared and actual limits (limit minus one/exact/plus one), disposal, error paths, four occupied consumer slots, cancelled queued work, HTTP/body/consumer cancellation and noncooperative delayed body completion. Loopback transport uses the actual shared client. Seven sizing cases exercise arithmetic, not WinRT decoded output.
- Tests do **not** execute MyImage, visual lifecycle events, real SVG renderer, custom Image providers, XamlRoot DPI/ancestor resizing or native decode memory. The 30-second elapsed deadline is configured but not directly timed. This pass did not rerun earlier red/green mutations, so their reported sensitivity remains incoming evidence only.
- Whitespace checks passed. Only this document was added after verification; source, test, project, kernel and runtime assets remained unchanged. All existing tracked acceptance artifacts are preserved. Local logs/TRX/experiment output remain ignored and are not committed.

## Parent acceptance handoff

No desktop was assigned. No deployment, app launch, input, UI capture, native-memory sampling or Release/AOT app run occurred. Headless Release tests and Debug build/runtime asset gate are separate from real UI acceptance.

Readiness must follow ui-46-47-diagnosis.md: actual ImageOpened plus positive rendered dimensions and content/lifecycle predicates, bounded ImageFailed/timeout handling, and preserved immediate observations. The original short-lived harness failures are not proof of a production defect. Do not replace their records with later Ready results or weaken same-instance/exact-content/no-duplication assertions. Earlier diagnosis evidence predates this image loader merge and does not validate #52.

After separate desktop allocation, the parent should verify:

1. Large landscape/portrait/square and EXIF images, explicit width/height combinations, invalid/missing MIME and malformed payloads; real decoded dimensions and visible aspect ratio.
2. SVG (including non-UTF-8 response charset), custom providers and cached Image reuse, without replacement load/unload loops.
3. Container resize and 100%/150%/200% DPI; default bitmap refresh, actual ancestor constraints, request churn and bounds.
4. Stalled headers/body/SVG/provider work across unload, clear, same-instance reattach and reopen, then release old completions: no stale publication and no old finally clearing a new load; current content survives. Keep the d4592d3 reattachment assertions.
5. Same-build, same-fixture repeated large-image opening: requests/connections, actual concurrent work, managed allocation and native decode/process-memory peaks. Four slots and calculated pixel budgets are not whole-process memory measurements or demonstrated performance gains.

No delegates, other-workspace operations, push, PR modifications or issue closure. The requested merge is retained with the compatibility finding reported; overall #52 acceptance is not declared passed.
