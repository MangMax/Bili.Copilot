"""Independent configuration-derived positions and observed remaining lifetimes."""
import time

def positions(v):
    out=[]
    for r in v['rows']:
        rate=v['speed']*5*v['extra']*.02*v['scale']*(1+min(r['width']*.0015,.2))
        expected=(v['width']-rate*r['age'] if r['text']=='ROLL18' else -r['width']+rate*r['age'] if r['text']=='REV18' else (v['width']-r['width'])/2)
        out.append(dict(text=r['text'],actual=r['x'],expected=expected,error=abs(r['x']-expected),rate=rate,pass_=abs(r['x']-expected)<1.5))
    return out

def lifetime(ns,engine):
    invoke,wait,latest,baseline,check,pause,seek20,snap,events=[ns[n] for n in ['invoke','wait','latest','baseline','check','pause','seek20','snap','events']]
    invoke(engine);time.sleep(1);pause();seek20();v=baseline(engine)
    pos=positions(v);check(engine+'ConfiguredPositions',all(x['pass_'] for x in pos),pos)
    # Paused wall time must not consume lifetime.
    time.sleep(3.4);v2=baseline(engine)
    stable=lambda rows:[{k:r[k] for k in ('text','x','y','width','height','age')} for r in rows]
    check(engine+'FourModesPausedPositionAgeStable',stable(v['rows'])==stable(v2['rows']),dict(before=v,after=v2))
    expected={}
    for r in v2['rows']:
        rate=next(x['rate'] for x in pos if x['text']==r['text'])
        expected[r['text']]=((r['x']+r['width']*(1.2 if engine=='Core' else 1))/rate if r['text']=='ROLL18' else (v2['width']-r['x'])/rate if r['text']=='REV18' else 3000-r['age'])/1000
    start=time.time();invoke('PauseResume');wait(lambda:latest('state').get('state')=='Playing')
    # Derive time zero from observer's first actual Playing state, retain its 250ms sampling bound.
    wait(lambda:all(r['text'] not in expected for r in latest('visual').get('rows',[])),max(expected.values())+8)
    pause()
    trace=[e for e in events() if e['data']['kind'] in ('state','visual') and __import__('datetime').datetime.fromisoformat(e['at']).timestamp()>=start]
    first=next(e for e in trace if e['data']['kind']=='state' and e['data'].get('state')=='Playing')
    stamp=lambda e:__import__('datetime').datetime.fromisoformat(e['at']).timestamp()
    t0=stamp(first);visuals=[e for e in trace if e['data']['kind']=='visual' and e['data'].get('engine')==engine and stamp(e)>=t0]
    detail=[]
    for name,remaining in expected.items():
        present=[e for e in visuals if name in [r['text'] for r in e['data'].get('rows',[])]]
        absent=next(e for e in visuals if name not in [r['text'] for r in e['data'].get('rows',[])])
        last=stamp(present[-1])-t0 if present else 0;gone=stamp(absent)-t0
        detail.append(dict(text=name,expectedRemaining=remaining,lastPresent=last,firstAbsent=gone,pass_=bool(present) and last-.55<=remaining<=gone+.55))
    check(engine+'ObservedRemainingLifetimes',all(d['pass_'] for d in detail),detail)
    # Motion at a real Playing observation uses the same configured rate; fixed rows stay centered.
    sample0=next(e for e in visuals if .5<stamp(e)-t0<1.0)
    sample1=next(e for e in visuals if 1.2<stamp(e)-t0<1.7)
    dt=stamp(sample1)-stamp(sample0);motion=[]
    for r in sample1['data']['rows']:
        if r['text'] not in expected:continue
        old=next(x for x in sample0['data']['rows'] if x['text']==r['text']);rate=next(x['rate'] for x in pos if x['text']==r['text'])
        rolling=r['text'] in ('ROLL18','REV18')
        delta=r['x']-old['x'];target=rate*dt*1000*(-1 if r['text']=='ROLL18' else 1 if r['text']=='REV18' else 0)
        tolerance=rate*75+2 if rolling else 2
        motion.append(dict(text=r['text'],delta=delta,expected=target,tolerance=tolerance,elapsed=dt,pass_=abs(delta-target)<=tolerance and (not rolling or abs(delta)>tolerance)))
    check(engine+'ResumeDisplacement',len(motion)==4 and all(x['pass_'] for x in motion),motion)
    seek20();baseline(engine);snap(engine.lower()+'-lifetime-restored')
