"""Verify sealed integration evidence without rebuilding, restoring or launching UI."""
from pathlib import Path
import hashlib
import json
import zipfile

directory = Path(__file__).resolve().parent
sha = lambda b: hashlib.sha256(b).hexdigest()
prefix = 'artifacts/seek-responsive-integration/'
with zipfile.ZipFile(directory/'evidence.zip') as z:
    manifest = json.loads(z.read('manifest.json'))
    assert set(z.namelist()) == set(manifest) | {'manifest.json'}
    for name, info in manifest.items():
        data = z.read(name)
        assert len(data) == info['bytes'] and sha(data) == info['sha256'], name
    def read(name):
        return json.loads(z.read(prefix+name))
    for stage in ('Restore', 'Tests', 'Debug', 'Release', 'RuntimeGate', 'DebugRuntimeGate'):
        assert read(stage+'/provenance.json')['exitCode'] == 0
    counts = read('Tests/provenance.json')['counters']
    assert (counts['total'], counts['passed'], counts['failed'], counts['notExecuted']) == ('577', '577', '0', '0')
    packages = read('package-verification.json')
    assert sha(z.read(prefix+'app-project.assets.json')) == packages['assetsSha256']
    assets = read('app-project.assets.json')
    for row in packages['packages']:
        key = row['name']+'/'+row['version']
        assert [k for k in assets['libraries'] if k.startswith(row['name']+'/')] == [key]
        for config in ('Debug', 'Release'):
            assert key in read(config+'/BiliCopilot.UI.deps.json')['libraries']
            record = read(config+'/provenance.json')
            matching = [h for p, h in record['outputs'].items() if p.endswith('/'+row['name']+'.dll') or p.endswith('\\'+row['name']+'.dll')]
            assert matching == [row['dllSha256']]
    closure = read('Closure/summary.json')
    assert closure['dllPdbPairs'] == 3 and closure['productionDocumentsMatchedToBundle'] == 37
print('PASS sealed evidence; Passed: 577, Failed: 0, Skipped: 0; isolated restore, both JIT outputs/gates, source closure')
