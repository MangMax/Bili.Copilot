# Tray foreground replay

Use the retained RC3 SDK/NuGet/native resources. Do not install anything globally or deploy over Caa6a7be. This directory's helpers act only on the offline `Mang.BiliCopilot.TrayDiag.EE493` identity, except the explicitly named `logged-case.ps1` and `observe.ps1 -LoggedApp` historical diagnostic. Do not run the latter as ordinary replay.

Read-only verification, from repository root:

```powershell
python -X utf8 docs/performance-acceptance/tray-foreground/verify.py
# Also check existing actual layouts, binaries and source:
python -X utf8 docs/performance-acceptance/tray-foreground/verify.py --live
```

Existing red/green frozen hosts are at `artifacts/tray-foreground/fixture` and `artifacts/tray-foreground/green/fixture`. They are separate full UI source builds, not modifications of the logged-in layout. From this worktree, choose a **new** run name, start the retained-handle observer, and wait for `ready.json` / the offline `tray-ready.json` before interaction:

```powershell
# Default fixture is red. For green add:
# -FixturePath artifacts/tray-foreground/green/fixture
Start-Process pwsh -WindowStyle Hidden -ArgumentList @(
  '-NoProfile','-File','docs/performance-acceptance/tray-foreground/observe.ps1',
  '-Run','replay-red')

. docs/performance-acceptance/tray-foreground/ui.ps1 -Run replay-red
U @('wait-for','KeyboardReceipt','-t','30000')
Start-Process pwsh -WindowStyle Hidden -ArgumentList @(
  '-NoProfile','-File','docs/performance-acceptance/tray-foreground/native-competitor.ps1',
  '-Out',$out,'-MainHwnd',$W)
# Wait for competitor-ready.json. Ensure the target is unobscured during setup.
U @('focus','KeyboardReceipt')

pwsh -NoProfile -File docs/performance-acceptance/tray-foreground/case.ps1 `
  -Run replay-red -Case Mouse -NativeClose
```

The last command is the exact symptom assertion. It must go **red** on the frozen original host after UIA caption close and **green** on the fixed host. `-Case Invoke`, `Aumid`, `Menu` select other restore triggers. `-CaptionMouse` additionally selects actual mouse close: that normal-user control was green even without the fix. `-NativeClose` omitted means the test button directly calls MainWindow.Close, which also missed the bug. Avoid repeating passed scenarios. Case results cannot be overwritten.

The guard checks target EXE/PID/creation FILETIME, expected HWND at the mouse point, hidden/competitor preconditions, actual foreground **before** keyboard-tool activation, and real received text. A tool failure during setup is not a product verdict. Foreground failure is still red when the later input tool activates the app and receives text.

Exit via `U @('invoke','ExitOwned')`. Close the exact HWND from that run's `competitor-ready.json`, retain its Process.Handle before closing, and record its exit code. **Wait for the observer's exit-observation.json with last query >=30s and its successful completion before removing the exact temporary package.** Verify `Get-AppxPackage -Name Mang.BiliCopilot.TrayDiag.EE493` points to this run's layout before `Remove-AppxPackage`; never remove any other identity. If cleanup cannot finish, preserve the process and report the problem rather than claiming a pass or force-killing unrelated processes. Yield with the same `WINAPP_UI_WORKFLOW_ID=tray-ee493` and record the actual returned `released` value.

Fresh preparation (fresh artifact directory required): `prepare.py` archives this repository and the existing local kernel commit, copies the retained offline service composition and native resources, refreshes production UI sources, and adds TrayProbe only. `prepare-green.py` creates a separate green fixture using the current production App source. The original `prepare.py` was run before the production fix; use the original commit for a new red source build, not current fixed App bytes. Both reject existing destination directories.

Build command used, with `base` either `artifacts/tray-foreground` or `artifacts/tray-foreground/green`:

```powershell
$base='artifacts/tray-foreground/green'
$env:DOTNET_ROOT='C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-d48c749d/artifacts/dependency-upgrade/sdk11'
& "$env:DOTNET_ROOT/dotnet.exe" build "$base/fixture/BiliCopilot.UI.csproj" `
  -c Release -p:Platform=x64 -p:PublishAot=false `
  -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false `
  "-p:SolutionDir=$((Get-Location).Path)/artifacts/tray-foreground/source/src/" `
  "-p:BuildOutDir=$((Get-Location).Path)/artifacts/tray-foreground/references/" -nr:false -m:4
python -X utf8 docs/performance-acceptance/refresh-undo-controlled/freeze.py $base
```

Fresh production build used `artifacts/tray-foreground/production/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj`, corresponding SolutionDir, and `artifacts/tray-foreground/production-build/` output. Fresh test command used `dotnet test artifacts/tray-foreground/production/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter FullyQualifiedName~TrayLifecycleTests --logger "trx;LogFileName=tray.trx"`; the same newly built assembly ran the full suite with `--no-build` and `suite.trx`. Full absolute task paths and results are in the compressed logs/TRX. No AOT or installed fixed-full-app claim.

Evidence is gzip JSON/JSONL/text/TRX plus five inspected PNGs. First run's missing exit evidence and physical-green setup refusal remain failures in their own files. `collect.py` refuses overwrites; `--physical` appends only the later physical-close comparison. The report distinguishes those from application behavior.
