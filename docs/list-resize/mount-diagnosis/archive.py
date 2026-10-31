"""Create the compact evidence archive; never include executable layouts or dumps."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[3]
native = root / "artifacts/recycled-cards"
metadata = root / "artifacts/mount-diagnosis"
payload = {}
for stage in native.iterdir():
    if not stage.is_dir() or stage.name in ("host", "source"):
        continue
    for p in stage.rglob("*"):
        if p.is_file() and "layout" not in p.relative_to(stage).parts:
            payload["runs/" + stage.name + "/" + p.relative_to(stage).as_posix()] = p.read_bytes()
for p in metadata.iterdir():
    if p.is_file() and p.suffix not in (".zip", ".dmp"):
        payload["metadata/" + p.name] = p.read_bytes()
for name in ("production-inputs.json", "kernel-source-inputs.json"):
    payload[name] = (metadata / name).read_bytes()
host = native / "host"
for p in host.iterdir():
    if p.is_file() and p.suffix in (".cs", ".xaml", ".csproj", ".props", ".manifest", ".appxmanifest"):
        payload["replay/host/" + p.name] = p.read_bytes()
for p in (host / "Assets").rglob("*"):
    if p.is_file():
        payload["replay/host/" + p.relative_to(host).as_posix()] = p.read_bytes()
for p in Path(__file__).parent.iterdir():
    if p.is_file():
        payload["replay/scripts/" + p.name] = p.read_bytes()
inputs = json.loads((metadata / "production-inputs.json").read_text())
for name in inputs:
    payload["production-inputs/" + name] = (root / name).read_bytes()
manifest = {name: {"size": len(data), "sha256": hashlib.sha256(data).hexdigest()} for name, data in sorted(payload.items())}
out = root / "docs/list-resize/mount-diagnosis-evidence.zip"
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for name, data in sorted(payload.items()):
        z.writestr(name, data)
    z.writestr("manifest.json", json.dumps(manifest, indent=2))
print(json.dumps({"path": str(out), "payloads": len(manifest), "bytes": out.stat().st_size}))
