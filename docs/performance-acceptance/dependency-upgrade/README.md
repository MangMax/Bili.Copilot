# Replaying the dependency experiment

Run from this checkout in PowerShell, with SDK 10.0.403, winapp 0.7.1, x64 runtime assets and desktop ownership. Use a fresh temporary fixture directory and a task-owned identity. Controls RunId is at most eight characters. Do not register Dev or the protected authenticated identity. The legacy preparation helper reads libmpv/ffmpeg from `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets`; these are runtime binaries only, not credentials.

## Recover the local-only kernel

The new bundle requires commit `6501985d0f30c55c28b3f78c6e045c2536e1d389`; the original integration bundle is retained next to the historical fixture. Verify the prerequisite in the target repository, then fetch the new bundle's `refs/heads/integration/dependency-upgrade-d48` and check out `0ae5e4e000a41a07f6696746706df11c06e8835b`. Both `2ca79bd` cancellation and `f02c33f` pageSize are ancestors. No remote branch is required or changed.

## Native player and Main

```powershell
$fixture="$env:TEMP/bili-dependency-replay1"
$layout="$env:TEMP/bili-dependency-layout1"
& docs/performance-acceptance/dependency-upgrade/prepare.ps1 -Destination $fixture -RunId Replay1
dotnet build "$fixture/BiliCopilot.UI.csproj" -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$((Get-Location).Path)/src/"
# Finish all builds/benchmarks before the native sampling starts.
& docs/performance-acceptance/dependency-upgrade/replay-player.ps1 -Fixture $fixture -Layout $layout -Run replay1
```

The driver freezes build output, fills only missing packaged content from the recipe without overwriting frozen files, excludes `.build.appxrecipe` from activation input, then uses winapp folder mode. Both project --no-build and ordinary folder mode can follow the recipe's absolute paths and inject later reference-project DLLs. It validates every deployed DLL against the frozen build, runs the historical Player15 then Main4/183 samples/20 loops, and verifies explicit exit before removing its own registration. It refuses stale evidence. A failed run preserves its state for diagnosis; inspect the recorded PID and identity before cleanup. The new font check opens real subtitle settings **after** idle sampling and captures the first populated selection. The historical baseline and the first RC replay do not contain that extra post-sampling check.

Controls use `prepare.ps1 -Kind controls -UseCurrentPackages`, then build Harness.csproj with `-p:SourceRoot=<checkout>` and `-p:Platform=x64`. `UseCurrentPackages` requires the restored production app's project.assets.json; it aligns explicit/transitive graphics dependencies. Launch with winapp --no-build and run the generated run-controls.ps1 using the owned PID/layout. Inspect results, close its explicit Close button and remove only that registration. A simulated callback is not physical device-loss testing.

To reproduce original baseline source, use `eff8d24` / kernel `98d0b9d` and the unchanged historical preparation scripts in a separately authorized checkout. Do not rebuild the retained baseline binary against upgraded references. Check the retained app DLL against baseline-hashes.json and all dependencies against the original build directory. The relocated runner changes provenance copying and bounded partial-JSON retries, not the measured workload. Two failed baseline redeployments exposed the recipe issue; preserve them as red deployment evidence, never as performance samples.

## Pure list workload

`measure-list.ps1 -Destination <fresh absolute path> [-Framework net11.0]` builds the unchanged historical benchmark program with the selected production RecommendListDiff.cs. Its preparation run is not a reported comparison. Use `replay-list.ps1` with three already-built Measure.exe paths and a fresh `-Tag` for interleaved independent processes. Each process performs three warmups then ten measured prepend-two/undo pairs at 100, 1000 and 5000 items. Final runs use `-Tag final`; earlier `repeat` files are pre-fix evidence.

`summarize.py` derives per-run medians/ranges and native CPU ms/s from cumulative CPU deltas divided by actual elapsed time. It asserts 61 rows/phase and scale 1.5. CPU ms/s is not percent across all logical processors. Never pool the 61 autocorrelated samples as 61 independent experiments.

## Isolated .NET 11 RC

`install-isolated-sdk.ps1` downloads the exact official SDK zip into a fresh ignored directory and verifies its SHA512. Existing installations are not overwritten. Candidate branch `t3/dependency-upgrade-net11-candidate` pins SDK/TFMs and H.NotifyIcon 2.4.1. In a process using that candidate:

```powershell
$env:DOTNET_ROOT=(Resolve-Path artifacts/dependency-upgrade/sdk11).Path
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version
```

Use the same build/test commands as stable. No machine-wide install or AOT toolchain is implied. The recorded native RC run is from `73b744e`; candidate `3367dd9` adds only the later pure-list allocation fix and has fresh compile/test evidence, not a second RC native run.

## Evidence

`versions.json` contains old/latest/selected/source/check-time rows; unused central declarations and sample-only kernel dependencies are distinguished in the report. `report-inventory.py` renders from frozen metadata without querying again. `archive-evidence.py` archives small raw JSON, deterministic gzip copies of full logs/TRX/metadata/red candidate patches, SHA256 hashes and package graph hashes. Decompress a `.gz` file to inspect its original contents. Large native binaries and most screenshots remain in ignored artifacts; screenshot hashes are in the ledger.

The final report names the exact production source commits and native fixture hashes. Later documentation-only commits do not change the production source tree.
