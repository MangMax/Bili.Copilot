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

