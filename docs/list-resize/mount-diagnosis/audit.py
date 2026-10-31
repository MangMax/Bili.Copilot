"""Audit a compact extracted archive, independently of runner exit codes."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path, PureWindowsPath

root = Path(sys.argv[1])
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

manifest = read(root / "manifest.json")
assert all((root / n).stat().st_size == v["size"] and digest(root / n) == v["sha256"] for n, v in manifest.items())
runs = root / "runs"
groups = [
    ["season-original20", "season-original05"],
    ["season-trace20", "season-trace05"],
    ["season-binding20", "season-binding-red20", "season-binding05"],
    ["pane-single", "pane-shared", "pane-independent", "pane-legacy-shared"],
    ["pane-legacy-cache2", "pane-legacy-independent2", "pane-fixed-cache2"],
    ["season-final", "pane-final-single", "pane-final", "pane-final-independent", "pane-final-legacy2", "pane-final-legacy-independent2", "pane-final-fixed2"],
]
def binary_hashes(stage):
    hashes = {PureWindowsPath(h["Path"]).name: h["Hash"] for h in read(runs / stage / "binary-hashes.json")}
    assert "Harness.dll" in hashes, stage
    return hashes
for group in groups:
    assert all(binary_hashes(name) == binary_hashes(group[0]) for name in group), group
for stage in runs.iterdir():
    source_hashes = stage / "source-hashes.json"
    if source_hashes.exists():
        for h in read(source_hashes):
            assert digest(stage / "sources" / PureWindowsPath(h["Path"]).name).upper() == h["Hash"], (stage.name, h["Path"])
for name, expected in read(root / "production-inputs.json").items():
    assert hashlib.sha256((root / "production-inputs" / name).read_bytes().replace(b"\r\n", b"\n")).hexdigest() == expected
green = ["season-binding20", "season-binding05", "pane-single", "pane-shared", "pane-independent", "pane-fixed-cache2", "season-final", "pane-final-single", "pane-final", "pane-final-independent", "pane-final-fixed2"]
for name in green:
    subprocess.run([sys.executable, str(Path(__file__).with_name("check.py")), str(runs / name)], check=True)
    wer = read(runs / name / "wer-exact.json")
    identity = read(runs / name / "identity.json")
    assert wer["pid"] == identity["pid"] and wer["path"] == identity["path"] and wer["observedSeconds"] >= 31 and not wer["events"]
for name in ("season-original20", "season-trace20", "season-native20b", "season-binding-red20", "pane-legacy-cache2", "pane-legacy-independent2", "pane-final-legacy2", "pane-final-legacy-independent2"):
    trace = [json.loads(line) for line in (runs / name / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
    assert any(e["kind"] == "unhandled" for e in trace), name
    exit_receipt = read(runs / name / "exit.json")
    assert exit_receipt["retainedHasExited"] and exit_receipt["retainedExitCode"] != 0 and exit_receipt["observedSeconds"] >= 31 and exit_receipt["absent"], name
    assert (runs / name / "remaining-owned-packages.txt").read_text(encoding="utf-8-sig").strip() == "0"
    wer = read(runs / name / "wer-exact.json")
    assert wer["events"] and all(e["exactPathMatch"] and e["exactPidMatch"] for e in wer["events"]), name
for name in ("season-original05", "season-trace05"):
    assert any(not c["vmCorrect"] for r in read(runs / name / "matrix-related.json") for s in r["samples"] for c in s["cards"]), name
pixels = read(runs / "season-final/season-pixels.json")
assert pixels["complete"] and pixels["results"] and all(r["pass"] for r in pixels["results"])
assets = sorted((root / "replay/host/Assets").glob("season-*.png"))
assert len(assets) == 50 and len({digest(p) for p in assets}) == 50
assert all(p.read_bytes()[16:24] == (1600).to_bytes(4, "big") + (900).to_bytes(4, "big") for p in assets)
print(json.dumps({"payloadsVerified": len(manifest), "sameBinaryGroups": groups, "verifiedGreenRuns": green, "pixels": len(pixels["results"])}))
