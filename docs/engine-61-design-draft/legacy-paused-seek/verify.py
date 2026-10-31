"""Verify retained evidence; no UI or network operations."""
import hashlib
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
summary = read(HERE / "summary.json")
for name in ["source-red", "final"]:
    folder = HERE / "evidence" / name
    events = [json.loads(line) for line in (folder / "events.jsonl").read_text(encoding="utf-8-sig").splitlines()]
    cases = [e for e in events if isinstance(e["data"], dict) and "passed" in e["data"]]
    assert len(cases) == 12 and len({e["name"] for e in cases}) == 12
    assert any(e["name"] == "SuiteCompleted" for e in events)
    assert (folder / "Program.cs").read_bytes() == (HERE / "Probe.cs").read_bytes()
    assert cases == summary[name]["cases"]
    assert sum(e["data"]["passed"] for e in cases) == (12 if name == "final" else 4)
    exit_record = read(folder / "retainedexit.json")
    assert exit_record["exitCode"] == 0 and exit_record["retainedHandle"] and not exit_record["forced"]
    wer = read(folder / "wer.json")
    assert wer["applicationLogReadable"] and wer["secondsAfterExit"] >= 31 and not wer["matches"]
    cleanup = read(folder / "cleanup.json")
    assert cleanup["packagesRemaining"] == 0 and not cleanup["processRemaining"]
    for phase in ["BACKWARD", "REPEATED"]:
        review = read(folder / (phase + ".review.json"))
        assert review["sha256"].lower() == sha(folder / (phase + ".png"))
        assert review["verdict"] == ("PASS" if name == "final" else "FAIL")
for name in ["compat-source", "compat-package"]:
    cases = read(HERE / name / "results.json")
    assert len(cases) == 23 and all(c["passed"] for c in cases)
    assert (HERE / name / "public-api.txt").read_bytes() == (HERE / "public-api-baseline.txt").read_bytes()
for path, hashes in read(HERE / "changes.json").items():
    assert sha(HERE / "source" / pathlib.Path(path).name) == hashes["after"]
for path, expected in read(HERE / "sha256.json").items():
    assert sha(HERE / path) == expected, path
print("Retained native red 4/12, green 12/12; compatibility 23/23; source/API/hashes/exit/visual records verified.")
