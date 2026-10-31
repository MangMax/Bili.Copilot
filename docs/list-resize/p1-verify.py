"""Final acceptance only; earlier adverse attempts remain in p1-audit output."""
from pathlib import Path
import json, hashlib
r=Path(__file__).resolve().parents[2]/'artifacts/list-p1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def exit_check(stage):
    d=r/stage;ex=read(d/'exit.json')
    assert ex['absent'] and ex['observedSeconds']>=31,(stage,ex)
    assert not read(d/'wer.json'),stage
    assert (d/'owned.png').stat().st_size>0,stage
    assert (d/'yield.json').exists(),stage
    assert not (d/'matrix-error.txt').exists(),stage
    if (d/'source-hashes.json').exists():
        for entry in read(d/'source-hashes.json'):
            p=d/'sources'/Path(entry['Path']).name
            assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==entry['Hash'],p
    return d
rows=[]
for stage,expected in [('half-main',16),('half-extra',6)]:
    d=exit_check(stage);data=read(d/'matrix-results.json');assert len(data)==expected
    checks=read(d/'matrix-checks.json');assert checks and not any(c.endswith('/FAIL') for c in checks),stage
    for x in data:
        assert x['cache']==0.5 and x['dpi']==1.5
        assert len(x['resize'])==120 and len(x['scrolling'])==27
        assert max(x['beats'])<=200,(stage,x['family'],x['count'],max(x['beats']))
        assert x['missing']==0
        assert all(s['visible']>0 and s['pendingVisibleImages']==0 for s in x['scrolling'])
    rows.extend(data)
    print(stage,len(checks),'functional checks passed')
covers=read(r/'half-extra/matrix-cover-checks.json')
assert len(covers)==4 and all(c['pass'] for c in covers),covers
print('4 native image semantics checks passed')
print('22 scenarios pass the unchanged 200ms resize heartbeat gate')
d=exit_check('half-user-png')
png=read(d/'matrix-results.json')
assert {x['count'] for x in png}=={50,500}
assert not any(c.endswith('/FAIL') for c in read(d/'matrix-checks.json'))
for x in png:
    assert x['family']=='User' and x['cache']==0.5 and max(x['beats'])<=200
    assert len(x['resize'])==120 and len(x['scrolling'])==27 and x['missing']==0
    assert all(s['visible']>0 and s['pendingVisibleImages']==0 for s in x['scrolling'])
print('Original large PNG User50/500 paired supplement passed')
d=exit_check('half-formats')
assert not any(c.endswith('/FAIL') for c in read(d/'matrix-checks.json'))
formats=read(d/'matrix-format-results.json')
assert {f['format'] for f in formats}=={'jpg','webp','avif'}
for f in formats:
    stage=f['format'];data=f['rows']
    assert f['Failed']==0 and f['Decoded']>0 and f['dimensions']==['96x96']
    assert {x['count'] for x in data}=={50,500}
    assert not any(c.endswith('/FAIL') for c in read(d/'matrix-checks.json')),stage
    for x in data:
        assert x['cache']==0.5 and x['dpi']==1.5 and max(x['beats'])<=200
        assert len(x['resize'])==120 and len(x['scrolling'])==27
        assert all(s['visible']>0 and s['pendingVisibleImages']==0 for s in x['scrolling'])
    print(stage,'real-size avatar supplement passed')
d=exit_check('half-extra')
assert all(c['pass'] for c in read(d/'matrix-cover-checks.json'))
d=exit_check('half-undo')
assert not (d/'matrix-resize-error.txt').exists()
undo=read(d/'matrix-resize-results.json')
assert len(undo)==3 and all(x['pass'] and x['count']==x['startCount'] and x['listUpdates']==0 and x['caches'] and all(c==0.5 for c in x['caches']) for x in undo)
required={'UndoExactReferenceSequence','UndoExactViewportAnchor','FinalPhysicalPixelDecode','PhysicalCoverageWithoutUpsampling','SourceClearedDuringResizeRejectsOldResult','UnloadCancelsPendingResize','RemountLoadsCurrentSize'}
assert set(read(d/'matrix-resize-checks.json'))==required
print('Actual Popular shared cache, 3 resize scenarios and 7 exact undo/image checks passed')
print('Two-pane diagnosis is explicitly separate; this verifier does not sign it green.')
