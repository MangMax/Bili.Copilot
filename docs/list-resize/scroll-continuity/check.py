"""Audit raw identity, recording coverage and owned process receipts; no VM-null filter."""
import json, sys
from pathlib import Path
from datetime import datetime
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
for arg in sys.argv[1:]:
 stage=Path(arg);checks=[]
 def check(name, ok, detail=None):checks.append(dict(name=name,passed=bool(ok),detail=detail))
 check('BothFixedDatasetSizesPresent',all((stage/f'matrix-continuity-{n}.json').exists() for n in (50,500)))
 for p in stage.glob('matrix-continuity-*.json'):
  d=read(p);rows=[r for s in d['samples'] for r in s['rows']]
  bad=[r for r in rows if not r['vmCorrect'] or r['vmNull'] or not r['images'] or any(i['source'] is None or not i.get('sourceCorrect',True) for i in r['images'])]
  if d['family']=='User':bad += [r for r in rows if any(not i['source'].endswith(f"/user-{r['index']}.png") for i in r['images'] if i['source'])]
  check(f'AllObservedVisibleIdentitiesIncludingNull_{d["count"]}',not bad,{'rows':len(rows),'bad':len(bad)})
  check(f'FixedDatasetTrajectory_{d["count"]}',len(d['trajectory'])==(32 if d.get('returnOnly') else 64))
  if d.get('returnOnly'):
   offsets=[s['offset'] for s in d['samples']];height=d['samples'][0]['height']
   check(f'ReturnMotionInsideAlreadyWarmedRegion_{d["count"]}',height<max(offsets)<=4.5*height+1 and any(b<a for a,b in zip(offsets,offsets[1:])),{'maxOffset':max(offsets),'viewport':height})
  check(f'VirtualizationStillBounded_{d["count"]}',d['count']!=500 or max(s['realized'] for s in d['samples'])<100)
  if (stage/'video-pixels.json').exists():
   recording=read(stage/'continuous.frames/manifest.json')
   start=(datetime.fromisoformat(d['startedUtc'])-datetime.fromisoformat(recording['startedUtc'])).total_seconds()*1000
   end=start+d['samples'][-1]['ms']
   check(f'ExternalRecordingCoversEntireMeasuredMotion_{d["count"]}',start>=0 and end<=recording['timing']['elapsedMs'],{'start':start,'end':end,'recorded':recording['timing']['elapsedMs']})
   v=[x for x in read(stage/'video-pixels.json') if x['count']==d['count']]
   check(f'ExternalPixelsPresent_{d["count"]}',len(v)>30 and all((stage/'pixel-grids'/x['raw']['grid']).is_file() for x in v))
   calculated={};matches=True
   for x in v:
    name=x['raw']['grid']
    if name not in calculated:
     data=(stage/'pixel-grids'/name).read_bytes()
     calculated[name]=sum(r<6 and g<6 and b<6 for r,g,b in zip(data[0::3],data[1::3],data[2::3]))/(len(data)/3)
    matches &= abs(calculated[name]-x['background'])<1e-12
   check(f'AllExternalPixelGridsMatchClassifier_{d["count"]}',matches)
 identity=read(stage/'identity.json');ex=read(stage/'exit.json')
 check('RetainedNormalExit',ex['pid']==identity['pid'] and ex['path']==identity['path'] and ex['absent'] and ex.get('retainedHasExited') and ex.get('retainedExitCode')==0 and ex['observedSeconds']>=31)
 check('ExactPidPathWER', (stage/'wer-exact.json').exists() and not read(stage/'wer-exact.json')['events'])
 check('OwnedPackageRemoved',(stage/'remaining-owned-packages.txt').read_text(encoding='utf-8-sig').strip()=='0')
 check('YieldReceipt',bool(read(stage/'yield.json')))
 check('CompletedWithoutException',(stage/'matrix-finished.txt').exists() and not (stage/'matrix-error.txt').exists())
 result=dict(stage=stage.name,checks=checks,passed=all(c['passed'] for c in checks))
 (stage/'continuity-checks.json').write_text(json.dumps(result,indent=2))
 print(json.dumps(result))
 if not result['passed']:sys.exit(2)
