# Independent fixed-source recheck #46 / #47

2026-10-30, Asia/Shanghai. **Original reattachment regression: PASS. Overall extended UI batch: NOT PASSED (17 passed, 1 failed).** Release suite and Debug x64 app build passed. No production fixes were made by this task.

## Provenance and merge

- Shared parent checkout: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`, branch `feat/performance-45`.
- Verified initial HEAD `d4592d3579aacaf4dcef24ccd9396c1ecc33506f`, including the Markdown Loaded restoration fix after #49 tip `ddc6074`.
- Verified `HEAD..67afccf` contained only evidence commit `67afccf7a68ab02e85fc2ea1cebdd32543adf63b` from `t3-5b5b1beb`; all its changes were under `docs/performance-acceptance`. Merged with `git merge --no-ff 67afccf7a68ab02e85fc2ea1cebdd32543adf63b`, producing `57dceffb1f20dc9db281755acb593c98265534f8`.
- Production source tested is unchanged from `d4592d3`. Kernel remains clean at `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. The historical `evidence-46-47` and `ui-46-47.md` are byte-for-byte unchanged from the evidence commit; see [isolation verification](evidence-46-47-fixed/isolation-final.json).
- Final owned PID **55384**, AUMID `BiliCopilot.Acceptance4647_y1mf40g4r9srg!App`. SDK 10.0.403, WinApp CLI 0.7.1, real local WinUI x64 Debug/self-contained harness. [Source and assembly hashes](evidence-46-47-fixed/provenance.json), [launch](evidence-46-47-fixed/launch.json), [temporary paths](evidence-46-47-fixed/run-paths.json).
- The window title retains the original harness's `eeb28f2` suffix for its selector; it is **not the tested source SHA**. Linked sources and hashes above establish provenance.

## Findings for parent

1. **Original defect no longer reproduced.** The original `SameInstanceUnchangedTextAttach` predicate was not edited or weakened. Actual unload/load of instance 1 with unchanged fixture restores all 7 blocks; `loads=2`, `unloads=1`, `IsLoaded=True`. Text, table, highlighted code, hyperlink and offline image are visible in [05-reattach.png](evidence-46-47-fixed/05-reattach.png) and its UIA tree. This directly contrasts with the preserved original red evidence (0 blocks).
2. **Unresolved extended lifecycle result — do not call full acceptance green.** After three further same-instance detach/attach cycles, an empty-text detach/attach, and reassignment of the fixture, `LifecycleBoundaries` reports FAIL with 7 blocks and `loads=6`, `unloads=5`. Correction after independent image inspection: the final [05b-lifecycle.png](evidence-46-47-fixed/05b-lifecycle.png) visibly contains the heading, body, table, code, hyperlink, image and FINAL-OMEGA. The earlier description “only the embedded code block” was incorrect. [UIA](evidence-46-47-fixed/05b-lifecycle.uia.json) also contains the complete body, table cells and hyperlink. The FAIL label records an earlier assertion, not proof of missing pixels in this capture. A previous run of the same final harness assembly passed this in-process assertion (`capture-attempt/observations.json`), but its screen capture failed. This could involve asynchronous layout/image readiness, drawing, or direct-window capture; the compound predicate did not record which term failed. **It is not established as a production regression or dismissed as a harness-only defect.** Parent should isolate predicate terms and observable readiness before deciding whether production work is needed. No extra production repair or endless rerun was attempted.
3. **Existing visual warning persists.** The table wraps `fixture` into `fixtur` + `e` despite available horizontal space. Initial, reattached, fresh and final screenshots show this. No baseline proves a new regression. Treat table sizing/padding as a separate follow-up.

## Requirement coverage

[Final structured results](evidence-46-47-fixed/results.json), [in-process observations](evidence-46-47-fixed/observations.json), [UI runner output](evidence-46-47-fixed/ui-run.txt), [parsed UIA review](evidence-46-47-fixed/uia-review.json).

