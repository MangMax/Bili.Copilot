# Issue #52 — bounded Markdown image loading

Date: 2026-10-30. Implementation and controlled HTTP tests; **real WinUI image acceptance is pending**. Do not close #52 based on this report.

## Scope and checkout

- Issue https://github.com/MangMax/Bili.Copilot/issues/52 under #45. Read both full bodies/comments with `gh issue view <number> --repo MangMax/Bili.Copilot --json number,title,body,comments`; neither had comments. Claimed with `gh issue edit 52 --repo MangMax/Bili.Copilot --add-assignee '@me'`.
- Read AGENTS.md, docs/agents/{domain,issue-tracker,triage-labels}. Rechecked local `docs/performance-review-2026-10-29.md` F08 against MyImage and the open PR file lists; no already-implemented image fix.
- Branch `t3/image-loader-performance-tests`, worktree `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-7b898ee6` started at required integration tip `e6f425509a728a4086eefbe29e7273d2851a03f0`. Fetched origin, then merged latest local `feat/performance-45` into this branch: `ddc607451075470eccb334b4ec3ce7a4e898dd26` before final checks. Integration itself was not changed.
- Integration later advanced to `d4592d3579aacaf4dcef24ccd9396c1ecc33506f` (MarkdownTextBlock restores content on reattachment). Merged that tip before the final build/full-suite run. Reviewed its interaction with MyImage: clearing/rebuilding unloads the old stable host, invalidating its requests and detaching listeners; fresh images get fresh ownership.
- Kernel source exists and `git -C externals/bili-kernel status --short` is clean at expected gitlink `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. No nested-repository repair, reset, or kernel modification was needed.
- No delegates, pushes, PR changes, issue closure, installs, global settings, app deployment/launch/input, or modification to another worktree or its UI harness. #46/#47 retain desktop ownership.

## Implementation and bounds

- Default HTTP(S) path uses one process-lifetime, image-only HttpClient/SocketsHttpHandler; cookies disabled, no account client/credentials/auth headers. Connection lifetime is five minutes; maximum four connections per server. No image/disk cache is introduced, so reopening may download again while reusing connections.
- Four concurrent default loads **including consumer/decode lifetime**, shared across Markdown controls. Queued/download/decoder work receives a linked 30-second cancellation deadline. Unload or display changes cancel the old operation; operation identity and loaded state reject late provider/renderer/bitmap results. Existing provider interfaces cannot force cancellation of their work; late publication is rejected. A noncooperative custom SVG renderer can retain a slot until completion, rather than allowing unbounded concurrent renderers.
- ResponseHeadersRead; reject declared bodies above **32 MiB** before consumption. Count actual bytes for unknown/incorrect lengths, reading at most the limit plus one byte. One seekable MemoryStream supports WinRT; there is no full byte-array-to-WinRT copy. Response, source stream, temporary stream, and WinRT adapter are deterministically disposed on success/failure/cancellation. MemoryStream growth can create transient allocations; 32 MiB is a response/storage bound, not a measured whole-process memory bound.
- Read oriented source dimensions, fit within declared width/height and nearest measured ancestor width, and multiply by XamlRoot rasterization scale. Set physical decode dimensions **before** SetSourceAsync. Cap requests at **4,194,304 pixels**, **4096 per edge**, and source resolution. Default images do not upscale; explicit display sizes may upscale without allocating extra decoded pixels. Both explicit dimensions are a containing box, preserving aspect ratio (integer decode rounding may differ by a pixel). Missing layout falls back to root width, then 800 DIPs; invalid scale falls back to one.
- Stable Border owns Loaded/Unloaded even when SVG/custom providers replace Image. Ancestor SizeChanged and XamlRoot.Changed refresh default bitmap requests when width/DPI changes; subscriptions detach on unload. Old async finally blocks cannot clear a new operation. Unload releases the provider image reference without clearing a potentially cached provider-owned Source.
- SVG MIME routing (case-insensitive, nullable-safe) and provider precedence remain. SVG/custom-provider sizing keeps existing overrides. The bitmap pixel cap does not claim to bound SVG complexity, custom-provider allocations, codec scratch space, or animated-image native allocations. Real sampling remains required.

## Skills and automated seam

Applied local `tdd` (tests.md/mocking.md), `code-testing` (report-safe names/TRX validation), `run-tests`, `winui-dev-workflow`, `winui-design`, `winui-code-review`, `winui-ui-testing`, `verification-before-completion`, and `systematic-debugging` from `C:/Users/hp/.codex/skills`.

The explicitly authorized focused seam is the **production MarkdownImageLoader**, linked into the existing xUnit project, with controlled HttpMessageHandler/streams and an actual loopback TCP server for the default client's connection/cookie behavior. Decoder consumption is the OS boundary. No fake copy of loader logic, private-field/source-text assertions, new test package, or shared framework. Sizing tests use known expected dimensions. This headless project does **not** execute MyImage/WinRT, provider replacement, or visual lifecycle events. Those are static review plus pending UI acceptance, not automated passes. code-testing-extensions was not supplied; existing xUnit conventions were used. No run-tests overlay was present. SDK 10.0.403, xUnit 2.9.3/VSTest, net9.0; WinApp CLI 0.7.1.

## Commands and results

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~MarkdownImageLoaderTests --logger 'trx;LogFileName=issue-52-red-size.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~MarkdownImageLoaderTests --logger 'trx;LogFileName=issue-52-green-limits.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~MarkdownImageLoaderTests --logger 'trx;LogFileName=issue-52-red-decode.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~MarkdownImageLoaderTests --logger 'trx;LogFileName=issue-52-green-decode.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter FullyQualifiedName~MarkdownImageLoaderTests --logger 'trx;LogFileName=issue-52-focused.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --logger 'trx;LogFileName=issue-52-integrated.trx'
```

