# Frozen local Danmaku feed

This directory is the repository-local NuGet source for the three active,
unpublished Danmaku packages. `nuget.config` maps the three exact package IDs
here; all other package sources keep their existing eligibility.

Active exact versions:

| Package | Version |
| --- | --- |
| Danmaku.Core | 0.0.1-seekd1fc.20261101.2 |
| Danmaku.Legacy | 0.0.1-win2dlayout.20261102.2 |
| Danmaku.Share | 0.0.1-win2dlayout.20261102.2 |

Do not rebuild, replace or publish a package under the same version. Only
`PackageVersion` changes when correcting package identity; all three assemblies
remain `1.0.0.0`. Core keeps its older Share minimum; the central Share pin
selects this compatible newer package.

Run `python -X utf8 -B packages/danmaku/verify.py` to check the committed
bytes, the central pins, the nuspec identity and the source mapping.

Use the SDK pinned in `global.json`, initialize submodules, and prepare native
assets as described in the root README. For an isolated restore from the root:

```powershell
dotnet restore src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --packages artifacts/fresh-packages --configfile nuget.config -p:Platform=x64 -p:PublishAot=false -p:NuGetAudit=false
```

For JIT development builds (no deployment):

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:NuGetAudit=false -p:SolutionDir="$PWD/src/"
```

Use `Release` in place of `Debug` for a Release JIT build. This does not qualify
AOT, performance, historical AV or device reset behavior. No fallback to the
old Danmaku preview6 combination is supported.