| Requirement | Evidence |
| --- | --- |
| First template and initial Loaded | `InitialTemplateLoaded` PASS, instance 1, loads 1/unloads 0, exactly 7 blocks with full rendering; `00-initial.png`. |
| Text/table/code/link/image | `Render` PASS, 7 blocks, full fixture and real production controls; `01-render.png`. Table warning retained. |
| Clear and reassign | `Clear` PASS, 0 blocks; `Reassign` PASS, 7 blocks; `02-clear.png`, `03-reassign.png`. |
| Unload cleanup | `Detach` PASS, loaded false and 0 blocks; `04-detach.png`. |
| Same instance, unchanged Text reattach | Original `SameInstanceUnchangedTextAttach` PASS; 7 blocks after actual second Loaded; `05-reattach.png`. |
| Repeated mounts without duplicate blocks | `RepeatedAttach1`, `RepeatedAttach2`, `RepeatedAttach3` all PASS, exact same object/text, exactly 7 blocks each; loads 3/4/5 and unloads 2/3/4 in observations. Each intermediate unload also requires unloaded state and zero blocks. |
| Empty text reattach | `EmptyTextReattach` PASS, unchanged empty string, loaded true, 0 blocks, loads 6/unloads 5. This is empty string coverage, not a separate null-text test. |
| Reassign after empty reattach | `LifecycleBoundaries` FAIL, 7 blocks but compound rendering predicate false. Complete screenshot and UIA follow an earlier failed compound assertion; its failed term was not recorded. |
| Fresh control | `FreshReopen` PASS, instance 2 and 7 blocks; `06-fresh.png`. |
| Coalesced selectable streaming | `CoalescedSelectableStreaming` PASS, first 156 characters exact via UIA, one queued publication, selection enabled and zero Markdown blocks; `07-stream.png`, `streaming-properties.json`. No physical selection gesture tested. |
| Immediate final publication | `FinalFlush` PASS, exactly 2 publications, exact 312-character fixture, generating false and full formatting; `08-final.png`. |
| Cancellation/late output isolation | `CancelledLateBeforeNewCompletion`, `CancelledLateAfterNewCompletion` PASS; old queued flush/append/Complete cannot overwrite or end the replacement session; `09-cancel-late.png`. |
| Clipboard | `ProductionCodeClipboard` PASS invokes real code-copy handler through UIA; `AnswerClipboard` PASS is harness full-answer copy only. |
| Cleanup/isolation | `CloseOwnedWindow`, `ProductionSourceAndManifestUnchanged`, `RemoveOwnedTemporaryPackage`, `YieldDesktop` PASS. |

All **11 final PNGs** were individually opened and inspected. Initial/render/reassign/reattach/fresh/final contain the expected text, code, hyperlink and image; clear/detach are blank; streaming is unformatted ordered text; cancellation shows only `NEW-ONLY-FINAL`. `05b-lifecycle` also contains the full fixture; its previous partial-drawing description is corrected above. See [independent diagnosis](ui-46-47-diagnosis.md). No broad theme/contrast/RTL/accessibility matrix was run.

## Review of the restoration change

`_restoreOnLoaded` defaults to false, so first template application performs `Build` without an additional initial Loaded render. `Unloaded` clears the document/renderer before setting the flag. When an existing renderer is loaded again, `ApplyText(Text, true)` calls `ReloadDocument`, clearing previous content and reinitializing the renderer stack before rendering, then the flag becomes false. This explains why repeat attachments do not append duplicate blocks. An empty string takes the cleanup path and the flag still resets. The `_renderer != null` guard avoids using an unconstructed renderer; if it is absent, template construction remains responsible for the initial document. These are source-review conclusions supplemented by the actual behavior cases above, not private-field tests.

No new definite production-code error was established by this focused review. The extended lifecycle failure remains unresolved rather than waived. Existing project analyzers ran in the app build; no extra WinUI analyzer package was added and no claim is made for that specialized analyzer suite.

