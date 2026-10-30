import gzip, hashlib, json, pathlib, subprocess, sys, xml.etree.ElementTree as ET
here=pathlib.Path(__file__).parent
ledger=json.loads((here/'evidence/ledger.json').read_text(encoding='utf-8'))
if '--tracked' in sys.argv:
    root=here.resolve().parents[2]
    tracked=set(subprocess.check_output(['git','ls-files','-z'],cwd=root).decode('utf-8').split('\0'))
    for entry in ledger:
        if 'archive' in entry:
            path=(here/'evidence'/entry['archive']).resolve().relative_to(root).as_posix()
            assert path in tracked,('Archive missing from Git index',path)
for entry in ledger:
    if 'archive' in entry:
        raw=gzip.decompress((here/'evidence'/entry['archive']).read_bytes())
        assert len(raw)==entry['bytes'] and hashlib.sha256(raw).hexdigest()==entry['sha256'],entry['path']
def read(path):
    return json.loads(gzip.decompress((here/'evidence'/(path+'.gz')).read_bytes()).decode('utf-8-sig'))
ownership=read('final-ownership.json')
assert len(ownership['runs'])==11 and all(r['ownedProcessAbsent'] and r['registrationAbsent'] for r in ownership['runs'])
assert ownership['parent']=='eff8d24c87620cf2b6025a11b8f8ddda27b1a116'
assert ownership['kernel']=='0ae5e4e000a41a07f6696746706df11c06e8835b'
runtime=read('final-test-runtimeconfig.json')['runtimeOptions']
assert runtime['tfm']=='net10.0' and runtime['rollForward']=='LatestPatch'
for graph in json.loads((here/'production-package-graphs.json').read_text(encoding='utf-8-sig')):
    raw=gzip.decompress((here/'evidence'/('final-assets-'+graph['path'].replace('/','_')+'.gz')).read_bytes())
    assert hashlib.sha256(raw).hexdigest().upper()==graph['sha256']
total=0
for run in ('r1-net9','r2-net10','r4-net10','r5-net9','final-net10','v3-r1-net9','v3-r2-net10','v3-r3-net9','v3-r4-net10'):
    assert read(run+'/accepted.json')['explicitExit']
    assert len(read(run+'/idle-baseline.json')['rows'])==183
    for name,count in (('player/results.json',15),('main/main-results.json',4)):
        result=read(run+'/'+name); assert len(result)==count and all(x['status']=='PASS' for x in result)
    cleanup=read(run+'/cleanup.json'); assert cleanup['processAbsent'] and cleanup['registrationAbsent']
    if 'observerAbsent' in cleanup: assert cleanup['observerAbsent']
    else: assert read(run+'/observer-cleanup-audit.json')['processAbsent']
    gate=read(run+'/hash-gate.json'); assert gate['missing']==gate['extra']==gate['mismatched']==0
    total+=gate['count']
    if '--live' in sys.argv:
        root=here.resolve().parents[2]; layout=root/'artifacts/net10-adaptation'/run/'layout'
        expected=set(read(run+'/provenance.json')['dlls'])
        actual={str(f.relative_to(layout)).lower()+'='+hashlib.sha256(f.read_bytes()).hexdigest().upper() for f in layout.rglob('*.dll')}
        assert actual==expected,(run,expected-actual,actual-expected)
red=read('r3-net9/failure.json'); assert not red['accepted']
controls=read('controls-native/results.json'); assert len(controls)==7 and all(x['status']=='PASS' for x in controls)
cleanup=read('controls-native/cleanup.json'); assert cleanup['processAbsent'] and cleanup['registrationAbsent']
control_gate=read('controls-native/hash-gate.json')
assert control_gate['missing']==control_gate['extra']==control_gate['mismatched']==0
if '--live' in sys.argv:
    layout=root/'artifacts/net10-adaptation/controls-native/layout'
    actual={str(f.relative_to(layout)).lower()+'='+hashlib.sha256(f.read_bytes()).hexdigest().upper() for f in layout.rglob('*.dll')}
    assert actual==set(control_gate['dlls'])
    closures=json.loads(gzip.decompress((here/'closures.json.gz').read_bytes()))
    for arm,record in closures.items():
        frozen=root/'artifacts/net10-adaptation'/arm/'frozen-output'
        assert not list(frozen.rglob('*.appxrecipe'))
        expected={r['path'].lower()+'='+r['sha256'] for r in record['frozenFiles'] if r['path'].lower().endswith('.dll')}
        actual={str(f.relative_to(frozen)).lower()+'='+hashlib.sha256(f.read_bytes()).hexdigest().upper() for f in frozen.rglob('*.dll')}
        assert actual==expected,arm
trx=gzip.decompress((here/'evidence/tests-final/final-net10.trx.gz').read_bytes())
tree=ET.fromstring(trx); counters=tree.find('.//{*}Counters').attrib
assert counters['passed']=='487' and counters['failed']=='0' and counters['executed']=='487',counters
print(json.dumps(dict(archiveHashes='PASS',trackedArchiveCheck='PASS' if '--tracked' in sys.argv else 'not requested',nativeCompletedRuns=9,failedRunRetained=True,completedRunDlls=total,controls=7,tests=counters,liveDllCheck='PASS' if '--live' in sys.argv else 'not requested')))
