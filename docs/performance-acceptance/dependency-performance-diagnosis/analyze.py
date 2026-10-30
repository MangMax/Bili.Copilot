import datetime, json, pathlib, statistics, sys
root=pathlib.Path(sys.argv[1])
out={}
native=[]
for run in sys.argv[2:]:
    p=root/f'native-{run}-main/idle-baseline.json'
    data=json.loads(p.read_text(encoding='utf-8-sig'))
    phases={}
    for phase in ('visible','hidden','restored'):
        rows=[r for r in data['rows'] if r['phase']==phase]
        assert len(rows)==61 and all(r['scale']==1.5 for r in rows)
        assert [r['sample'] for r in rows]==list(range(61))
        def rate(a,b):
            elapsed=(datetime.datetime.fromisoformat(rows[b]['utc'])-datetime.datetime.fromisoformat(rows[a]['utc'])).total_seconds()
            cpu=rows[b]['cpuMilliseconds']-rows[a]['cpuMilliseconds']
            return dict(cpuDeltaMilliseconds=cpu,elapsedSeconds=elapsed,cpuMsPerSecond=cpu/elapsed)
        phases[phase]={'full':rate(0,60),'first':rate(0,30),'last':rate(30,60),
            'privateMiBMedian':statistics.median(r['privateBytes']/1048576 for r in rows)}
    native.append(dict(run=run,phases=phases))
out['native']=native
print(json.dumps(out,indent=2))
