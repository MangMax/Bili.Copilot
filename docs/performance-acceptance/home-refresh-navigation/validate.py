"""Validate the compact red/green evidence without running or registering an app."""
import hashlib
import json
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

folder = Path(__file__).resolve().parent
manifest = json.loads((folder / "evidence-sha256.json").read_text())
with zipfile.ZipFile(folder / "evidence.zip") as archive:
    assert set(archive.namelist()) == set(manifest)
    for name, digest in manifest.items():
        assert hashlib.sha256(archive.read(name)).hexdigest() == digest, name
    def data(name):
        return json.loads(archive.read(name).decode("utf-8-sig"))
    def counts(name):
        root = ET.fromstring(archive.read("test-results/" + name + ".trx"))
        return root.find("{*}ResultSummary/{*}Counters").attrib
    assert counts("baseline")["passed"] == "572"
    assert counts("regression-red")["failed"] == "3"
    assert counts("related-green")["passed"] == "33"
    assert counts("all-green")["passed"] == "577"
    assert counts("all-green")["failed"] == "0"
    red = {r["name"]: r for r in data("pendingred2/controlled-results.json")}
    for name in ("FirstNavigationRequestsAndDisplays", "ReturnKeepsContent",
                 "PendingPaginationRespectsSettingFalse", "PendingPaginationRespectsSettingTrue"):
        assert red[name]["status"] == "FAIL", name
    for run, total in (("green", 24), ("aotgreen", 27)):
        rows = data(run + "/controlled-results.json")
        assert len(rows) == total and all(r["status"] == "PASS" for r in rows)
    for run in ("pendingred2", "green", "aotgreen"):
        obs = data(run + "/exit-observation.json")
        assert obs["exitCode"] == 0 and obs["terminated"] and not obs["forced"]
        assert obs["handleRetained"] and obs["queryComplete"] and obs["observationComplete"]
        assert obs["queries"][-1]["startedSecondsAfterExit"] >= 31
        assert not data(run + "/event-xml.json")
        cleanup = data(run + "/cleanup.json")
        assert cleanup["processAbsent"] and cleanup["registrationAbsent"]
    assert all(x["equal"] for x in data("final-production-inputs.json"))
    native = data("native-pe.json")
    assert native["clrRva"] == 0 and native["clrSize"] == 0
    executed = [x for x in data("aotgreen/loaded-modules.json") if x["path"].endswith("BiliCopilot.UI.exe")]
    assert len(executed) == 1 and executed[0]["sha256"].lower() == native["sha256"]
    assert all(data("protection-check.json").values())
print("PASS: hashes, red/green, 577 tests, native AOT, bounded exits, protected state")
