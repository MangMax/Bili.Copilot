"""Restore pinned upstream bytes, overlay two candidate files, pack and build locally.

Does not launch UI, publish packages, or alter application dependencies/submodules.
"""
import argparse
import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
manifest = json.loads((HERE / "origin.json").read_text())
parser = argparse.ArgumentParser()
parser.add_argument("--name", required=True)
parser.add_argument("--archive", default=manifest["archive"])
parser.add_argument("--baseline", action="store_true")
args = parser.parse_args()
assert re.fullmatch(r"[a-z][a-z0-9-]{0,35}", args.name)
root = REPO / "artifacts" / ("legacy-seek-" + args.name)
assert not root.exists(), "Use a fresh name"
archive = pathlib.Path(args.archive)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(archive) == manifest["archiveSha256"]
root.mkdir(parents=True)
with zipfile.ZipFile(archive) as z:
    for n in z.namelist():
        assert not pathlib.PurePosixPath(n).is_absolute() and ".." not in pathlib.PurePosixPath(n).parts
    z.extractall(root / "extract")
source = next((root / "extract").iterdir())
source.rename(root / "source")
source = root / "source"
for name, expected in manifest["files"].items():
    assert sha(source / name) == expected, name
if not args.baseline:
    for name, hashes in json.loads((HERE / "changes.json").read_text()).items():
        replacement = HERE / "source" / pathlib.Path(name).name
        assert sha(replacement) == hashes["after"], name
        shutil.copyfile(replacement, source / name)
for name in ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"]:
    (root / name).write_text("<Project />")
shutil.copytree(HERE / "harness", root / "host")
shutil.copyfile(HERE / "Probe.cs", root / "host/Program.cs")
(root / "host/Assets").mkdir()
shutil.copyfile(source / "src/icon.png", root / "host/Assets/Logo.png")
version = "0.0.1-legacyseek1085f94e." + args.name
identity = "BiliCopilot.LegacySeek" + args.name.replace("-", "")
project = root / "host/Spike.csproj"
project.write_text(project.read_text(encoding="utf-8-sig").replace("0.0.1-legacyseek1085f94e.2", version))
appx = root / "host/Package.appxmanifest"
appx.write_text(appx.read_text(encoding="utf-8-sig").replace("BiliCopilot.LegacySeek1085f94e", identity))
(root / "run.ps1").write_text((HERE / "run.ps1").read_text(encoding="utf-8-sig").replace("BiliCopilot.LegacySeek1085f94e", identity).replace("legacy-seek-1085f94e", "legacy-seek-" + args.name), encoding="utf-8-sig")
(root / "NuGet.Config").write_text('<configuration><packageSources><clear/><add key="local" value="feed"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')

def run(command, name):
    with (root / name).open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=REPO, stdout=log, stderr=subprocess.STDOUT)
    print(name, "exit", result.returncode, flush=True)
    result.check_returncode()

for package in ["Danmaku.Share", "Danmaku.Legacy"]:
    run(["dotnet", "pack", str(source / "src" / package / (package + ".csproj")), "-c", "Release", "-p:Platform=x64", "-p:PackageVersion=" + version, "-p:TreatWarningsAsErrors=false", "-p:NuGetAudit=false", "-o", str(root / "feed")], "pack-" + package + ".log")
run(["dotnet", "build", str(project), "-c", "Release", "-p:Platform=x64", "-p:NuGetAudit=false", "--configfile", str(root / "NuGet.Config")], "build.log")
(root / "restored.json").write_text(json.dumps({"baseline": args.baseline, "version": version, "identity": identity, "files": {n: sha(source / n) for n in manifest["files"]}}, indent=2))
print("Prepared:", root)
print("Explicit local UI execution: & '" + str(root / "run.ps1") + "' -RunName native")
