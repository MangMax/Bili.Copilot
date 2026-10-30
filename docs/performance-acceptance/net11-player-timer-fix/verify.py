"""Read-only evidence audit; emits a compact ledger, never rewrites a verdict."""
import hashlib, importlib.util, json, pathlib, sys, xml.etree.ElementTree as ET
root = pathlib.Path.cwd()
base = root / 'artifacts/timer-fix'
out = pathlib.Path(__file__).parent
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
def graph(p):
    return {k: v['sha512'] for k, v in read(p)['libraries'].items() if v['type'] == 'package'}
def normalized(p): return p.read_bytes().removeprefix(b'\xef\xbb\xbf').replace(b'\r\n', b'\n').rstrip(b'\n')
spec = importlib.util.spec_from_file_location('gate', root/'docs/performance-acceptance/net11-exit-reproduction/exit_gate.py')
gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)
historical = pathlib.Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction')
old = historical/'original1'
red = gate.verdict(read(old/'exit-observation.json'), [gate.event(x) for x in read(old/'event-xml.json')])
assert red['status'] == 'BLOCKED' and read(old/'exit-observation.json')['exitCode'] == 0xC000027B
assert read(base/'historical-dump-hash.json')['Hash'].lower() == 'eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d'
result = dict(historicalRed=red, historicalDump=read(base/'historical-dump-hash.json'), variants={}, runs={})
result['sdkArchive'] = read(base/'sdk-archive-hash.json')
assert result['sdkArchive']['Hash'].lower() == 'dae0a0450d92b5315034db134259acffe9ec0043d93dced4fb2a9b8700520e87a99005d6845ad80716f4a8f30df24ae5363ca7c9be391797b4f53170e9b2c088'
expected = graph(historical/'fixture/obj/project.assets.json')
for variant in ('stop-only', 'green', 'final'):
    fixture = base/variant/'fixture'
    files = read(fixture/'frozen-all-files.json')
    frozen = fixture/'frozen-output'
    assert all(sha(frozen/f['path']) == f['sha256'] for f in files)
    actual = {str(p.relative_to(frozen)) for p in frozen.rglob('*') if p.is_file()}
    assert actual == {f['path'] for f in files}
    assert not list(frozen.rglob('*.appxrecipe'))
    packages = graph(fixture/'obj/project.assets.json')
    assert packages == expected
    differences = []
    ui = root/'src/Desktop/BiliCopilot.UI'
    for p in ui.rglob('*.cs'):
        if any(x in p.relative_to(ui).parts for x in ('obj', 'bin')): continue
        other = fixture/p.relative_to(ui)
        if other.exists() and normalized(p) != normalized(other): differences.append(str(p.relative_to(ui)))
    result['variants'][variant] = dict(files=len(files), dlls=sum(f['path'].endswith('.dll') for f in files),
        packages=len(packages), packageGraphMatchesHistorical=True, sourceDifferences=sorted(differences),
        panelSha256=sha(fixture/'Controls/Core/PlayerInteractivePanel.xaml.cs'),
        uiDllSha256=sha(frozen/'BiliCopilot.UI.dll'), coreclrSha256=sha(frozen/'coreclr.dll'))
    assert result['variants'][variant]['coreclrSha256'] == '46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2'
