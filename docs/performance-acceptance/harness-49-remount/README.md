# #49 remount regression

Uses the original harness-49 App, adapters, project and actual production-linked control/VM. Only the temporary package identity and window title change. No renderer implementation is copied. Production XAML remains byte-identical. Requires the existing local WinApp 0.7.1 toolchain and exclusive desktop authorization.

```powershell
./docs/performance-acceptance/harness-49-remount/prepare.ps1 -SourceRoot . -Destination "$PWD/artifacts/acceptance49/harness"
dotnet build artifacts/acceptance49/harness/Harness.csproj -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false
winapp run artifacts/acceptance49/harness/Harness.csproj --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory artifacts/acceptance49/layout
./docs/performance-acceptance/harness-49-remount/run.ps1 -AppPid <owned launch PID> -ArtifactDirectory <new evidence directory>
```

The script records structural checks separately from engine pause-state checks. Exit 1 remains expected while pause defects are present; never call this whole matrix green. `green` names the lifecycle-fix run, not full #49 acceptance. Inspect every final PNG; blank canvas does not pass visual acceptance. Reflection is observational only in the original harness.

The operator must close owned processes, verify the exact temporary identity's InstallLocation before removing it, then run `winapp ui yield --on local --json` with `WINAPP_UI_WORKFLOW_ID=bili-acceptance49-31f2`. Never remove Dev/production packages or act on other workflows. Script intentionally keeps the app alive for visual follow-up and does not own deployment cleanup.
