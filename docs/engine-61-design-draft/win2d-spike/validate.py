"""Checks the recorded negative capability result, not a production acceptance test."""
import json
import sys
from pathlib import Path

p = Path(sys.argv[1])
events = [json.loads(x) for x in (p/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
events.sort(key=lambda x:x['seq'])
assert [e['seq'] for e in events] == list(range(1,len(events)+1))
begins = [e['test'] for e in events if e['name']=='Begin']
assert len(begins)==14 and len(set(begins))==14
assert events[-1]['name']=='Complete'
for name in begins:
    rows=[e for e in events if e['test']==name]
    summary=next(e['data'] for e in rows if e['name']=='Summary')
    assert any(e['name']=='End' for e in rows)
    owner=summary['owner']
    for e in rows:
        if e['name'] in ('ResourceCreated','ResourceLastUse','GameLoopStoppedEnter','GameLoopStoppedReturn'):
            assert e['tid']==owner,(name,e)
        if e['name']=='ResourceDisposed' and e['data']['via']!='HARNESS-RESCUE-UI-NOT-CANDIDATE':
            assert e['tid']==owner,(name,e)
    disposal=[e for e in rows if e['name']=='ResourceDisposed']
    assert len(disposal)==summary['creates'],(name,'experiment resource balance')
    if disposal:
        last=max(e['seq'] for e in rows if e['name']=='ResourceLastUse')
        assert disposal[0]['seq']>last
        assert len(disposal)==1
    if summary['loaded']:
        stop=next(e for e in rows if e['name']=='GameLoopStoppedReturn')
        unload=next(e for e in rows if e['name']=='Unloaded')
        assert stop['seq']<unload['seq'] and stop['tid']!=unload['tid']
    else:
        assert summary['stopped']==0 and summary['creates']==0
    for action in summary['actions']:
        if action['Name']=='post-remove-probe': assert action['status']=='Canceled'
        if action['Name']=='cleanup': assert action['status']==('Completed' if name=='queue/normal' else 'Canceled')
    if name.startswith('stopped/'):
        assert summary['disposals']==summary['creates'] and not summary['targetAlive']
    if '/blocked-' in name:
        enter=next(e for e in rows if e['name']=='BarrierEnter')
        leave=next(e for e in rows if e['name']=='BarrierReturn')
        assert leave['data']['signaled'] and 1000 < leave['ms']-enter['ms'] < 2500
        observe=[e for e in rows if e['name'].startswith('BarrierUiObservation')]
        assert observe[1]['data']>observe[0]['data']
        clear=next(e for e in rows if e['name']=='ChildrenClearReturn')
        assert clear['ms']<leave['ms']
        assert not any(e['name']=='Heartbeat' and clear['ms']+100<e['ms']<leave['ms'] for e in rows)
        assert leave['ms']-clear['ms']>1000
assert not any(e['name'] in ('SuiteFailure','HardProcessTimeout') for e in events)
print(f'PASS: {len(begins)} observed cases; event sequence, real owner IDs, cancellation, resource ledger and UI-stall counterexample verified. This is NOT L1/L3 acceptance.')
