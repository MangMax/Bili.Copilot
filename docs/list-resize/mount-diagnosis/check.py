"""Check raw mount/scroll evidence. Phase trace files are expected, exceptions are not."""
import json
import sys
from pathlib import Path

stage = Path(sys.argv[1])
def read(name):
    return json.loads((stage / name).read_text(encoding="utf-8-sig"))

checks = []
def check(name, value):
    checks.append({"name": name, "pass": bool(value)})

trace = [json.loads(line) for line in (stage / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()] if (stage / "trace.jsonl").exists() else []
check("NoUnhandledOrCOMException", not any(e["kind"] in ("unhandled", "first-chance") for e in trace))
check("NativeRunCompleted", (stage / "matrix-finished.txt").exists() and not (stage / "matrix-error.txt").exists())
phases = [e["data"] for e in trace if e["kind"] == "mount-phase"]
check("MountLoadedMeasureScrollStagesRecorded", all(any(isinstance(p, str) and word in p.lower() for p in phases) for word in ("mount", "loaded", "measure", "scroll-start")))
if (stage / "matrix-related.json").exists():
    for run in read("matrix-related.json"):
        samples = run["samples"]
        check("SeasonRapidTrajectoryComplete", len(samples) >= 31)
        check("EveryVisibleSeasonCardMatchesCurrentItem", all(s["cards"] and all(all(c[k] for k in ("vmCorrect", "contextCorrect", "contentCorrect", "commandCorrect", "textCorrect", "imageSourceCorrect")) for c in s["cards"]) for s in samples))
        check("SeasonItemsSourceMatchesCurrentItem", all(c.get("sourceCorrect", True) for s in samples for c in s["cards"]))
        check("SeasonSettledImagesReady", samples[-1]["cards"] and all(c["imagesReady"] for c in samples[-1]["cards"]))
if (stage / "matrix-panes.json").exists():
    r = read("matrix-panes.json"); samples = {s["phase"]: s for s in r["samples"]}
    dual = not r["mode"].endswith("Single")
    def unchanged(a, b):
        return abs(a["VerticalOffset"] - b["VerticalOffset"]) < 1 and a["realized"] == b["realized"]
    check("PaneMountAndLeftEnd", "mounted" in samples and "left-end" in samples and 499 in samples["left-end"]["left"]["realized"])
    check("PaneRequestedCache", all(s[p]["VerticalCacheLength"] == r.get("cache", .5) for s in r["samples"] for p in (("left", "right") if dual else ("left",))))
    if dual and all(n in samples for n in ("mounted", "left-end", "right-end", "left-start-right-end")):
        check("LeftScrollPreservesRightOffsetAndRange", unchanged(samples["mounted"]["right"], samples["left-end"]["right"]))
        check("RightScrollPreservesLeftOffsetAndRange", unchanged(samples["left-end"]["left"], samples["right-end"]["left"]))
        check("OppositePaneRangesIndependent", 0 in samples["left-start-right-end"]["left"]["realized"] and 499 in samples["left-start-right-end"]["right"]["realized"] and unchanged(samples["right-end"]["right"], samples["left-start-right-end"]["right"]))
    elif dual:
        check("DualPaneTrajectoryComplete", False)
    check("RequestedLayoutSharing", r["shared"] == (not r["mode"].endswith("Independent")))
if (stage / "matrix-season-resize.json").exists():
    r = read("matrix-season-resize.json")
    check("SeasonResize120Steps200msGate", len(r["widths"]) == 120 and r["beats"] and max(r["beats"]) <= 200)
    check("SeasonResizeCache05", r["cache"] == .5)
    check("SeasonBindingsSampledThroughoutResize", len([s for run in read("matrix-related.json") for s in run["samples"] if s["phase"] == "resize"]) == 120)
check("FunctionalEvidencePresent", any((stage / n).exists() for n in ("matrix-related.json", "matrix-panes.json")))
required = ("exit.json", "identity.json", "wer.json", "remaining-owned-packages.txt", "yield.json")
check("OwnedExitReceiptsPresent", all((stage / n).exists() for n in required))
if all((stage / n).exists() for n in required):
    e = read("exit.json"); i = read("identity.json")
    check("OwnedRetainedExitAndWER", e["pid"] == i["pid"] and e["path"] == i["path"] and e["absent"] and e["observedSeconds"] >= 31 and e["retainedHasExited"] and e["retainedExitCode"] == 0 and not read("wer.json"))
    check("OwnedCleanupAndYield", (stage / "remaining-owned-packages.txt").read_text(encoding="utf-8-sig").strip() == "0" and "released" in read("yield.json"))
result = {"stage": stage.name, "checks": checks, "passed": sum(c["pass"] for c in checks), "failed": sum(not c["pass"] for c in checks)}
(stage / "mount-checks.json").write_text(json.dumps(result, indent=2))
print(json.dumps({k: v for k, v in result.items() if k != "checks"}))
sys.exit(2 if result["failed"] else 0)
