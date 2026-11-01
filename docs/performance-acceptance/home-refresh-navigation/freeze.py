"""Freeze a completed JIT build, including recipe assets but excluding the recipe itself."""
import argparse
import hashlib
import json
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("output", type=Path)
p.add_argument("destination", type=Path)
args = p.parse_args()
source = args.output.resolve()
dest = args.destination.resolve()
if dest.exists():
    raise SystemExit("Use a fresh destination")
dest.mkdir(parents=True)
for item in source.rglob("*"):
    if item.is_file() and not item.name.endswith(".appxrecipe"):
        target = dest / item.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(item, target)
for item in ET.parse(next(source.glob("*.build.appxrecipe"))).iter():
    if item.tag.split("}")[-1] != "AppxPackagedFile":
        continue
    relative = next(x.text for x in item if x.tag.split("}")[-1] == "PackagePath")
    target = (dest / relative).resolve()
    if not target.is_relative_to(dest):
        raise SystemExit("Recipe path escapes destination")
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(item.attrib["Include"], target)
rows = [{"path": str(f.relative_to(dest)), "sha256": hashlib.sha256(f.read_bytes()).hexdigest()}
        for f in dest.rglob("*") if f.is_file()]
dest.with_suffix(".hashes.json").write_text(json.dumps(rows, indent=2))
print(f"Frozen {len(rows)} files")