| Report | Passed | Failed | Skipped | Exit | Evidence |
|---|---:|---:|---:|---:|---|
| red-size | 0 | 3 | 0 | 1 | Baseline loader accepted oversized/404 responses and fifth concurrent load |
| green-limits | 3 | 0 | 0 | 0 | Bounds/status/slots implemented |
| red-decode | 3 | 7 | 0 | 1 | Native-size baseline violates display/DPI/budget assertions |
| green-decode | 10 | 0 | 0 | 0 | Display/DPI/budget sizing implemented |
| transport | 16 | 0 | 0 | 0 | Disposal, byte boundaries, late cancellation, loopback connection/cookies |
| focused | 20 | 0 | 0 | 0 | Final image-loader suite |
| integrated | 362 | 0 | 0 | 0 | Full suite after integration merge |
| final-focused | 20 | 0 | 0 | 0 | After app-analyzer fixes: async memory-stream write and redundant using removal |
| final-integrated | 362 | 0 | 0 | 0 | Final full suite on d4592d3 with analyzer fixes |

Parsed each ignored `tests/BiliCopilot.UI.Tests/TestResults/issue-52-*.trx` with PowerShell XML and checked counters. Initial REDs are executed assertion failures against the newly extracted baseline loader/sizing behavior, not compilation failures. No external image server, account, fixed sleeps, or desktop used. Loopback observed **two requests on one TCP connection**, both without Cookie/Authorization after receiving Set-Cookie. This is transport evidence, not a native decode-memory measurement.

```powershell
dotnet restore src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-7b898ee6/src/
winapp find-api refresh --project-dir src/Desktop/BiliCopilot.UI
winapp find-api check-property BitmapImage DecodePixelWidth DecodePixelHeight DecodePixelType --json --project-dir src/Desktop/BiliCopilot.UI
winapp find-api check-property BitmapDecoder OrientedPixelWidth OrientedPixelHeight --json --project-dir src/Desktop/BiliCopilot.UI
winapp find-api members XamlRoot --filter Changed --json --project-dir src/Desktop/BiliCopilot.UI
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-7b898ee6/src/ -p:GenerateAppxPackageOnBuild=false
```

Restore succeeded. App-scoped API lookup found the requested APIs, with CLI caveats: installed SDK 10.0.28000 versus target 10.0.22621; unrelated installed Runtime 2.6 excluded from referenced WindowsAppSDK 1.8.39 metadata. No WinUIAnalyzer package exists in this app; it was not installed under the explicit no-install instruction, so those diagnostics did not run. Existing compiler/analyzer configuration was preserved.

First build overlapped the integration merge: MSBuild evaluated its source list before new DanmakuRedrawSession.cs arrived, then compiled the newer panel. It failed CS0246 for that class (plus existing unused proto import warning). No unrelated code fix; restarted on the stable merged tree. Final build result recorded below.

The stable integrated build then identified VSTHRD103 on MemoryStream.Write and IDE0005 on a redundant Http using. Replaced the write with awaited WriteAsync and removed that using; no diagnostic suppression. Repeated the focused test command with `LogFileName=issue-52-final-focused.trx`: Passed: 20, Failed: 0, Skipped: 0, exit 0; parsed counters confirm all 20 executed/passed with zero errors/notExecuted. The full 362-test run precedes these two narrow fixes; the focused rerun covers them.

Next build compiled BiliCopilot.UI successfully with no warnings but failed the project's runtime asset guard for missing libmpv. Copied only missing ignored `Assets/libmpv/x64/libmpv-2.dll` and `Assets/ffmpeg/x64/ffmpeg.exe` from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets`, as explicitly authorized. No binaries staged, no installation, and the runtime asset guard remains enabled. Final build uses the same command above; log `C:/Users/hp/AppData/Local/Temp/issue-52-build-assets.log`. The earlier logs are `issue-52-build.log`, `issue-52-build-integrated.log`, and `issue-52-build-final.log` in that directory.

