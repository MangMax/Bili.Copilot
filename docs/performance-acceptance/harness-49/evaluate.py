"""Evaluate recorded actual-control snapshots; this is not a renderer simulation.
Exit 1 means acceptance failures, even if collection scripts exited 0.
"""
import json, sys
from pathlib import Path
root=Path(__file__).resolve().parent.parent/'evidence-49-ui'
rows=[json.loads(x) for x in (root/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
last=max(i for i,r in enumerate(rows) if r['kind']=='launch')
s=[r for r in rows[last:] if r['kind']=='snapshot']
assert len(s)==80, f'Incomplete final run: {len(s)} snapshots'
results=[]
def test(name,indices,predicate,scope='structured state only'):
    selected=[s[i-1] for i in indices]
    ok=all(predicate(r['data']) for r in selected)
    results.append(dict(name=name,status='PASS' if ok else 'FAIL',snapshots=indices,utc=[r['utc'] for r in selected],scope=scope))
def render(d):return (d.get('engineTree') or {}).get('_render') or {}
for engine,offset in [('DirectX',0),('Win2D',25)]:
    ix=lambda *a:[i+offset for i in a]
    test(engine+'_ReadyExclusiveEngine',ix(1),lambda d:d['visuals']==1 and (d['dx'] != d['legacy']))
    test(engine+'_BurstLatestState',ix(2),lambda d:d['visuals']==1 and len(d['items'])==5 and all(i['Text'].startswith('LATEST') for i in d['items']))
    test(engine+'_ReadyBatchNoFixed500ms',ix(2),lambda d:d['elapsed']<500, 'One command-to-low-dispatch-fence observation, not frame latency or coalescing count')
    test(engine+'_ContentLatestState',ix(3),lambda d:len(d['items'])==5 and all(i['Text'].startswith('REPLACEMENT') for i in d['items']))
    test(engine+'_StyleState',ix(4),lambda d:(render(d).get('_configState') or render(d)).get('_fontSizeScale',render(d).get('_danmakuFontSizeScale'))==2 and (render(d).get('_configState') or render(d)).get('_textOpacity')==1)
    test(engine+'_OrdinaryPauseState',ix(6,7),lambda d:d['paused'] and render(d).get('_isPaused') is True)
    test(engine+'_PausedSeekPreservesPause',ix(8,9,10),lambda d:d['paused'] and render(d).get('_isPaused') is True)
    test(engine+'_PausedRedrawState',ix(11),lambda d:d['paused'] and render(d).get('_isPaused') is True)
    test(engine+'_PlayingSeekPosition',ix(13),lambda d:d['position']==20 and d['lastProgress']==20 and not d['paused'])
    test(engine+'_HideShowBinding',ix(14),lambda d:not d['show'] and d['visibility']=='Collapsed')
    test(engine+'_ShowBinding',ix(15),lambda d:d['show'] and d['visibility']=='Visible')
    test(engine+'_ClearQueuedRedraw',ix(16),lambda d:d['items']==[])
    test(engine+'_ReinitializeContent',ix(17),lambda d:len(d['items'])==5 and all(i['Text'].startswith('REINIT') for i in d['items']))
    test(engine+'_ReplaceVMRejectsOldEvents',ix(18),lambda d:d['position']==2 and d['lastProgress']==2 and not d['paused'] and all(i['Text'].startswith('NEWVM') for i in d['items']))
    test(engine+'_QueuedUnloadReleasesVisuals',ix(19,20),lambda d:not d['loaded'] and d['visuals']==0 and not d['dx'] and not d['legacy'])
    test(engine+'_RepeatedRemountRestoresRenderer',ix(21,23,25),lambda d:d['loaded'] and d['visuals']==1 and (d['dx'] != d['legacy']))
test('DirectXToWin2D_PausedSwitchRestoresPause',[57,58],lambda d:d['paused'] and render(d).get('_isPaused') is True)
test('Win2DToDirectX_PausedSwitchRestoresPause',[72,73],lambda d:d['paused'] and render(d).get('_isPaused') is True)
test('BothEngines_RemountPublicRedrawRestoresRenderer',[65,80],lambda d:d['loaded'] and d['visuals']==1)
results.append(dict(name='NoUnhandledOrCaughtCallbackExceptionObserved',status='FAIL' if any(r['kind'] in ['ERROR','UNHANDLED'] for r in rows) else 'PASS',scope='Only instrumented app exception events; not exhaustive native callback instrumentation'))
(root/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
for r in results:print(r['status'],r['name'])
print('PASS',sum(r['status']=='PASS' for r in results),'FAIL',sum(r['status']=='FAIL' for r in results))
sys.exit(1 if any(r['status']=='FAIL' for r in results) else 0)
