"""Prepare the archived final diagnostic host with a fresh owned identity."""
import argparse
import hashlib
import io
import json
import shutil
import subprocess
import uuid
import zipfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("--kernel-repo", type=Path)
p.add_argument("--kernel-source", type=Path)
args = p.parse_args()
if bool(args.kernel_repo) == bool(args.kernel_source):
    p.error("Supply one read-only kernel repository or preserved kernel source snapshot")
root = Path(__file__).resolve().parents[3]
out = root / "artifacts/recycled-cards"
if out.exists():
    raise SystemExit("Fresh checkout/output required; evidence is never overwritten")
host = out / "host"
source = out / "source"
host.mkdir(parents=True)
source.mkdir()
with zipfile.ZipFile(root / "docs/list-resize/mount-diagnosis-evidence.zip") as z:
    for name in z.namelist():
        if name.startswith("replay/host/") and not name.endswith("/"):
            dest = host / name.removeprefix("replay/host/")
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(z.read(name))
    expected = json.loads(z.read("production-inputs.json"))
    kernel_expected = json.loads(z.read("kernel-source-inputs.json"))
zipfile.ZipFile(io.BytesIO(subprocess.check_output(["git", "archive", "--format=zip", "HEAD", "src"], cwd=root))).extractall(source)
for name, digest in expected.items():
    if hashlib.sha256((source / name).read_bytes().replace(b"\r\n", b"\n")).hexdigest() != digest:
        raise SystemExit("Production input differs: " + name)
kernel = source / "externals/bili-kernel"
if args.kernel_source:
    shutil.copytree(args.kernel_source, kernel, ignore=shutil.ignore_patterns("bin", "obj", ".git", "_build"))
else:
    kernel.mkdir(parents=True)
    zipfile.ZipFile(io.BytesIO(subprocess.check_output(["git", "-C", str(args.kernel_repo), "archive", "--format=zip", "0ae5e4e000a41a07f6696746706df11c06e8835b"]))).extractall(kernel)
for name, digest in kernel_expected.items():
    if hashlib.sha256((kernel / name).read_bytes().replace(b"\r\n", b"\n")).hexdigest() != digest:
        raise SystemExit("Kernel snapshot differs: " + name)
(host / "Directory.Build.props").write_text("<Project><PropertyGroup><SourceRoot>" + source.as_posix() + "</SourceRoot></PropertyGroup></Project>")
identity = "BiliCopilot.Recycle" + uuid.uuid4().hex[:12] + ".Owned"
manifest = host / "Package.appxmanifest"
manifest.write_text(manifest.read_text().replace("BiliCopilot.RecycleD228Mount.Owned", identity))
print(host, identity)
