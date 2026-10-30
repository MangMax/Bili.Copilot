import json, pathlib, statistics, sys, datetime, hashlib
root = pathlib.Path(sys.argv[1])
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def utc(s): return datetime.datetime.fromisoformat(s.replace('Z','+00:00')).timestamp()
def parse(events):
    pauses=[]; collections=[]; pending=None; starts={}; errors=[]
    for e in sorted(events,key=lambda x:x['ms']):
        name=e['name'];d=e['data']
        if name=='GC/SuspendEEStart':
            if pending is not None: errors.append('nested suspend')
            pending=e
        elif name=='GC/RestartEEStop':
            if pending is None: errors.append('restart without suspend');continue
            reasons=[x['data']['Reason'] for x in events if x['name']=='GC/Start' and pending['ms']<=x['ms']<=e['ms']]
            stopped=[x for x in events if x['name']=='GC/SuspendEEStop' and pending['ms']<=x['ms']<=e['ms']]
            restarting=[x for x in events if x['name']=='GC/RestartEEStart' and pending['ms']<=x['ms']<=e['ms']]
            if len(stopped)!=1 or len(restarting)!=1: errors.append('missing suspension inner pair')
            inner=restarting[0]['ms']-stopped[0]['ms'] if stopped and restarting else None
            pauses.append(dict(startUtc=pending['utc'],endUtc=e['utc'],ms=e['ms']-pending['ms'],fullySuspendedMs=inner,suspendReason=pending['data']['Reason'],gcReasons=reasons))
            pending=None
        elif name=='GC/Start': starts[d['Count']]=e
        elif name=='GC/Stop':
            start=starts.pop(d['Count'],None)
            if start is None: errors.append('stop without start');continue
            collections.append(dict(count=d['Count'],reason=start['data']['Reason'],generation=start['data']['Depth'],type=start['data']['Type'],startUtc=start['utc'],endUtc=e['utc'],collectionMs=e['ms']-start['ms']))
    if pending is not None: errors.append('open suspend')
    if starts: errors.append('open collections')
    return pauses,collections,errors
summary=[]
for folder in sorted(root.iterdir()):
    if not folder.is_dir() or not (folder/'records.json').exists(): continue
    rows=read(folder/'records.json'); samples={x['name']:x for x in rows if 'gen' in x}
    if 'measure-end' not in samples: continue
    a=samples['measure-start'];b=samples['measure-end'];last=samples['observe-10']
    assert len([x for x in rows if 'cycle' in x])==50
    assert all(x['pass'] for x in rows if 'pass' in x)
    out=dict(run=folder.name,assertions=sum('pass' in x for x in rows),durationMs=(utc(b['utc'])-utc(a['utc']))*1000,observationMs=(utc(last['utc'])-utc(b['utc']))*1000,counts=[y-x for x,y in zip(a['gen'],b['gen'])],idleCounts=[y-x for x,y in zip(b['gen'],last['gen'])],emptyMedianMs=statistics.median(x['emptyMs'] for x in rows if 'emptyMs' in x),emptyMaxMs=max(x['emptyMs'] for x in rows if 'emptyMs' in x),aliveEnd=[b[k] for k in ['controlsAlive','documentsAlive','blocksAlive']],aliveIdle=[last[k] for k in ['controlsAlive','documentsAlive','blocksAlive']],allocatedDelta=b['allocated']-a['allocated'],privateDelta=b['privateBytes']-a['privateBytes'],managedDelta=b['managed']-a['managed'])
    if (folder/'events.json').exists():
        raw=read(folder/'events.json');pauses,collections,errors=parse(raw['events'])
        assert raw['lost']==0 and not errors,(folder,raw['lost'],errors)
        measured=[p for p in pauses if utc(a['utc'])<=utc(p['startUtc']) and utc(p['endUtc'])<=utc(b['utc'])]
        cs=[c for c in collections if utc(a['utc'])<=utc(c['startUtc']) and utc(c['endUtc'])<=utc(b['utc'])]
        assert len(cs)==out['counts'][0],(folder,len(cs),out['counts'])
        reasons={r:sum(c['reason']==r for c in cs) for r in sorted({c['reason'] for c in cs})}
        out.update(lost=raw['lost'],pairingErrors=errors,pauses=len(measured),pauseTotalMs=sum(p['ms'] for p in measured),fullySuspendedTotalMs=sum(p['fullySuspendedMs'] for p in measured),pauseMaxMs=max([p['ms'] for p in measured],default=0),pauseMedianMs=statistics.median([p['ms'] for p in measured]) if measured else 0,reasons=reasons,collectionTotalMs=sum(c['collectionMs'] for c in cs))
        (folder/'paired.json').write_text(json.dumps(dict(pauses=pauses,collections=collections,errors=errors),indent=2))
    summary.append(out)
(root/'summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
