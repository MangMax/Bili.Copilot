import json, sys
from pathlib import Path
for stage in sys.argv[1:]:
 for path in Path(stage).glob('matrix-continuity-*.json'):
  d=json.loads(path.read_text(encoding='utf-8-sig'))
  s=[s for s in d['samples'] if s['trigger']=='rendering']
  bad=[x for x in s if x['coverage']<.5]
  missing=[x for x in s if any(not r['vmCorrect'] or not r['images'] or any(i['source'] is None or not i.get('sourceCorrect',True) for i in r['images']) for r in x['rows'])]
  pending=[x for x in s if any(not i['ready'] for r in x['rows'] for i in r['images'])]
  def duration(predicate):
   return sum(b['ms']-a['ms'] for a,b in zip(s,s[1:]) if predicate(a))
  result=dict(stage=Path(stage).name,family=d['family'],count=d['count'],cache=d['cache'],frames=len(s),minCoverage=min(x['coverage'] for x in s),lowCoverageFrames=len(bad),lowCoverageMs=duration(lambda x:x['coverage']<.5),bindingBadFrames=len(missing),pendingFrames=len(pending),pendingMs=duration(lambda x:any(not i['ready'] for r in x['rows'] for i in r['images'])),maxRealized=max(x['realized'] for x in s),prepared=max(x['prepared'] for x in s),resizeMax=max(d['beats']),privateMB=d['privateBytes']/1048576,workingMB=d['workingSet']/1048576)
  # Classify observed direction, not the impulse sign: inertia can reverse later.
  result['reversePrepared']=sum(b['prepared']-a['prepared'] for a,b in zip(s,s[1:]) if b['offset']<a['offset'])
  result['reverseDraws']=sum(b['draws']-a['draws'] for a,b in zip(s,s[1:]) if b['offset']<a['offset'])
  result['maxRenderingGap']=max(b['ms']-a['ms'] for a,b in zip(s,s[1:]))
  result['motionMs']=d['samples'][-1]['ms']
  result['maxOffsetViewports']=max(x['offset'] for x in s)/s[0]['height']
  video=Path(stage)/'video-pixels.json'
  if video.exists():
   v=[x for x in json.loads(video.read_text(encoding='utf-8-sig')) if x['count']==d['count']]
   intervals=[(x,(v[i+1]['ms']-x['ms']) if i+1<len(v) else max(0,d['samples'][-1]['ms']-x['ms'])) for i,x in enumerate(v)]
   current=longest=0
   for x,dt in intervals:
    current=current+dt if x['background']>.9 else 0
    longest=max(longest,current)
   result.update(pixelSamples=len(v),blankMs=sum(dt for x,dt in intervals if x['background']>.9),blankMaxMs=longest,blankFraction=sum(x['background']>.9 for x in v)/len(v))
  print(json.dumps(result))
