"""Summarize observed events, never turn absent callbacks into successful retirement."""
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
events = [json.loads(line) for line in (root / 'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
events.sort(key=lambda e: e['seq'])
assert [e['seq'] for e in events] == list(range(1, len(events) + 1)), 'Missing/duplicated events'
cases = []
for name in dict.fromkeys(e['test'] for e in events if e['name'] == 'Begin'):
    rows = [e for e in events if e['test'] == name]
    def find(event):
        return next((e for e in rows if e['name'] == event), None)
    def elapsed(begin, end):
        a, b = find(begin), find(end)
        return round(b['ms'] - a['ms'], 3) if a and b else None
    beat = [e['ms'] for e in rows if e['name'] == 'Heartbeat']
    barrier, release = find('BarrierEnter'), find('BarrierReturn')
    start, end = find('BarrierUiObservationStart'), find('BarrierUiObservationEnd')
    cases.append(dict(
        case=name, complete=find('End') is not None,
        closeMs=elapsed('CloseEnter', 'CloseReturn'), clearMs=elapsed('ChildrenClearEnter', 'ChildrenClearReturn'),
        removeMs=elapsed('RemoveEnter', 'RemoveReturn'), barrierMs=elapsed('BarrierEnter', 'BarrierReturn'),
        maxHeartbeatGapMs=round(max((b-a for a,b in zip(beat,beat[1:])), default=0),3),
        observationBeats=end['data'] - start['data'] if start and end else None,
        summary=find('Summary')['data'] if find('Summary') else None
    ))
result = dict(events=len(events), sequenceComplete=True, suiteComplete=any(e['name']=='Complete' for e in events), cases=cases)
(root / 'summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
for c in cases:
    print(c['case'], 'close',c['closeMs'],'clear',c['clearMs'],'remove',c['removeMs'],'barrier',c['barrierMs'],'gap',c['maxHeartbeatGapMs'],'beats',c['observationBeats'], 'summary', c['summary'])
