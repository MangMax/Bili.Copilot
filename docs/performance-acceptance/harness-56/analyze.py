"""Validate synthetic control observations. Never converts these to business budgets."""
import collections
import datetime
import hashlib
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
summary = []
for folder in [root / name for name in sys.argv[2:]]:
    rows = [json.loads(line) for line in (folder / 'records.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert not [r for r in rows if r['kind'] in ('failure', 'unhandled', 'log')], folder
    assert [r['data'] for r in rows if r['kind'] == 'step-pass'] == list(range(1, 12))
    samples = [r['data'] for r in rows if r['kind'] == 'sample']
    by = {r['checkpoint']: r for r in samples}
    assert [by[c]['items'] for c in ['A-1', 'A-5', 'A-15-middle', 'A-15-bottom', 'B-5', 'C-60']] == [30, 150, 450, 450, 150, 60]
    expected = {'Recommend': 450, 'Hot': 150, 'Rank': 60}
    loops = [r for r in samples if r['checkpoint'].startswith('loop-')]
    assert len(loops) == 30
    for r in loops:
        assert r['items'] == expected[r['section']]
        assert (r['cacheSlots'], r['uniqueVMs'], r['uniqueIds']) == (660, 660, 640)
        assert r['referenceSlots'] == 660 + r['items']
        assert (r['recommendOffset'], r['hotOffset']) == (15, 5)
        assert r['VerticalOffset'] == 0
        assert r['requests'] == by['A-return']['requests']
        baseline = {'Recommend':'A-15-middle', 'Hot':'B-5', 'Rank':'C-60'}[r['section']]
        assert r['sequenceHash'] == by[baseline]['sequenceHash']
    for r in samples:
        assert r['synthetic'] is True
        assert 0 <= r['visibleCards'] <= r['realizedCards']
        assert r['decodedImages'] == r['imageCount']
        assert r['uniqueIds'] <= r['uniqueVMs'] <= r['referenceSlots']
    assert by['hidden']['visibleCards'] == 0
    assert by['hidden']['hiddenRealizedCards'] == by['hidden']['realizedCards'] > 0
    assert by['shown']['sequenceHash'] == by['A-return']['sequenceHash']
    assert by['refresh-A']['oldVMTracked'] == 450
    assert (by['refresh-A']['items'], by['refresh-A']['cacheSlots'], by['refresh-A']['uniqueVMs'], by['refresh-A']['uniqueIds']) == (30, 210, 240, 220)
    cleanup = json.loads((folder / 'result.json').read_text(encoding='utf-8-sig'))
    assert cleanup['error'] is None and cleanup['processAbsent'] and cleanup['packageAbsent']
    trace = json.loads((folder / 'events.json').read_text(encoding='utf-8-sig'))
    assert trace['lost'] == 0
    gc = [e for e in trace['events'] if e['name'] == 'GC/Start']
    stops = [e for e in trace['events'] if e['name'] == 'GC/Stop']
    assert collections.Counter(e['data']['Count'] for e in gc) == collections.Counter(e['data']['Count'] for e in stops)
    assert all(n == 1 for n in collections.Counter(e['data']['Count'] for e in gc).values())
    sample_rows = [r for r in rows if r['kind'] == 'sample']
    begin = datetime.datetime.fromisoformat(sample_rows[0]['utc'])
    end = datetime.datetime.fromisoformat(sample_rows[-1]['utc'])
    window_gc = [e for e in gc if begin < datetime.datetime.fromisoformat(e['utc']) <= end]
    assert len(window_gc) == samples[-1]['gen0'] - samples[0]['gen0']
    kinds = collections.Counter(e['data']['Reason'] for e in gc)
    tailutc = next(r['utc'] for r in rows if r['kind']=='sample' and r['data']['checkpoint']=='refresh-A')
    endutc = next(r['utc'] for r in rows if r['kind']=='sample' and r['data']['checkpoint']=='observe-10')
    tailgc = [e for e in gc if datetime.datetime.fromisoformat(tailutc) <= datetime.datetime.fromisoformat(e['utc']) <= datetime.datetime.fromisoformat(endutc)]
    # Keep the full original trace hash; compact JSON only keeps GC collection and suspension records.
    selected = ['GC/Start','GC/Stop','GC/SuspendEEStart','GC/SuspendEEStop','GC/RestartEEStart','GC/RestartEEStop','GC/Triggered']
    compact = {'lost':trace['lost'],'events':[e for e in trace['events'] if e['name'] in selected]}
    (folder / 'gc-events.json').write_text(json.dumps(compact,ensure_ascii=False,indent=2),encoding='utf-8')
    summary.append({'run':folder.name,'samples':len(samples),'loops':len(loops),'gcReasons':dict(kinds),'pairedCollections':len(gc),'sampleWindowCollections':len(window_gc),
        'afterRefreshGCReasons':dict(collections.Counter(e['data']['Reason'] for e in tailgc)),
        'checkpoints':[{k:r[k] for k in ['checkpoint','items','cacheSlots','referenceSlots','uniqueVMs','uniqueIds','realizedCards','visibleCards','hiddenRealizedCards','managedBytes','privateBytes','workingSetBytes','oldVMAlive','gen0','gen1','gen2']} for r in samples if not r['checkpoint'].startswith(('before-switch','loop-'))],
        'loopRanges':{k:[min(r[k] for r in loops),max(r[k] for r in loops)] for k in ['realizedCards','cardWeakTracked','cardWeakAlive','managedBytes','privateBytes','workingSetBytes']}})
(root/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'validatedRuns':len(summary),'syntheticOnly':True,'samples':[r['samples'] for r in summary],'gcReasons':[r['gcReasons'] for r in summary]},ensure_ascii=False))
