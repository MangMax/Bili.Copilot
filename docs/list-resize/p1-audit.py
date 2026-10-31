"""Keep all runs (including reds) visible; never relax the 200 ms gate."""
from pathlib import Path
import json

r = Path(__file__).resolve().parents[2] / 'artifacts/list-p1'
for stage in sorted(r.iterdir()):
    batch=stage/'matrix-format-results.json'
    if batch.exists():
        for format in json.loads(batch.read_text(encoding='utf-8-sig')):
            print(stage.name,format['format'],'decoded',format['Decoded'],'failed',format['Failed'],'dimensions',format['dimensions'])
            for row in format['rows']:
                assert abs(max(row['beats'])-row['heartbeatMax'])<.001
                print(dict(count=row['count'],cache=row['cache'],heartbeat=max(row['beats']),serious_stall=max(row['beats'])>200))
    results = stage / 'matrix-results.json'
    if not results.exists():
        continue
    print(stage.name)
    for row in json.loads(results.read_text(encoding='utf-8-sig')):
        beats = row['beats']
        assert abs(max(beats) - row['heartbeatMax']) < .001
        assert len(row['resize']) == 120
        assert len(row['scrolling']) == 27
        assert row['dpi'] == 1.5
        assert row['cache'] in (0.5, 1, 2, 20)
        events = stage / f"matrix-events-{row['family']}-{row['count']}.json"
        draw_max = None
        if events.exists():
            ev = json.loads(events.read_text())
            starts = [e['ms'] for e in ev if e['kind'] == 'draw-start']
            ends = [e['ms'] for e in ev if e['kind'] == 'draw-end']
            assert len(starts) == len(ends) == row['draws']
            draw_max = max((b-a for a,b in zip(starts,ends)), default=0)
        print(dict(family=row['family'], count=row['count'], cache=row['cache'],
                   realized=row['realized'], heartbeat=max(beats),
                   serious_stall=max(beats)>200, draws=row['draws'], draw_max=draw_max))
    checks = json.loads((stage/'matrix-checks.json').read_text(encoding='utf-8-sig')) if (stage/'matrix-checks.json').exists() else []
    print('failures', [c for c in checks if c.endswith('/FAIL')])
    if (stage/'exit.json').exists():
        ex = json.loads((stage/'exit.json').read_text(encoding='utf-8-sig'))
        assert ex['absent'] and ex['observedSeconds'] >= 31
        assert not json.loads((stage/'wer.json').read_text(encoding='utf-8-sig'))
    else:
        print('EXIT OBSERVATION INCOMPLETE')
