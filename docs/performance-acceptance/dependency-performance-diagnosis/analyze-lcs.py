import json, pathlib, statistics, sys
root=pathlib.Path(sys.argv[1]); runs=[]; table=[]
for p in sorted(root.glob('[1-6]-*.json')):
    d=json.loads(p.read_text()); assert len(d['tests'])==15 and len(d['rows'])==180
    assert all(t['status']=='PASS' for t in d['tests'])
    runs.append(d)
for n in (256,1024,2048):
    for kind in ('replacement','rotation','reversal','duplicate-reversal','same-references'):
        result=dict(n=n,kind=kind,variants={})
        for v in ('Rectangular','Jagged','Shortcut'):
            samples=[[r for r in d['rows'] if r['n']==n and r['kind']==kind] for d in runs if d['variant']==v]
            assert len(samples)==2 and all(len(s)==12 for s in samples)
            result['variants'][v]=dict(processMediansMs=[statistics.median(r['milliseconds'] for r in s) for s in samples],
                allocatedBytes=sorted(set(r['allocatedBytes'] for s in samples for r in s)),
                middle=sorted(set(r['middle'] for s in samples for r in s)))
        assert result['variants']['Jagged']['allocatedBytes']==result['variants']['Shortcut']['allocatedBytes']
        table.append(result)
print(json.dumps(table,indent=2))
