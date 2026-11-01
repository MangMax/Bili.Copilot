# Frozen local Danmaku feed

The active three unpublished candidate packages are committed as their original bytes.
The repository `nuget.config` uses this relative directory and maps the three exact
package IDs here; all other package sources retain their existing eligibility.
Do not rebuild or replace a package under the same version, or publish these packages.

Active version: **0.0.1-seekd1fc.20261101.2** for Core, Legacy and Share.
Original bytes and SHA256 are locked in [packages.json](../../docs/seek-responsive-fix/packages.json).
Core source: `e094d330a69dbedeba9b0260b6bb499391d643c4`;
Legacy/Share: `15434adfee3c80032ca12ab4840bc36f54858f65`.
[Reviewed source recovery](../../docs/seek-responsive-fix/README.md) includes full Git bundles,
patches, source snapshot and paired PDBs; copy the committed candidate-feed nupkg bytes
into a missing feed path only after checking packages.json. Never overwrite an existing version.
The fixed [independent review](../../docs/seek-responsive-review-d6d54b61/review.md)
retains terminal failure propagation and possible repeated logging limitations.
Dense Legacy ready remains slow; Core has no demonstrated gain in its short dense sample.

Historical packages below remain byte-identical evidence, not active central pins.

| Package | Version | nupkg SHA256 |
| --- | --- | --- |
| Danmaku.Core | 0.0.1-pxdpi.9dc8c6c9.clean | 00a76b9cd5890485bf8f1fcf1a510bf0e36eebee6039d63f19a169c0ae5be84a |
| Danmaku.Legacy | 0.0.1-reconstruct0b4f.final | 4c5c57f0df78e76870834af9a9b17d8b169d2a6c89aed49d66b94c587eb3b867 |
| Danmaku.Share | 0.0.1-reconstruct0b4f.final | 1fe14bfaa90ef8da9373ee4b5be6d06f7bcb2e3297d75e6f90e4f03e5fb3cfb4 |

Core source: `698d546e5c9edf5c96b6871357dc532542457c45`.
Legacy source: `d7984643f4792133e7664c55e01df6e95e9cbc21`.
The Legacy nuspec's historical application repository commit is **not** its engine
source commit. Share final is the frozen Legacy companion; its seven source
documents differ from Core's companion only in BOM/line endings, as recorded in
[the original acceptance](../../docs/engine-61-design-draft/combined-acceptance/README.md).

The original nupkg/snupkg, paired DLL/PDB and source-document checksum closure are
preserved in [offline-evidence.zip](../../docs/engine-61-design-draft/combined-acceptance/offline-evidence.zip)
with [its manifest](../../docs/engine-61-design-draft/combined-acceptance/offline-evidence-manifest.json).
Archive SHA256: `72f868fca9bb8e2dfcee5e1f77a93749f4d0cd6b42e5d619dc26d88b1a8d4508`.
From the repository root, `python -X utf8 -B docs/engine-61-design-draft/combined-acceptance/restore.py`
verifies the archive and every entry before recovery into the unused
`artifacts/combined-restored-60cd` directory. Its `feed/*.nupkg` are byte-identical
to this directory. Normal builds need no recovery step or agent cache.
Run `python -X utf8 -B packages/danmaku/verify.py` to check the committed feed,
archive identity, central versions and source mapping without restoring packages.

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
