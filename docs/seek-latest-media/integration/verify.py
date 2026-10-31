"""Verify the sealed integration evidence without building or launching the app."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

d = Path(__file__).resolve().parent
sha = lambda data: hashlib.sha256(data).hexdigest()
manifest = json.loads((d / 'evidence-manifest.json').read_text(encoding='utf-8'))
archive = d / 'evidence.zip'
assert sha(archive.read_bytes()) == manifest['sha256']
with zipfile.ZipFile(archive) as z:
    assert set(z.namelist()) == set(manifest['files'])
    for name, expected in manifest['files'].items():
        assert sha(z.read(name)) == expected, name
    read = lambda name: json.loads(z.read(name).decode('utf-8-sig'))
    for stage in ['restore-mapped', 'debug', 'release', 'runtime-gate', 'runtime-gate-release', 'full', 'review']:
        assert read(stage + '-command.json')['exit'] == 0, stage
    for name, count in [('full', 572), ('review', 3)]:
        counters = ET.fromstring(z.read('tests/' + name + '.trx')).find(
            '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}ResultSummary/'
            '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
        assert tuple(int(counters[k]) for k in ['total', 'passed', 'failed', 'notExecuted']) == (count, count, 0, 0)
        print(name, 'Passed:', count, 'Failed: 0, Skipped: 0')
    closure = read('package-verification.json')
    assert sha(z.read('app-project.assets.json')) == closure['assetsSha256']
    assets = read('app-project.assets.json')
    for package in closure['packages']:
        name, version = package['name'], package['version']
        assert [k for k in assets['libraries'] if k.startswith(name + '/')] == [name + '/' + version]
        for configuration in ['debug', 'release']:
            output = next(x for x in read(configuration + '-output.json') if x['path'].endswith(name + '.dll'))
            assert output['sha256'] == package['dllSha256']
            assert name + '/' + version in read(configuration + '-BiliCopilot.UI.deps.json')['libraries']
    state = read('integration-state.json')
    assert state['productionMatchesReview'] and state['speedMatchesParent'] and state['noWarnUnchanged']
    assert state['kernelHead'] == '0ae5e4e000a41a07f6696746706df11c06e8835b' and not state['kernelStatus']
print('PASS: sealed evidence, fresh commands/tests, unique package graph, both output hash records, integration boundaries')
