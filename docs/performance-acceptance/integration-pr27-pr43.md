# PR27 + PR43 integration

Status: IN PROGRESS; local integration only. No push, PR/issue edits or dependency upgrade.

Scope L, high difficulty: A baseline → B PR27 / paging and refresh → C PR43 / lifetime → D combined native acceptance. Each phase has its own commit/evidence. User authorized this decomposition and self-execution; no agents spawned.

Baseline `bd8f51638a77185a562158a8d42a2d92cce4e68e` verified ancestor of local `feat/performance-45`; starting branch `t3/integrate-pr27-pr43` clean. GitHub heads rechecked and exact pull refs fetched: PR27 `863a941aa98b164778f862935e0c632c4cecd829`, PR43 `5c1b52bb365c8888018969c2380623c9c7409bb5`. Both reviews, comments and inline review comments empty. Source diffs and issues saved under ignored `artifacts/integration-pr27-pr43`.

## A: pre-upgrade baseline

Fresh Release xUnit/VSTest: Passed 403, Failed 0, Skipped 0; `artifacts/integration-pr27-pr43/stage-a/stage-a.trx`. Kernel `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`. SDK 10.0.403, application net9 Windows, winapp 0.7.1. First build reached the runtime-assets gate and failed (missing libmpv); retained stage-a-build.log. Copied only ignored libmpv/ffmpeg from the main checkout, then Release x64 JIT build passed, 0 warnings / 0 errors (stage-a-build-assets.log).

Replay: `./docs/performance-acceptance/integration-pr27-pr43/replay.ps1 -Gate test -Stage stage-a`; use `-Gate build` for Release x64 JIT, `-Gate environment` for provenance. No package version changes. Dedicated WinUIAnalyzer is not installed; no unrequested analyzer/package upgrade. Existing AOT missing-C++ gate and #49 pause FAIL remain. #61 docs preservation does not approve its request-changes design or integrate an engine replacement.

Controlled performance method: warm same isolated complete app, same settings page, disable notifications before restart, no playing/downloading, sample 60 seconds visible / hidden / restored with `sample-process.ps1`, retain raw UTC/process counters and visibility evidence. No forced GC or working-set trim. UI refresh cost requires real-control/frame/anchor evidence; test-run duration is never used as UI performance. Separate pure-list allocation/elapsed measurements from UI evidence.

## Product direction / pending evidence

PR27 latest implementation intentionally supersedes original #22 result-count text with loading + undo-only overlay and adds pageSize/three-page batching. Keep default opt-in, old item identity and continuation cursor, undo within current section. Empty/failed requests must not destroy existing content; stale completions must not cross section versions. Original PR LCS allocates a quadratic matrix; loaded lists are not limited to one page.

PR43 default opt-in residency with same-window restore and ordered explicit exit is consistent with #39. Its own full acceptance remains NOT ACCEPTED; previous runtime evidence is not fresh combined validation. Preserve #48 cancellation, #54 late window-mode cleanup and #55 child-process strategy.

## Desktop ownership

No desktop actions yet. Protected PID9608, Dev package and authenticated `Mang.BiliCopilot.PerfAcceptance.Caa6a7be` layout/data are untouched. No credentials read/copied. No owned package or app process yet; no yield needed yet.

## B: PR27 merge and scoped repairs

Conflicts: the kernel gitlink was merged, never selected wholesale; Popular properties keep both baseline section/display ownership and refresh state; requests keep generation-checked load commits plus pageSize; commands keep nonblocking directory initialization, queued-switch version checks and positive-growth-only load notifications. Native partition testing also found cache ownership was saved after changing IsRecommend; saving now uses the departing section. Stale partition page/refresh completions cannot publish or reset another section's loading state.

TDD evidence: stage-b-merge-red.trx had 1 failure / 26 passes (existing cursor test); stage-b-behavior-red.trx had 4 failures / 0 passes. Empty batches stop immediately, ordinary reload retains its one-request behavior, opt-in incremental batches stop on empty/duplicate/no cursor progress, previous snapshot survives empty/error, old refresh releases ownership when switching section. Fresh 454/0/0 suite at artifacts/integration-pr27-pr43/stage-b/stage-b.trx. Kernel subset 10/0/0 includes real service→transport pageSize/cursor + caller cancellation and existing gRPC/web cancellation/no-retry tests.

Real NuGet WinUI partition command tests: 3 FAIL before, identical 3 PASS after; raw partition-red.json / partition-green.json in evidence. The host uses actual ViewModelBase and LayoutUserControlBase from restored Richasy.WinUIKernel.Share, controlled service responses and display adapters. This is not full authenticated app acceptance. Own PIDs 60692 and 35628 closed; yields released=true. Controls package remains owned for D reruns. Initial harness build lacked the Close localization enum; fixed fixture resource mapping, then Release JIT built (65 fixture nullable/AOT warnings, no errors). No analyzer warnings were suppressed. First long screenshot was captured while loading and is not counted as populated-card evidence.

LCS scoped repair trims common prefix/suffix before allocating the middle matrix; the 5000-item prepend+undo regression failed at 200541168 allocated bytes, now passes a 4 MB upper bound with identity intact. Arbitrary large reorder still has quadratic middle cost: no claim of general linear LCS or cache eviction. Cache budget #56 remains a separate investigation. Undo explicitly restores snapshot object identities for equal-key replacements; scroll anchoring still requires D real-control evidence.

Kernel merge: 98d0b9d0f0c81bd3904b1425ef3b90f637af37d7, parents 2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d and f02c33f639addd5d421700c43fa66382beafe323. Both parents fetched from origin; cancellation is on origin/fix/copilot-48-danmaku-cancellation, pageSize on origin/main. Merge is LOCAL ONLY, not remotely reachable. Verified kernel.bundle includes integration/copilot-pr27-pr43 and requires common ancestor 6501985d0f30c55c28b3f78c6e045c2536e1d389. Restore in an initialized kernel repository with `git fetch <absolute-path>/kernel.bundle refs/heads/integration/copilot-pr27-pr43:refs/heads/integration/copilot-pr27-pr43`, then checkout the recorded merge SHA. Parent decides publishing a dedicated kernel branch. Original authors and commits preserved; no external default branch changed.
