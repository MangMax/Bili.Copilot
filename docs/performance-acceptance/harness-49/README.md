# Offline actual-control acceptance #49

See [ui-49.md](../ui-49.md) for **failed acceptance**, exact fixture history and scope limits. This host links production panel/codebehind/base/VM/session/parser without rewriting orchestration. Only settings, font enumeration and composition services are offline adapters. Reflection is read-only observation, not handler invocation.

Use an unlocked, exclusively granted local desktop and existing cached dependencies/WinApp 0.7.1. `prepare.ps1` creates a fresh owned temp directory; do not reuse someone else's registration. Package identity is BiliCopilot.Acceptance49C4D3. No account, credentials, media or network is used by the host.

Build the prepared Harness.csproj with `dotnet build -p:Platform=x64`, then project-mode `winapp run --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory <owned-layout>`. Use the returned PID, never 9608. Run `run.ps1` with each engine and distinct artifact directories, then `probes.ps1`. Each script checks CLI exit codes and bounded UIA readiness; exit 0 only means collection succeeded. Review every PNG independently of UIA and internal state.

`evaluate.py` evaluates the committed final run (80 snapshots from last activation); it exits 1 for the six observed acceptance failures. It does not execute or simulate production code. Use a separate version of the evaluator if a future experiment changes the scenario sequence; never relabel old output.

Buttons DirectX/Win2D create fresh panels. Switch uses the existing panel/VM Renderer setter. Burst queues 100 alternating Renderer/Redraw calls and a final Initialize/position/Redraw. UnloadQueued queues redraw and renderer selection before removing the panel. Remount adds that same instance and observes actual Loaded. No lifecycle repair is performed by the host. Realtime calls the actual public AddDanmaku; it is a separate visibility control, not a replacement for cache-content validation.

Cleanup: copy owned LocalState/events.jsonl, invoke Close on the owned process, verify exit, resolve the exact package name and require its InstallLocation equals your layout before Remove-AppxPackage. Set WINAPP_UI_WORKFLOW_ID=bili-acceptance49-c4d3 and `winapp ui yield --on local`. Do not stop/remove any Dev/production or unrelated processes/packages. Temporary binaries are not deliverables.
