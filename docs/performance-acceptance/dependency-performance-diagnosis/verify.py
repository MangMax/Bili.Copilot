"""Verify final evidence against original files; no benchmark/build/deployment."""
import gzip, hashlib, json, pathlib, subprocess
root=pathlib.Path(__file__).resolve().parents[3]
native=root/'artifacts/dependency-upgrade'; diag=root/'artifacts/dependency-performance-diagnosis'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
runs=['d34-g1r-1-a','d34-g1r-2-b','d34-g1r-3-b','d34-g1r-4-a',
      'd34-g2-1-b','d34-g2-2-c','d34-g2-3-c','d34-g2-4-b']
checks=[]; runtime=set(); mpv=set()
for run in runs:
    player=read(native/f'native-{run}-player/results.json')
    main=read(native/f'native-{run}-main/main-results.json')
    assert len(player)==15 and all(t['status']=='PASS' for t in player),run
    assert len(main)==4 and all(t['status']=='PASS' for t in main),run
    exit=read(native/f'native-{run}-main/exit.json')
    assert exit['explicitExit'] and exit['unhandledCount']==0 and exit['baselineRows']==183,run
    cleanup=read(native/f'{run}-cleanup.json')
    assert cleanup['exited'] and cleanup['registrationRemoved'],run
    rows=read(native/f'native-{run}-main/idle-baseline.json')['rows']
    assert len(rows)==183 and all(r['scale']==1.5 for r in rows),run
    for phase in ('visible','hidden','restored'):
        assert [r['sample'] for r in rows if r['phase']==phase]==list(range(61)),run
    events=[json.loads(x) for x in (native/f'native-{run}-main/events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert any(e['kind']=='idle-cycles' and e['data']['count']==20 and e['data']['sameHwnd'] for e in events),run
    closure=read(native/f'{run}-closure.json')
    assert closure and all(x['match'] and x['buildSha256']==x['deployedSha256'] for x in closure),run
    modules={m['module']:m for m in read(native/f'{run}-loaded-modules.json')}
    runtime.add(modules['coreclr.dll']['sha256']);mpv.add(modules['libmpv-2.dll']['sha256'])
    expected='1.4.0' if run.endswith('-a') else '1.3.2'
    assert modules['Microsoft.Graphics.Canvas.dll']['fileVersion']==expected,run
    app=modules['Microsoft.WindowsAppRuntime.dll']
    assert app['productVersion'].startswith('1.8' if run.endswith('-c') else '2.6'),run
    checks.append(dict(run=run,pid=cleanup['pid'],dlls=len(closure),player=15,main=4,rows=183,loops=20))
assert len(runtime)==len(mpv)==1
for arm in ('b','c'):
    p=diag/f'controls-{arm}-results'
    results=read(p/'results.json');assert len(results)==7 and all(r['status']=='PASS' for r in results)
    assert all(r['match'] for r in read(p/'closure.json'))
    assert read(p/'cleanup.json')['registrationRemoved'] and read(p/'cleanup.json')['exited']
for folder in ('lcs-results','lcs-tiered-off-results'):
    files=list((diag/folder).glob('[1-6]-*.json')); assert len(files)==6
    for p in files:
        d=read(p);assert d['runtime']=='.NET 9.0.22'
        assert len(d['tests'])==15 and all(t['status']=='PASS' for t in d['tests'])
        assert len(d['rows'])==180 and all(r['middle']>0 for r in d['rows'])
base=read(diag/'lcs-default-config.json');off=read(diag/'lcs-tiered-off-results/config.json')
hashes=lambda d:{pathlib.Path(h['Path']).name:h['Hash'] for h in d['hashes']}
assert hashes(base)['Lcs.dll']==hashes(off)['Lcs.dll']
assert off['DOTNET_TieredCompilation']==off['COMPlus_TieredCompilation']=='0'
audit=read(diag/'final-deployment-audit.json')
assert len(audit)==10 and all(not x['extra'] and not x['missing'] and not x['mismatches'] for x in audit)
cleanup=read(diag/'final-cleanup-audit.json')
assert not cleanup['remainingRegistrations'] and not cleanup['remainingOwnedProcesses'] and not cleanup['kernelStatus']
assert not subprocess.check_output(['git','diff','--','src','externals/bili-kernel','global.json'],cwd=root)
assert not subprocess.check_output(['git','status','--porcelain'],cwd=root/'externals/bili-kernel')
ledger=pathlib.Path(__file__).parent/'evidence/ledger.json'
if ledger.exists():
    for r in read(ledger):
        original=(root/r['path']).read_bytes()
        assert len(original)==r['bytes'] and hashlib.sha256(original).hexdigest()==r['sha256'],r['path']
        if 'archive' in r: assert gzip.decompress((ledger.parent/r['archive']).read_bytes())==original,r['path']
print(json.dumps(dict(status='PASS',runs=checks,controls=14,lcsProcesses=12,lcsBehaviorCasesPerProcess=15,
                     lcsMeasurements=2160,coreclrSha256=list(runtime),libmpvSha256=list(mpv)),indent=2))
