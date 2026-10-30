"""Independent read-only evidence checks; no builds, UI or process operations."""
import gzip
import hashlib
import json
import pathlib
import statistics
import sys
import xml.etree.ElementTree as ET

here = pathlib.Path(__file__).resolve().parent
source = pathlib.Path(sys.argv[1]).resolve()
evidence = here / 'evidence'
def read(name):
    return json.loads(gzip.decompress((evidence / (name + '.gz')).read_bytes()).decode('utf-8-sig'))
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

closures = json.loads(gzip.decompress((here / 'closures.json.gz').read_bytes()))
result = {'candidate': '79ffd4f1b5ef6898b18426b9159f9342c3483450', 'sourceCheckout': str(source), 'arms': {}, 'runs': []}
for arm, record in closures.items():
    fixture = source / 'artifacts/net10-adaptation' / arm
    frozen = fixture / 'frozen-output'
    expected = {x['path'].replace('\\', '/').lower(): x['sha256'] for x in record['frozenFiles']}
    actual = {x.relative_to(frozen).as_posix().lower(): sha(x) for x in frozen.rglob('*') if x.is_file()}
    assert actual == expected, arm
    assert not list(frozen.rglob('*.appxrecipe'))
    sources = {}
    for f in fixture.rglob('*.cs'):
        rel = f.relative_to(fixture)
        if any(p in ('bin', 'obj', 'frozen-output') for p in rel.parts):
            continue
        raw = f.read_bytes().replace(b'N854B', b'N854X').replace(b'N854C', b'N854X').replace(b'N854F', b'N854X').replace(b'\r\n', b'\n')
        sources[str(rel)] = hashlib.sha256(raw).hexdigest()
    assert sources == record['normalizedSources'], arm
    graph = json.loads(gzip.decompress((here / (arm + '-assets.json.gz')).read_bytes()).decode('utf-8-sig'))
    packages = {k: dict(sha512=v.get('sha512'), type=v['type']) for k, v in graph['libraries'].items()}
    assert packages == record['packages']
    idle = (fixture / 'IdleBaseline.cs').read_text(encoding='utf-8-sig')
    assert all(s not in idle for s in ['Process.GetCurrentProcess', 'process.Refresh', 'process.Threads', 'GetProcessInfos'])
    result['arms'][arm] = {'allFiles': len(actual), 'dlls': sum(k.endswith('.dll') for k in actual), 'sourceFiles': len(sources), 'runtime': record['runtime']['runtimeOptions']['includedFrameworks']}
assert closures['player9common']['normalizedSources'] == closures['player10common']['normalizedSources'] == closures['player10final']['normalizedSources']
for run in ['r1-net9','r2-net10','r3-net9','r4-net10','r5-net9','final-net10','v3-r1-net9','v3-r2-net10','v3-r3-net9','v3-r4-net10']:
    provenance = read(run + '/provenance.json')
    arm = pathlib.PureWindowsPath(provenance['fixture']).name
    expected = {x['path'].lower() + '=' + x['sha256'] for x in closures[arm]['frozenFiles'] if x['path'].lower().endswith('.dll')}
    assert set(provenance['dlls']) == expected, run
    layout = source / 'artifacts/net10-adaptation' / run / 'layout'
    actual = {str(f.relative_to(layout)).lower() + '=' + sha(f) for f in layout.rglob('*.dll')}
    assert actual == expected, run
    modules = read(run + '/modules.json')
    core = next(x for x in modules if x['name'].lower() == 'coreclr.dll')
    assert 'coreclr.dll=' + core['sha256'] in expected
    entry = {'run': run, 'arm': arm, 'dlls': len(actual), 'coreclr': core, 'runtime': provenance['runtime']['runtimeOptions']['includedFrameworks']}
    if run == 'r3-net9':
        assert read(run + '/failure.json')['accepted'] is False
    else:
        baseline = read(run + '/idle-baseline.json')
        entry['phases'] = {}
        for phase in ['visible', 'hidden', 'restored']:
            rows = [r for r in baseline['rows'] if r['phase'] == phase]
            assert [r['sample'] for r in rows] == list(range(61))
            def rate(a, b):
                return (rows[b]['cpuMilliseconds'] - rows[a]['cpuMilliseconds']) / (rows[b]['monotonicSeconds'] - rows[a]['monotonicSeconds'])
            entry['phases'][phase] = {'full': rate(0,60), 'firstHalf': rate(0,30), 'lastHalf': rate(30,60), 'privateMiBMedian': statistics.median(r['privateBytes'] for r in rows)/1048576}
        entry['observerMs'] = {'median': statistics.median(baseline['observerSampleSeconds'])*1000, 'max': max(baseline['observerSampleSeconds'])*1000}
        events = [json.loads(l) for l in gzip.decompress((evidence / (run + '/main/events.jsonl.gz')).read_bytes()).decode('utf-8-sig').splitlines() if l.strip()]
        assert any(e['kind'] == 'idle-cycles' and e['data']['count'] == 20 and e['data']['sameHwnd'] for e in events)
        font = read(run + '/main/font-results.json')
        assert font['status'] == 'PASS' and font['before'] == 0 and font['count'] > 0
        exit_record = read(run + '/main/exit.json')
        assert exit_record['explicitExit'] and exit_record['unhandledCount'] == 0
    result['runs'].append(entry)
summary = json.loads((here / 'summary.json').read_text())
for published in summary:
    checked = next(r for r in result['runs'] if r['run'] == published['run'])
    for phase, values in published['phases'].items():
        assert abs(checked['phases'][phase]['full'] - values['cpuMsPerSecond']) < 1e-10

metadata = json.loads((here / 'package-metadata.json').read_text(encoding='utf-8-sig'))
covered = {(r.replace('\\','/'), p['id']) for p in metadata for r in p['references']}
missing = []
for root in ['src', 'tests', 'externals/bili-kernel/src']:
    for f in (source/root).rglob('*'):
        if f.suffix not in ['.csproj','.props'] or any(x in f.parts for x in ['obj','bin','_build','Samples']): continue
        for item in ET.parse(f).getroot().iter('PackageReference'):
            name = item.get('Include')
            if name and (f.relative_to(source).as_posix(), name) not in covered:
                missing.append([f.relative_to(source).as_posix(), name])
assert not missing, missing
result['packageCoverage'] = {'metadataRows': len(metadata), 'uncoveredActualReferences': missing}
# These are current read-only snapshots, not hashes recorded by the original build.
result['currentProductionSnapshots'] = []
for configuration, project in [('Debug','BiliCopilot.UI'),('Release','BiliCopilot.UI'),('Release','BiliCopilot.Visor')]:
    folder = source / 'src/_build/x64' / configuration / project / 'bin'
    for name in [project+'.dll', project+'.runtimeconfig.json', 'coreclr.dll']:
        f = folder/name
        if f.exists():
            item = {'path': str(f), 'sha256': sha(f), 'bytes': f.stat().st_size}
            if name.endswith('.json'): item['runtime'] = json.loads(f.read_text(encoding='utf-8-sig'))
            result['currentProductionSnapshots'].append(item)
print(json.dumps(result, indent=2))
