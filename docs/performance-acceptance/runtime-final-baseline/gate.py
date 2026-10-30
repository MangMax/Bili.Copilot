"""Strict additional identity, query-window and behavior gates over existing verdict."""
import datetime, importlib.util, json, pathlib, sys
HERE=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('exit_gate',HERE.parent/'net11-exit-reproduction/exit_gate.py')
gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def check(folder):
    r=read(folder/'exit-observation.json');ready=read(folder/'ready.json')
    result=gate.verdict(r,read(folder/'events-safe.json'))
    for k in ('pid','exe','creationFileTime','startUtc','package'):
        assert r[k]==ready[k],k
    assert ready['handleRetained']
    assert all(q['ok'] for q in r['queries'])
    assert r['queries'][-1]['startedSecondsAfterExit']>=30
    assert gate.timestamp(r['lastQueryUtc'])-gate.timestamp(r['terminationUtc'])>=30
    assert r['managedUnhandled']==0
    assert result['status']=='PASS_BOUNDED',result
    for name,count in [('player/results.json',15),('main/main-results.json',4)]:
        rows=read(folder/name);assert len(rows)==count and all(x['status']=='PASS' for x in rows),name
    font=read(folder/'main/font-results.json')
    assert font['status']=='PASS' and font['before']==0 and font['count']>0
    samples=read(folder/'idle-baseline.json');assert samples['protocol']=='external-os-v3'
    rows=samples['rows'];assert len(rows)==183
    for phase in ('visible','hidden','restored'):
        current=[r for r in rows if r['phase']==phase]
        assert len(current)==61 and [r['sample'] for r in current]==list(range(61))
        assert all(b['cpuMilliseconds']>=a['cpuMilliseconds'] and b['monotonicSeconds']>a['monotonicSeconds'] for a,b in zip(current,current[1:]))
        assert all(r['pid']==ready['pid'] and r['scale']==1.5 and r['visible']==(phase!='hidden') for r in current)
        assert all((r['x'],r['y'],r['width'],r['height'])==(120,90,1200,800) for r in current)
    assert len({r['hwnd'] for r in rows})==1
    events=[json.loads(s) for s in (folder/'main/events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert any(e['kind']=='idle-cycles' and e['data']['count']==20 and e['data']['sameHwnd'] for e in events)
    cleanup=read(folder/'cleanup.json')
    assert cleanup['processAbsent'] and cleanup['registrationAbsent'] and cleanup['observerAbsent']
    return result
if __name__=='__main__':
    folder=pathlib.Path(sys.argv[1])
    try: result=check(folder)
    except Exception as e: result={'status':'BLOCKED','reason':repr(e)}
    (folder/'exit-verdict.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result));sys.exit(0 if result['status']=='PASS_BOUNDED' else 2)