assert result['variants']['final']['panelSha256'] == sha(root/'src/Desktop/BiliCopilot.UI/Controls/Core/PlayerInteractivePanel.xaml.cs')
assert set(result['variants']['final']['sourceDifferences']) == {'App.xaml.cs', 'GlobalDependencies.cs', str(pathlib.Path('Toolkits/FontCatalog.cs'))}
for name in ('stop1', 'green1', 'final1', 'final2', 'controls1', 'controls2'):
    folder = root/'artifacts/exit-reproduction'/name
    observation = read(folder/'exit-observation.json')
    verdict = gate.verdict(observation, [gate.event(x) for x in read(folder/'event-xml.json')])
    assert verdict == read(folder/'exit-verdict.json')
    if name not in ('stop1', 'controls1'): assert verdict['status'] == 'PASS_BOUNDED', (name, verdict)
    cleanup = read(folder/'cleanup.json')
    assert cleanup['processAbsent'] and cleanup['registrationAbsent'] and cleanup['observerAbsent']
    entry = dict(pid=observation['pid'], exitCode=observation['exitCode'], handleRetained=observation['handleRetained'],
                 startUtc=observation['startUtc'], collectionEndUtc=observation['collectionEndUtc'],
                 verdict=verdict, cleanup=cleanup, hashGate=read(folder/'hash-gate.json'),
                 yieldResult=read(folder/'yield.json'))
    if not name.startswith('controls'):
        entry['timer'] = read(folder/'timer-results.json')
        if name != 'stop1':
            assert len(entry['timer']) == 9 and all(x['status']=='PASS' for x in entry['timer'])
            entry['player'] = read(folder/'player/results.json')
            entry['main'] = read(folder/'main/main-results.json')
            assert len(entry['player'])==15 and all(x['status']=='PASS' for x in entry['player'])
            assert len(entry['main'])==4 and all(x['status']=='PASS' for x in entry['main'])
            runtime = next(m for m in read(folder/'modules.json') if m['name'].lower()=='coreclr.dll')
            assert runtime['sha256'].lower()==result['variants']['final']['coreclrSha256']
            entry['loadedRuntime'] = runtime
            events = [json.loads(x) for x in (folder/'main/events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            exits = [x for x in events if x['kind']=='explicit-exit']
            assert len(exits)==1 and exits[0]['data']['players']==1
            entry['explicitExit'] = exits[0]
    else:
        entry['controls'] = read(folder/'controls/results.json')
        assert all(x['status']=='PASS' for x in entry['controls'])
    result['runs'][name] = entry
rows = [json.loads(s) for s in (root/'artifacts/exit-reproduction/green1/exit-trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
states = {}; violations = []
for r in rows:
    if r['stage'] not in ('timer-start', 'timer-stop', 'timer-vm-read'): continue
    owner = r['owner']['id']
    if r['stage']=='timer-vm-read' and states.get(owner)=='timer-stop': violations.append(r)
    if r['stage']!='timer-vm-read': states[owner]=r['stage']
assert not violations and all(s=='timer-stop' for s in states.values())
assert not [r for r in rows if r['stage']=='first-chance' and r['owner'].get('hresult')==-2147418113]
result['timerTrace'] = dict(instances=len(states), starts=sum(r['stage']=='timer-start' for r in rows),
    stops=sum(r['stage']=='timer-stop' for r in rows), reads=sum(r['stage']=='timer-vm-read' for r in rows),
    readAfterStop=violations, finalStates=states,
    exitTail=[r for r in rows if r['sequence']>=650 and r['stage']!='first-chance'])
trx = ET.parse(base/'tests/timer-final.trx'); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
counts = trx.find('.//t:Counters', ns).attrib
assert counts['passed']=='487' and counts['failed']=='0' and counts['executed']=='487'
result['suite'] = dict(counters=counts, trxSha256=sha(base/'tests/timer-final.trx'))
result['builds'] = {}
for label, directory in [('DebugClean', 'debug'), ('ReleaseJIT', 'release')]:
    log = base/('production-debug-final.log' if directory=='debug' else 'production-release.log')
    assert '0 个错误' in log.read_text(encoding='utf-8-sig')
    frozen = base/f'frozen-production-{directory}'
    for f in read(frozen.with_suffix('.json')): assert sha(frozen/f['path'])==f['sha256']
    result['builds'][label] = dict(logSha256=sha(log), assetsSha256=sha(frozen/'assets.json'),
        runtime=read(frozen/'BiliCopilot.UI/bin/BiliCopilot.UI.runtimeconfig.json'),
        uiDllSha256=sha(frozen/'BiliCopilot.UI/bin/BiliCopilot.UI.dll'))
(out/'evidence.json').write_text(json.dumps(result, indent=2))
print(json.dumps(dict(status='PASS', runs=list(result['runs']), suite=counts)))
