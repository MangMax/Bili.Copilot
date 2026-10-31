"""Recompute all timing statistics and validate complete red/green native evidence."""
import json,math,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]/'artifacts/list-resize'
def read(stage,name): return json.loads((root/stage/name).read_text(encoding='utf-8-sig'))
def validate(stage,expected,green):
    rows=read(stage,'matrix-results.json');assert len(rows)==expected,(stage,len(rows))
    checks=read(stage,'matrix-checks.json');assert checks,(stage,'empty checks')
    failures=[s for s in checks if s.endswith('/FAIL')]
    if green: assert not failures,(stage,failures)
    exit=read(stage,'exit.json');assert exit['absent'] and exit['observedSeconds']>=31,exit
    assert not read(stage,'wer.json'),(stage,'WER')
    for row in rows:
        assert row['dpi']==1.5 and len(row['resize'])==120,(stage,row['family'])
        for source,key,percentile in [('beats','heartbeatMax',False),('beats','heartbeatP95',True),('resize','resizeMax',False),('resize','resizeP95',True)]:
            values=sorted(row[source]);value=values[int((len(values)-1)*.95)] if percentile else max(values)
            assert math.isclose(value,row[key],abs_tol=.00001),(stage,key)
        assert row['cache']==(2 if green else 20)
        assert len(row['scrolling'])==27
        if green: assert row['missing']==0
    return rows,failures
def main():
    stages=sys.argv[1:] or ['red-settled','red-extra','green-verified','green-extra-navigation']
    assert len(stages)==4,'supply red-main red-extra green-main green-extra'
    data=[]
    for stage,n,green in zip(stages,[16,6,16,6],[False,False,True,True]):
        rows,failures=validate(stage,n,green);data.append(rows)
        print(stage,'rows',len(rows),'functional failures',len(failures))
    red={(r['family'],r['count']):r for r in data[0]+data[1]}
    green={(r['family'],r['count']):r for r in data[2]+data[3]}
    assert red.keys()==green.keys()
    comparisons=[]
    for key,r in red.items():
        g=green[key]
        # Cache and layout work are deterministic corroboration of noisy wall-clock measurements.
        if key[1]==500:
            assert g['realized']<r['realized'],(key,'no reduction in offscreen work')
            assert g['resizeP95']<r['resizeP95'],(key,'no resize p95 improvement')
        comparisons.append({'family':key[0],'count':key[1],'realized':[r['realized'],g['realized']],
                            'measures':[r['measures'],g['measures']],'arranges':[r['arranges'],g['arranges']],
                            'heartbeatMax':[r['heartbeatMax'],g['heartbeatMax']],
                            'resizeP95':[r['resizeP95'],g['resizeP95']],
                            'draws':[r['draws'],g['draws']],
                            'greenScrollHeartbeatMax':g['scrollHeartbeatMax'],
                            'greenContinuousPendingCheckpoints':sum(x['pendingVisibleImages']>0 for x in g['scrolling'][3:])})
    covers=read(stages[3],'matrix-cover-checks.json');assert len(covers)==4 and all(x['pass'] for x in covers),covers
    (root/'comparison.json').write_text(json.dumps(comparisons,indent=2),encoding='utf-8')
    residual=[{'family':r['family'],'count':r['count'],'heartbeatMax':r['heartbeatMax']} for r in green.values() if r['heartbeatMax']>=200]
    (root/'residual-stalls.json').write_text(json.dumps(residual,indent=2),encoding='utf-8')
    print('PASS: 22 matched scenarios, all green functional checks, native image semantics, exit/WER and recomputed timings')
    if residual:
        print('NOT ALL-PASS at the unchanged 200ms severe-stall line:',json.dumps(residual))
        return 2
    return 0
if __name__=='__main__':sys.exit(main())