After the final integration merge and analyzer fixes, repeated the full test command with `LogFileName=issue-52-final-integrated.trx`: Passed: 362, Failed: 0, Skipped: 0, exit 0. Parsed XML confirms total/executed/passed 362 and failed/notExecuted zero.

Final x64 Debug build: **exit 0, 0 warnings, 0 errors**, 2m59.44s (`issue-52-build-assets.log`). Build/package generation only; no deployment/activation. Then merged integration `57dceffb1f20dc9db281755acb593c98265534f8`, containing only #46/#47 acceptance artifacts and their standalone harness under docs. `git diff --name-only d4592d3 HEAD -- src tests externals` is empty; app/test/kernel inputs are identical to those just verified, so no redundant rebuild. This does not infer desktop permission from another ticket's evidence.

Post-build WinUI code review found no remaining must-fix static issue in this slice. `git diff --check` passes. Final integration ancestor and clean kernel checks passed. The explicit UI/native-memory limitations below remain acceptance blockers, not code-review passes.

## Requirement evidence

| Requirement | Evidence |
|---|---|
| Dedicated reusable pool; no cookie/credential replay | `SharedClientReusesConnectionWithoutReplayingCookiesOrCredentials` |
| Deterministic success/error disposal; HTTP failures rejected | `UnknownLengthBodyEnforcesBoundaryAndDisposesBuffers`, `HttpFailureDoesNotReachDecoderAndReleasesResponse`, `DecoderFailureReleasesResponseBufferAndSlot` |
| Declared/actual oversized responses bounded | `OversizedDeclaredResponseIsRejectedBeforeConsumption`, `UnknownLengthBodyEnforcesBoundaryAndDisposesBuffers` (limit minus one/exact/plus one) |
| Four slots include decode; cancelled waiter does not request | `FourDecodesHoldSlotsAndCancelledWaiterNeverSendsRequest` |
| Cancellation reaches HTTP/body/decode; late body rejected; slots recover | `CancellationReachesHttpAndDecoderAndReleasesTheirSlots`, `CancelledBodyCannotReachDecoderEvenWhenStreamIgnoresCancellation` |
| Only HTTP(S) default transport | `UnsupportedUriNeverReachesHttp` |
| Display/DPI/aspect ratio/decode request budget | `DecodeFitsDisplayDpiAndPixelBudget` (7 cases); actual WinRT output remains pending |
| SVG/provider routing, control unload/reload, stale publication, resize/DPI event wiring | Static MyImage review; real UI pending |
| Repeated real large-image request/connection/decode peak sampling | Transport-only sample above; native decode peak and same-build UI sample pending |

## WinUI review and remaining acceptance

Manual review checks UI continuation affinity, cancellation-source ownership, stream ownership, stable lifecycle host, detached layout/root handlers, provider-owned images, nullable content type, HTTP status/size enforcement, aspect ratio, and scoped bounds. No XAML bindings, interactive controls, resource/theme strings, global decode switches, or application cache changes. Static findings fixed: image replacement must not lose unload ownership; unload must not mutate a cached provider Image; late finally must not clear a new request. There is no claim that manual review proves runtime correctness.

After parent grants desktop access, use the existing approved UI acceptance workflow without modifying another worktree's harness:

1. Same build and controlled local fixtures: large landscape/portrait/square images, EXIF rotation, missing MIME, malformed image, SVG and custom provider (including cached Image), explicit width/height combinations, and an image-heavy document.
2. Reopen repeatedly, resize container, and move between 100%/150%/200% DPI; inspect visible aspect ratio, bounds, and image quality. Check actual decoded dimensions and request counts; avoid labeling calculated dimensions as native measurements.
3. Stall headers/body/renderer, unload/clear/reopen, then release old results. Verify no stale image reappears, subscriptions stop, HTTP cancellation is observed, and the new image remains. Check provider and SVG replacement do not cause a load/unload loop.
4. Record request/connection counts, concurrent loads, managed allocation peak and native decode/process-memory peak with identical fixtures/build. Native codec temporary allocations and SVG complexity are not established by headless tests. Capture UIA outcomes plus screenshots and visually inspect them under winui-ui-testing. Release/AOT runtime acceptance is also unrun.

The 30-second deadline is configured but its elapsed-time behavior is not directly tested; cooperative cancellation propagation is tested. Dynamic resizing currently re-downloads rather than caching compressed images; rapid changes cancel obsolete work and retain the four-load bound. Actual request churn and rendering behavior need the pending UI sample.
