from pathlib import Path
import json,datetime
from assertions import positions
F=Path('artifacts/combined-60cd/full');events=[json.loads(s) for s in (F/'events.jsonl').read_text().splitlines()]
stamp=lambda e:datetime.datetime.fromisoformat(e['at']).timestamp()
out=[]
for engine,run in [('Core','runtime3'),('Legacy','runtime3/continuation')]:
 E=F/run;commands=json.loads((E/'commands.json').read_text());results=json.loads((E/'state-results.json').read_text())
 end=next(i for i,c in enumerate(commands) if any(engine.lower()+'-lifetime-restored' in str(a) for a in c['command']))
 cmd=[c for c in commands[:end] if 'PauseResume' in c['command']][-2]
 start=cmd['utc'];playing=next(e for e in events if stamp(e)>=start and e['data']['kind']=='state' and e['data'].get('state')=='Playing');t0=stamp(playing)
 visuals=[e for e in events if e['data']['kind']=='visual' and e['data'].get('engine')==engine and t0<=stamp(e)<t0+2]
 s0=next(e for e in visuals if .5<stamp(e)-t0<1.0);s1=next(e for e in visuals if 1.2<stamp(e)-t0<1.7)
 dt=stamp(s1)-stamp(s0);base=next(x['detail']['before'] for x in results if x['name']==engine+'FourModesPausedPositionAgeStable');pos=positions(base)
 motion=[]
 for row in s1['data']['rows']:
  if row['text'] not in {r['text'] for r in base['rows']}:continue
  old=next(r for r in s0['data']['rows'] if r['text']==row['text']);rate=next(r['rate'] for r in pos if r['text']==row['text']);rolling=row['text'] in ['ROLL18','REV18']
  expected=rate*dt*1000*(-1 if row['text']=='ROLL18' else 1 if row['text']=='REV18' else 0);actual=row['x']-old['x'];tol=rate*75+2 if rolling else 2
  motion.append(dict(text=row['text'],expected=expected,actual=actual,tolerance=tol,pass_=abs(expected-actual)<=tol and (not rolling or abs(actual)>tol)))
 out.append(dict(engine=engine,sourceRun=run,fromObservation=s0,toObservation=s1,elapsed=dt,motion=motion,verticalSlots=all(r['y']==0 for r in base['rows']),pass_=len(motion)==4 and all(r['pass_'] for r in motion)))
(F/'runtime3/tight-displacement.json').write_text(json.dumps(out,indent=2))
print(json.dumps([dict(engine=x['engine'],elapsed=x['elapsed'],motion=x['motion'],pass_=x['pass_']) for x in out],indent=2))
assert all(x['pass_'] and x['verticalSlots'] for x in out)
