# Offline engine spike #57

This is disposable experimental code, not a production adapter. It uses the actual restored `Danmaku.Core`, `Danmaku.Legacy`, and `Danmaku.Share` preview6 packages. The source is synthetic; there is no HTTP, account, player, mpv, or credential access. See [the report](../issue-57.md) for failures and interpretation.

Prerequisites: existing Windows/.NET 9-compatible SDK, WinApp CLI 0.7.1, cached dependencies, desktop permission. Do not install/enable anything to satisfy these instructions. Use a fresh temporary directory. `prepare.ps1` refuses an existing destination. Its package identity is reserved for this experiment: if already registered, verify it is yours before use; do not replace someone else's package.

```powershell
$destination = Join-Path $env:TEMP 'BiliSpike57-a35e'
./docs/performance-acceptance/harness-57/prepare.ps1 -SourceRoot . -Destination $destination
$env:WINAPP_UI_WORKFLOW_ID = 'bili-spike57-a35e'
winapp run "$destination/Harness.csproj" --arch x64 -p Platform=x64 --detach --json --output-appx-directory "$destination-layout"
# Capture the returned ProcessId, NEVER a pre-existing app PID.
./docs/performance-acceptance/harness-57/run-ui.ps1 -AppPid <owned-pid> -ArtifactDirectory <fresh-absolute-directory>
./docs/performance-acceptance/harness-57/run-probes.ps1 -AppPid <owned-pid> -ArtifactDirectory <another-fresh-absolute-directory>
python ./docs/performance-acceptance/harness-57/schedule-model.py
```

`run-ui.ps1` checks command exit codes, waits for experiment state and records captures. State text is **not** a rendering assertion. Manually view every PNG. `visibleUpperBoundMs` in raw captures is only command-to-capture elapsed time; it is a valid visible bound **only when that capture actually contains the expected text**. Empty/corrupt captures must not be counted as success. `run-probes.ps1` intentionally separates captures by two seconds for motion observation. `--capture-screen` is used in the final scripts after default captures proved unreliable; even screen captures showed unresolved paused/blank observations on this machine.

Modes:

- `Window`: synthetic segments 1–2; `Full`: segments 1–120 (12-hour timeline, six concurrent requests). Each request takes 30 ms, except segment 120 takes 3,000 ms. Both modes await their selected set, then submit sorted items at position 1001 ms.
- `Dense`: 20,000 items per segment, two segments. Only two items are due at the captured time. This exercises dense data loading, **not** 40,000 simultaneously visible items or sustained GPU saturation.
- `Primary`: request current and slow neighbor together; submit current after 30 ms, while neighbor completes after 3,000 ms. Neighbor completion is logged but not injected into the engine. This proves first-segment display independently of a slow neighbor, not seamless incremental append.
- `Append`: existing duplicate ID, an earlier-time new item, and a future item. Logs public `GetDanmakuList()` output. `Replace`: shorter list without seek. `Seek`: explicit seek and delayed pause restoration.
- `Rolling` pauses a moving item; `SeekPaused` deliberately seeks without restoring pause. These are negative contract probes. The 250/500/800 ms setup delays are observations, not readiness guarantees or recommended production waits.

Measurements append to the owned package's `LocalState/measurements.jsonl`. Copy that file before removing the package. `submitMs` is Stopwatch time from load-start through synthetic acquisition, flatten/sort and synchronous API submission. It excludes renderer creation and does not measure first visible frame. Process private bytes/working set are event snapshots from `Process.GetCurrentProcess().Refresh()`, not peak memory or heap live objects.

For close-during-load, invoke `Full` then immediately `Close`, retain `closed`/`canceled` rows and verify the returned PID exits. Repeat in a fresh activation for the other engine. This validates this harness's cancellation, not production PlayerViewModel wiring.

Cleanup: invoke `Close` on the known owned PID, verify its windows/process disappear; copy measurements; resolve `Get-AppxPackage -Name BiliCopilot.Spike57.A35E` and require its install location to equal your owned layout before `Remove-AppxPackage -Package <that-exact-full-name>`. Never use wildcard removal or uninstall Dev/production identities. Always `winapp ui yield --on local` with the same workflow ID. Preserve artifacts; runtime binaries must not be committed.
