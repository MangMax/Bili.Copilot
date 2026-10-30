"""Descriptive, per-process and per-state results; never pool historical runs."""
import csv, json, statistics
from prepare import BASE, HERE, save
from gate import check, read

def describe(values):
    return {'first':values[0],'last':values[-1],'median':statistics.median(values),'min':min(values),'max':max(values)}
def rate(rows):
    return (rows[-1]['cpuMilliseconds']-rows[0]['cpuMilliseconds'])/(rows[-1]['monotonicSeconds']-rows[0]['monotonicSeconds'])

result={'protocol':'external-os-v3/final-source/ABBA','runs':[],'missing':[],'ranges':[]}
flat=[]
for name,arm in [('a1-net10','net10'),('b1-net11','net11'),('b2-net11','net11'),('a2-net10','net10')]:
    folder=BASE/name
    if not (folder/'accepted.json').exists():
        result['missing'].append(name);continue
    check(folder)
    data=read(folder/'idle-baseline.json')
    run={'run':name,'arm':arm,'states':[],'observerWallMs':describe([x*1000 for x in data['observerSampleSeconds']])}
    for phase in ('visible','hidden','restored'):
        rows=[r for r in data['rows'] if r['phase']==phase]
        state={'phase':phase,'startUtc':rows[0]['utc'],'endUtc':rows[-1]['utc'],'samples':len(rows),'wallSeconds':rows[-1]['monotonicSeconds']-rows[0]['monotonicSeconds'],
               'cpuMsPerSecond':rate(rows),'firstHalfCpuMsPerSecond':rate(rows[:31]),'lastHalfCpuMsPerSecond':rate(rows[30:]),
               'oneSecondCpuMsPerSecond':describe([rate(rows[i:i+2]) for i in range(60)])}
        for metric in ('privateBytes','workingSet','handles','threads'):state[metric]=describe([r[metric] for r in rows])
        run['states'].append(state)
        flat.extend(dict(run=name,**r) for r in rows)
    before=read(folder/'background-before.json');after=read(folder/'background-after.json')
    first={r['pid']:r for r in before['rows']}
    changes=[]
    for r in after['rows']:
        earlier=first.get(r['pid'])
        if earlier and earlier['name']==r['name'] and earlier.get('startedUtc')==r.get('startedUtc') and r['cpuSeconds'] is not None and earlier['cpuSeconds'] is not None:
            changes.append({'pid':r['pid'],'name':r['name'],'deltaCpuSeconds':r['cpuSeconds']-earlier['cpuSeconds']})
    run['background']={'startUtc':before['utc'],'endUtc':after['utc'],'topContinuingProcessCpu':sorted(changes,key=lambda r:r['deltaCpuSeconds'],reverse=True)[:15], 'limitation':'Does not measure processes that exited between snapshots; not causal attribution.'}
    result['runs'].append(run)
for arm in ('net10','net11'):
    for phase in ('visible','hidden','restored'):
        states=[s for r in result['runs'] if r['arm']==arm for s in r['states'] if s['phase']==phase]
        if not states:continue
        result['ranges'].append({'arm':arm,'phase':phase,'n':len(states),'cpuMsPerSecond':describe([s['cpuMsPerSecond'] for s in states]),**{k:describe([s[k]['median'] for s in states]) for k in ('privateBytes','workingSet','handles','threads')}})
save(BASE/'analysis.json',result)
if flat:
    with (BASE/'samples.csv').open('w',encoding='utf-8',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(flat[0]));w.writeheader();w.writerows(flat)
lines=['| Run | State | CPU ms/s (first / last half) | Private MiB median [min,max] | Working set MiB median [min,max] | Handles median [min,max] | Threads median [min,max] |','|---|---|---|---|---|---|---|']
for r in result['runs']:
    for s in r['states']:
        cells=[r['run'],s['phase'],f"{s['cpuMsPerSecond']:.3f} ({s['firstHalfCpuMsPerSecond']:.3f} / {s['lastHalfCpuMsPerSecond']:.3f})"]
        for k in ('privateBytes','workingSet','handles','threads'):
            d=s[k];scale=1048576 if k in ('privateBytes','workingSet') else 1
            cells.append(f"{d['median']/scale:.2f} [{d['min']/scale:.2f},{d['max']/scale:.2f}]")
        lines.append('| '+' | '.join(cells)+' |')
(BASE/'tables.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({'accepted':len(result['runs']),'missing':result['missing'],'ranges':result['ranges']},indent=2))
