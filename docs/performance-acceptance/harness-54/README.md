# #54 real PlayerWindow offline experiment

Run only with exclusive local desktop ownership. No account, media, network capability, Dev identity or production identity is used. This is a packaged Debug x64 JIT harness, not a Release/AOT test.

`Harness.csproj` links unchanged production PlayerWindow, PresenterChangeSession and CursorGrid sources from `SourceRoot`. Real Windows App SDK AppWindow, presenters, XAML island, input activation and native HWND operations execute. No presenter implementation is copied into the fixture. The actual Richasy/WinUIEx/Toolkit packages are restored at the pinned versions (WinUIEx is transitive from Richasy).

Adapters replace only settings (process dictionary), service resolution (actual current DispatcherQueue), and PlayerOverlay/HeaderBar (focusable UserControl and observable header). No real PlayerViewModel, media controls, playback engine, navigation, production title-bar padding implementation or account services are composed. Settings restoration calculations execute in production PlayerWindow, but persistence across app processes is untested.

```powershell
./docs/performance-acceptance/harness-54/prepare.ps1 -SourceRoot . -Destination "$env:TEMP/bili-54-new-owned"
dotnet build "$env:TEMP/bili-54-new-owned/Harness.csproj" -c Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
if($LASTEXITCODE){throw 'Build failed'}
$launch=winapp run "$env:TEMP/bili-54-new-owned/Harness.csproj" --no-build --arch x64 -p Platform=x64 --detach --json | ConvertFrom-Json
if($launch.Error -or !$launch.ProcessId){throw 'Launch failed'}
# Pass absolute evidence path. This package identity must not already belong to another task.
./docs/performance-acceptance/harness-54/run.ps1 -AppPid $launch.ProcessId -Evidence C:/path/to/fresh-evidence
./docs/performance-acceptance/harness-54/verify.ps1 -Evidence C:/path/to/fresh-evidence -RequireFocus
# View every PNG, even if results are PASS. Finally, including after failures:
./docs/performance-acceptance/harness-54/cleanup.ps1 -AppPid $launch.ProcessId -Evidence C:/path/to/fresh-evidence
```

Do not supply `--output-appx-directory` for this reproduction: local CLI 0.7.1 attempted to register the nonexistent requested layout without staging it. The successful project launch registered its real build output; cleanup validates that exact registered path against the owned process.

The suite is initiated once by UIA. Its rapid calls run on the real UI thread so command-line startup overhead cannot push each operation past 500ms. Real time is retained: 180ms between operations, then a 200ms old-generation observation with a precondition rejecting missed timing, followed by bounded settling and a 1700ms stability horizon. Title tests observe the real queued callback's alignment changes, close before dequeue or during either 800ms await, then allow all relevant delays plus a low-priority dispatcher barrier to pass. All four mode entry points are invoked after destruction to exercise their guards. `IsWindow` validates actual HWND destruction. First-chance and unhandled exceptions are recorded; they are not marked handled.

Live GotFocus in RapidFinalNormal is a positive control; absence after close alone is insufficient. Header UpdatePadding has a live positive control too. Tests can detect exceptions and observable late effects, not every native API access: source review is still needed for the lifecycle guards. This is not proof of arbitrary native race freedom.

The initial evidence-54 run used a Grid overlay and lacked the focus positive control (18 PASS, limited evidence). The evidence-54-focus run uses the focusable fixture and an additional live focus assertion, with a fresh process/package: **10 PASS / 8 FAIL**, retained. Its five PlayerWindow screenshots are blank and fail visual review. There is no production fix or pass-by-retry policy.

The final source adds a separate `Diagnose` button, synchronous GettingFocus stacks, deferred GotFocus stacks and before/after-close markers. It does not change the red assertion. With a fresh process, run `diagnose.ps1` instead of `run.ps1` for the three-case minimum reproduction. The captured diagnostic has **3 FAIL**, all delayed GotFocus notifications; synchronous GettingFocus occurred before close, inside production Show/SetFocus. No synchronous post-close focus request was observed. Thus the strict `lateFocus == 0` oracle conflates deferred notification with new access to a destroyed HWND. Keep those failures as diagnostic evidence, not as a confirmed production guard bug or a clean acceptance result. See ui-54.md for unresolved visual and lifecycle boundaries.

## 2026-10-30 recheck in parent checkout

See [ui-54-recheck.md](../ui-54-recheck.md). Current scripts use the separate owned package `BiliCopilot.Acceptance54.R1D19` and workflow `acceptance54-recheck-1d19`; historical evidence and `verify.ps1` remain unchanged.

The focusable UserControl now paints the existing background on its Content Grid. `GridFixture`/`GridProbe` are visual comparisons only, using the same real PlayerWindow; RunTests always creates the focusable fixture afresh. `VisualProbe` records the actual loaded visual tree.

`NativeProbe.cs` resolves `PInvoke` in the production class's namespace, forwarding to the real generated Windows.Win32.PInvoke imports. Only SetFocus/SetForegroundWindow get invocation logs. No presenter algorithm, fake HWND or replacement production window is introduced. App instrumentation uses an explicit alias to the original imports. Header reads are recorded at the adapter getter; test code uses the separate Probe field, avoiding measurement pollution.

`Diagnose` still selects the original `lateFocus == 0` assertion and is expected to reproduce the historical red. `RunTests` uses the access-boundary oracle, with live native ResetFocus, GotFocus, activation and header positive controls. It requires zero post-close native focus/foreground requests, synchronous GettingFocus, activation callbacks, header getter/updates, plus destroyed HWND, disposed window and closed session. The private session `_closed` field is inspected read-only for this Debug acceptance fixture; this is deliberately not an AOT test.

Run `verify-recheck.ps1 -Evidence <fresh evidence directory> -RequireFocus` after `run.ps1`. It validates the 18 named cases, 12 close observations, three live controls, and classifies retained late GotFocus separately. This is observable boundary coverage plus source guard review, not a trace of all internal WinRT/native accesses. For pixel analysis without installing dependencies, run `powershell.exe -NoProfile -File analyze-pixels.ps1 -Evidence <recheck root>`.