## Build and test commands

No repository run-tests overlay was present. The test project uses SDK-style xUnit 2 and Microsoft.NET.Test.Sdk/VSTest. Commands completed with exit **0**:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=release.trx' --results-directory docs/performance-acceptance/evidence-46-47-fixed
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
```

**Passed: 342, Failed: 0, Skipped: 0.** [Parsed TRX counters](evidence-46-47-fixed/release-counters.json) confirm total/executed/passed 342 and failed/notExecuted 0; [TRX](evidence-46-47-fixed/release.trx), [test log](evidence-46-47-fixed/release-tests.txt). [Debug app build](evidence-46-47-fixed/debug-build.txt): 0 warnings, 0 errors. This builds the app without installing or launching Dev/production.

Harness preparation/build/launch followed [README](harness-46-47/README.md), using paths in `run-paths.json`:

```powershell
./docs/performance-acceptance/harness-46-47/prepare.ps1 -SourceRoot $source -Destination $project
dotnet build "$project/Harness.csproj" -p:Platform=x64 -v:minimal
$launch = winapp run "$project/Harness.csproj" --arch x64 --no-build --detach --json --output-appx-directory $layout | ConvertFrom-Json
./docs/performance-acceptance/harness-46-47/run-ui.ps1 -AppPid $launch.ProcessId -Layout $layout -PreparedProject $project -Evidence $evidence
```

Harness final build: exit **0**, 0 warnings/errors ([log](evidence-46-47-fixed/harness-build-final.txt)). Final UI runner: exit **1**, 17 PASS / 1 FAIL. These are different validation outcomes and are not conflated.

## Retained attempts and evidence limits

- `startup-attempt`: PID 32728 was checked before its window was ready; no behavior tests ran, title check and UIA Close failed. The owned layout package was removed, process exited, desktop yielded. Added bounded `HarnessStatus` readiness wait.
- `observer-attempt`: PID 65188; original regression and repeated/empty cases passed; immediate reassignment observation failed. All 11 PNGs were viewed; two immediate captures had incomplete content. Reassignment observation was moved to the original harness's dispatcher helper without weakening its predicate.
- `capture-attempt`: PID 21056; all in-process observations passed, but screen-region captures returned `foreground_not_target`; no screenshot delivery or full batch PASS is claimed. Followed tool advice to use direct HWND capture. Final runner uses a 300 ms compositor delay, without changing assertions.
- Final PID 55384: 11 direct HWND screenshots delivered; `LifecycleBoundaries` still failed. Earlier attempts remain separate and are not overwritten by final results. Original historical red evidence is also untouched.

The harness links actual production Markdown sources/template and `StreamingResponseSession`; the streaming TextBlock comes from production AIBody XAML with only ViewModel binding removed. Localization and post-copy notification are stubbed; images use a local packaged PNG through the production provider seam. No authenticated AIViewModel/provider/network business flow was exercised. Source switch/regeneration commands, production full-answer copy, network image cancellation, external hyperlink navigation, Release/AOT, GC pauses/survivors/allocation or latency improvements remain unverified. The prior WPR error `0xc5585011` was not retried. No installation, sandbox enablement or system policy change occurred.

## Desktop release

Before each launch, the acceptance identity was absent. Each launched PID's executable was checked against the owned layout and explicitly rejected PID 9608. Cleanup checks the exact package install location before removal. No other package was uninstalled; no Dev/production package, credential or other-task window was manipulated.

Final owned PID 55384 exited through UIA Close; all task PIDs 32728, 65188, 21056 and 55384 are confirmed absent. The acceptance package is absent. [Final yield](evidence-46-47-fixed/desktop-release.json) exited 0 and returned `released: false`; this is not a positive lease-release event and is recorded verbatim. The workflow has ended and this task relinquishes desktop ownership. **Exclusive desktop slot is released.** No push, PR/issue modification or child agents.
