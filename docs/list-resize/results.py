"""Produce a concise result table from the verifier's raw-data comparisons."""
from pathlib import Path
import json
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-resize'
rows=json.loads((a/'comparison.json').read_text())
lines=['# Native result ledger','',
       'Baseline: `red-settled` + `red-extra`. Final: `green-verified` + `green-extra-navigation`. Fixed DPI 1.5, identical 120-step width trajectory and local 1600×900 content. Both 50 and 500 items were tested per family; table shows 500 items. Not a frame-rate or all-page acceptance claim.','',
       '| Template family | Realized before → after | Image measure / arrange before → after | Resize-call p95 ms before → after | UI heartbeat max ms before → after | Final scroll heartbeat max ms |',
       '| --- | --- | --- | --- | --- | --- |']
for x in rows:
    if x['count']!=500:continue
    pair=lambda k: f'{x[k][0]:.1f} → {x[k][1]:.1f}'
    lines.append(f'| {x["family"]} | {x["realized"][0]} → {x["realized"][1]} | {x["measures"][0]} / {x["arranges"][0]} → {x["measures"][1]} / {x["arranges"][1]} | {pair("resizeP95")} | {pair("heartbeatMax")} | {x["greenScrollHeartbeatMax"]:.1f} |')
lines+=['','Image counters are actual Basic/Recommendation MeasureOverride and ArrangeOverride calls, **not all subtree layout calls**. Zero for fixed-size or text-only templates is not zero total layout cost. Basic DrawImage counts are draws, not decoder counts. Recommendation native-in-flight counters retain their original meaning.','',
        'All final functional assertions pass. Continuous forward/reverse scrolling has zero pending visible Basic/Blur images at the 100ms checkpoints in these local fixtures; this does not prove no inter-frame placeholders or slow-network behavior. Repeated draws during recycling still occur and are recorded in the raw rows.','',
        'The unchanged 200ms serious-stall line still fails User/50 and User/500. The verifier intentionally returns 2 after passing functional/evidence checks. The unresolved two-pane diagnostic is excluded and documented in README; it is not counted as passing.','',
        '| Final native stage | Assertions | PID | Post-close observation | Exact-path WER |',
        '| --- | --- | --- | --- | --- |']
for name in ['green-verified','green-extra-navigation']:
    ex=json.loads((a/name/'exit.json').read_text(encoding='utf-8-sig'))
    checks=json.loads((a/name/'matrix-checks.json').read_text())
    lines.append(f'| {name} | {len(checks)} passed | {ex["pid"]} | {ex["observedSeconds"]:.3f}s, absent={ex["absent"]} | empty |')
(r/'docs/list-resize/results.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
