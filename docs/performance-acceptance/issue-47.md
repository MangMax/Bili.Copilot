# Issue #47 — Markdown cleanup without forced GC

Date: 2026-10-30. Parent specification: #45.
Status: implementation and static review complete; build blocked; real WinUI and performance acceptance pending.

## Scope and checkout

- Claimed `MangMax/Bili.Copilot#47` using `gh issue edit 47 --repo MangMax/Bili.Copilot --add-assignee '@me'` (exit 0).
- Read #47 and #45 bodies/comments using `gh issue view ... --json number,title,body,comments`; both had no comments. Open PRs #27, #43 and #44 did not duplicate this change.
- Branch: `t3/remove-gc-allocations`.
- Worktree: `C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-a488f7ab`.
- Starting HEAD and local integration `feat/performance-45` both were `90f357733c30d7cc405a7f6f649922f3ab8ed29c`.
- First `git merge feat/performance-45` returned exit 0, `Already up to date.` Integration subsequently advanced; repeated the merge before final verification, fast-forwarding to `ed35a68bb894cd63640435220008c5c79a0d1921` (includes #46). No integration checkout was modified.
- Pre-existing submodule state was preserved: recorded gitlink `6501985d0f30c55c28b3f78c6e045c2536e1d389`, submodule HEAD `f02c33f639addd5d421700c43fa66382beafe323`, 474 staged file deletions (82,495 deleted lines), with only `.git` present in its working directory. No submodule changes are included in this commit.

## Change and testing decision

Removed only `GC.Collect()` from `MarkdownTextBlock.Clean`. Both `RichTextBlock.Blocks.Clear()` and `_renderer?.Clear()` remain. Empty-string assignment and `Unloaded` still use that cleanup path.

The approved seam is the real Markdown control: display text/link/image, clear or unload, then display again. The existing net9.0 xUnit project links pure logic and cannot instantiate WinUI controls. No test framework, abstraction, source-text assertion or mock GC was added. No red/green lifecycle result is claimed. The existing suite is a general regression baseline, not coverage of this control.

## Commands and exact results

Commands ran from the worktree with .NET SDK `10.0.403`, using the existing xUnit 2 / VSTest runner. The repository run-tests overlay and global.json were absent.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=baseline.trx' --results-directory artifacts/issue-47
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:AppxPackageSigningEnabled=false
git merge feat/performance-45
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=final.trx' --results-directory artifacts/issue-47
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:AppxPackageSigningEnabled=false
git diff --check
```

After integration advanced, repeated `git merge feat/performance-45`, the same test command with `LogFileName=integration-final.trx`, and the same build command. This is the final verified base.

| Verification | Result | Local evidence |
| --- | --- | --- |
| Pre-edit tests | Exit 0; Passed: 282, Failed: 0, Skipped: 0; 412 ms test duration | `artifacts/issue-47/baseline-tests.log`, `baseline.trx` |
| Initial build attempt | Exit 1; 1 warning, 8 errors; elapsed 25.35 s | `artifacts/issue-47/baseline-build.log` |
| Post-merge tests | Exit 0; Passed: 282, Failed: 0, Skipped: 0; 317 ms test duration | `artifacts/issue-47/final-tests.log`, `final.trx` |
| Post-merge build | Exit 1; 1 warning, 8 errors; elapsed 4.97 s | `artifacts/issue-47/final-build.log` |
| Latest integration tests (`ed35a68`) | Exit 0; Passed: 288, Failed: 0, Skipped: 0; 561 ms test duration | `artifacts/issue-47/integration-final-tests.log`, `integration-final.trx` |
| Latest integration build (`ed35a68`) | Exit 1; 1 warning, 8 errors; elapsed 5.63 s | `artifacts/issue-47/integration-final-build.log` |
| Whitespace check | Exit 0, no diagnostics | `git diff --check` |

The initial two TRX reports were parsed: total/executed/passed 282, failed/notExecuted 0. The latest integration TRX has total/executed/passed 288, failed/notExecuted 0 (six additional tests from #46). Logs and TRX are ignored local artifacts, not committed. The initial build was started before the edit but completed afterward; it failed in dependencies, so it is not a verified pre-edit app build.

Build blocker: MSB9008 reports the missing `externals/bili-kernel/src/BiliKernel.Abstractions/BiliKernel.Abstractions.csproj`; CS0246 in `BiliCopilot.UI.Models/MediaSnapshot.cs` reports missing `Richasy`, `VideoInformation`, `SeasonInformation`, `EpisodeInformation` and `VideoPart`. Restoring/resetting the pre-existing submodule state would overwrite local changes, so it was not attempted. No environment installation, package deployment, application launch, desktop input or runtime asset copy was performed.

## Per-ticket static WinUI review

Reviewed `MarkdownTextBlock.xaml.cs`, its XAML template, `WinUIRenderer`, `MyFlowDocument`, `MyImage`, and the Markdown bindings in `AIBody.xaml`.

- No introduced Error/Warning found in the one-line deletion. Cleanup still drops the block tree and renderer stack/renderers; removing GC does not alter those operations.
- Reassignment of a new nonempty Text value invokes `ReloadDocument`, which clears and repopulates the document stack and object renderers before rendering. This is static evidence only.
- Note / acceptance risk: unloading clears content; there is no Loaded handler to restore unchanged Text on the same instance. `OnApplyTemplate` uses `Build` with `rerender=false`. Same-instance reattachment, unchanged-value reassignment and fresh-instance reopening must be distinguished during acceptance; existing code is not proof that every case works. No speculative lifecycle fix was included.
- Note / acceptance risk: image loading is asynchronous and can retain content while in flight; GC removal does not supply deterministic native-resource disposal or cancel those requests. Image loading changes belong to #52, not this ticket.
- No XAML, binding, layout, theme, accessibility, localization, MVVM, interop or package-identity changes. No new API assumptions.
- The dedicated `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer` package is absent, so its checks did not run. No analyzer package was added for this one-line ticket. Full compiled review remains blocked by the dependency build failure.

## Pending acceptance for the parent

#48 owns the exclusive desktop/Sandbox slot. No slot was granted during this implementation; no UI or profiler acceptance was attempted.

1. Resolve the prepared checkout dependency state without discarding another worker's changes, then build the actual integration artifact. Record its exact SHA, configuration, runtime and tool versions.
2. Once the slot is available, use the real WinUI control with a fixed Markdown fixture containing plain text, a hyperlink and a reachable image. Record fixture/image content hashes and use identical material for before/after runs.
3. Verify display → empty string → new nonempty assignment; close/unload → reopen; and same-instance unload/reattach with unchanged and changed Text separately. Assert readable text, working link and visible image. Capture screenshots and structured UI assertions, then visually review every captured state. Record null assignment separately: the existing callback ignores null values.
4. Repeat clear/close cycles under equal Release build settings and material (for example 5 warmups, then 50 measured cycles per run, 3 runs per revision). Record induced versus natural GC events, GC pause durations, collection counts, allocations and surviving objects after equivalent observation periods. Retain traces and record the profiling commands actually used. Separate profiler-induced collections from application-induced ones.
5. Confirm no application-requested full collection originates in normal Markdown clear/unload. Natural runtime GC remains expected. Investigate retained-object roots if growth persists; do not use immediate working-set reduction as the acceptance criterion.

No GC pause, surviving-object, image-memory or user-visible speedup result is available. All ticket UI/performance criteria remain pending until those observations exist. Parent owns merge and subsequent acceptance; the issue remains open and no PR/push was made.

## Skills applied

Read the repository AGENTS.md and issue-tracker/domain instructions. Read skills under `C:/Users/hp/.codex/skills`: `tdd` (including tests/mocking guidance), `code-testing`, `run-tests`, `winui-dev-workflow`, `winui-code-review`, `winui-ui-testing`, and `verification-before-completion`. TDD/code-testing informed the decision against fabricated implementation-coupled tests; run-tests selected the existing runner; WinUI skills guided static review and the deferred acceptance boundary. User constraints override skill suggestions to install tools/analyzers or launch an app.
