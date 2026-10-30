import hashlib, json, pathlib, statistics
root=pathlib.Path(__file__).resolve().parents[3]
data=root/'artifacts/net10-adaptation'; dest=pathlib.Path(__file__).parent
summary=[]
for name in ('v3-r1-net9','v3-r2-net10','v3-r3-net9','v3-r4-net10','final-net10'):
    path=data/name
    if not (path/'accepted.json').exists(): raise RuntimeError(f'{name} not accepted')
    baseline=json.loads((path/'idle-baseline.json').read_text(encoding='utf-8-sig'))
    results=json.loads((path/'player/results.json').read_text(encoding='utf-8-sig'))
    assert len(results)==15 and all(r['status']=='PASS' for r in results)
    main=json.loads((path/'main/main-results.json').read_text(encoding='utf-8-sig'))
    assert len(main)==4 and all(r['status']=='PASS' for r in main)
    end=json.loads((path/'main/exit.json').read_text(encoding='utf-8-sig'))
    assert end['explicitExit'] and end['unhandledCount']==0
    font=json.loads((path/'main/font-results.json').read_text(encoding='utf-8-sig'))
    assert font['status']=='PASS' and font['before']==0 and font['count']>0
    cleanup=json.loads((path/'cleanup.json').read_text(encoding='utf-8-sig'))
    assert cleanup['processAbsent'] and cleanup['registrationAbsent']
    gate=json.loads((path/'hash-gate.json').read_text(encoding='utf-8-sig'))
    assert not gate['missing'] and not gate['extra'] and not gate['mismatched']
    cycles=[json.loads(l) for l in (path/'main/events.jsonl').read_text(encoding='utf-8-sig').splitlines() if l.strip()]
    assert any(r['kind']=='idle-cycles' and r['data']['count']==20 and r['data']['sameHwnd'] for r in cycles)
    assert baseline['protocol']==('external-os-v3' if name.startswith('v3-') else 'external-os-v2') and len(baseline['rows'])==183
    phases={}
    for phase in ('visible','hidden','restored'):
        rows=[r for r in baseline['rows'] if r['phase']==phase]
        assert len(rows)==61 and all(r['scale']==1.5 and r['visible']==(phase!='hidden') for r in rows)
        assert [r['sample'] for r in rows]==list(range(61))
        assert len({r['pid'] for r in rows})==1 and len({r['hwnd'] for r in rows})==1
        assert all(b['cpuMilliseconds']>=a['cpuMilliseconds'] and b['monotonicSeconds']>a['monotonicSeconds'] for a,b in zip(rows,rows[1:]))
        elapsed=rows[-1]['monotonicSeconds']-rows[0]['monotonicSeconds']
        assert elapsed>0
        phases[phase]=dict(cpuMsPerSecond=(rows[-1]['cpuMilliseconds']-rows[0]['cpuMilliseconds'])/elapsed,
            privateMiBMedian=statistics.median(r['privateBytes'] for r in rows)/1048576,
            workingSetMiBMedian=statistics.median(r['workingSet'] for r in rows)/1048576,
            handleRange=[min(r['handles'] for r in rows),max(r['handles'] for r in rows)],
            threadRange=[min(r['threads'] for r in rows),max(r['threads'] for r in rows)],elapsedSeconds=elapsed)
    summary.append(dict(run=name,protocol=baseline['protocol'],phases=phases,dllCount=gate['count'],observerSampleMsMedian=statistics.median(baseline['observerSampleSeconds'])*1000,
                        observerSampleMsMax=max(baseline['observerSampleSeconds'])*1000,fontCount=font['count']))
(dest/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
