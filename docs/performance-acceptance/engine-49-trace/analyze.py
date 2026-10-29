"""Summarize complete rings, retaining selected original records, never infer GPU completion."""
from pathlib import Path
import collections
import hashlib
import json

base = Path('artifacts/engine49-trace')
dest = Path('docs/performance-acceptance/engine-49-trace/evidence')
dest.mkdir(exist_ok=True)
reports = []
for folder, filename in [('run', 'trace-4.json'), ('replay-original', 'trace-1.json')]:
    raw = base / folder / filename
    trace = json.loads(raw.read_text())
    rings = trace['data']['rings']
    events = sorted((e for ring in rings for e in ring['entries']), key=lambda e: e['sequence'])
    assert all(r['written'] < r['capacity'] for r in rings), 'Ring overwrite: cannot prove absence'
    assert [e['sequence'] for e in events] == list(range(1, len(events) + 1)), 'Incomplete sequence'
    selected = {e['sequence']: e for e in events if e['data']['action'] in ['Add', 'Remove', 'PauseEnter', 'PauseFlagWritten', 'PauseExit', 'ClearEnter', 'ClearExit', 'CloseEnter', 'CloseExit', 'Exception', 'DeviceLost']}
    windows = []
    for pause in (e for e in events if e['data']['action'] == 'PauseExit'):
        instance = pause['data']['instance']
        after = [e for e in events if e['sequence'] > pause['sequence'] and e['data']['instance'] == instance]
        end = next((e for e in after if e['data']['action'] in ['StartEnter', 'ClearEnter', 'CloseEnter']), None)
        after = [e for e in after if end is None or e['sequence'] < end['sequence']]
        per_item = []
        for item in sorted({e['data']['itemId'] for e in after if e['data']['action'] == 'DrawQueued'}):
            draws = [e for e in after if e['data']['action'] == 'DrawQueued' and e['data']['itemId'] == item]
            first, last = draws[0], draws[-1]
            batch = next((e for e in after if e['sequence'] > last['sequence'] and e['data']['action'] == 'BatchDisposed' and e['threadId'] == last['threadId']), None)
            for e in (first, last, batch):
                if e: selected[e['sequence']] = e
            per_item.append(dict(itemId=item, text=first['data']['text'], mode=first['data']['mode'], draws=len(draws),
                                 xRange=[min(e['data']['x'] for e in draws), max(e['data']['x'] for e in draws)],
                                 targets=sorted({e['data']['target'] for e in draws}), firstDraw=first['sequence'], lastDraw=last['sequence'], batchAfterLast=batch['sequence'] if batch else None))
        windows.append(dict(pauseExit=pause['sequence'], end=end['sequence'] if end else None,
                            seconds=(after[-1]['ticks']-pause['ticks'])/trace['data']['frequency'] if after else 0,
                            counts=dict(collections.Counter(e['data']['action'] for e in after)), items=per_item))
    captures = json.loads((base / folder / 'captures.json').read_text(encoding='utf-8-sig'))
    for capture in captures:
        png = base / folder / (capture['name'] + '.png')
        capture.update(runId=trace['runId'], dllHash=trace['dllHash'], sha256=hashlib.sha256(png.read_bytes()).hexdigest())
    evidence = dict(runId=trace['runId'], pid=trace['pid'], dllHash=trace['dllHash'], rawPath=str(raw), rawSha256=hashlib.sha256(raw.read_bytes()).hexdigest(),
                    ringCounts=[r['written'] for r in rings], completeSequence=True, eventCount=len(events),
                    counts=dict(collections.Counter(e['data']['action'] for e in events)), pauses=windows, captures=captures)
    (dest / (folder + '-summary.json')).write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    (dest / (folder + '-records.json')).write_text(json.dumps(sorted(selected.values(), key=lambda e: e['sequence']), indent=2), encoding='utf-8')
    filtered = [json.loads(line) for line in (base / folder / 'events.jsonl').read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    filtered = [e for e in filtered if e['pid'] == trace['pid'] and e['runId'] == trace['runId']]
    (dest / (folder + '-ui.jsonl')).write_text('\n'.join(json.dumps(e) for e in filtered) + '\n', encoding='utf-8')
    reports.append(evidence)
print(json.dumps([dict(runId=r['runId'], pid=r['pid'], events=r['eventCount'], pauses=r['pauses']) for r in reports], indent=2))
