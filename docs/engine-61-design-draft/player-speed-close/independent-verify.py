"""Read-only evidence audit; run from the fixed candidate checkout root."""
import hashlib
import json
import pathlib
import subprocess
import zipfile
import xml.etree.ElementTree as ET

base = pathlib.Path(__file__).parent
z = zipfile.ZipFile(base / 'evidence.zip')
manifest = json.loads((base / 'evidence-manifest.json').read_text())
assert set(z.namelist()) == set(manifest)
for name, entry in manifest.items():
    data = z.read(name)
    assert len(data) == entry['bytes']
    assert hashlib.sha256(data).hexdigest() == entry['sha256'], name

def read(name):
    return json.loads(z.read(name).decode('utf-8-sig'))

ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
result = {'archive_files_verified': len(manifest), 'trx': {}, 'runtime': {}}
for name, count, failed in [('red/results.trx', 1, 1), ('final2/results.trx', 8, 0), ('suite/suite.trx', 563, 0)]:
    tree = ET.fromstring(z.read(name))
    counters = tree.find('.//t:Counters', ns).attrib
    rows = tree.findall('.//t:UnitTestResult', ns)
    assert len(rows) == count == int(counters['executed'])
    assert sum(row.attrib['outcome'] == 'Failed' for row in rows) == failed == int(counters['failed'])
    result['trx'][name] = counters

metadata = read('consumer-pdb.json')[0]
hashes = {d['hash'].lower() for d in metadata['documents']}
sources = read('source-equality.json')
result['checkout_eol_differences'] = []
for name, value in sources.items():
    data = pathlib.Path(name).read_bytes()
    if hashlib.sha256(data).hexdigest() != value:
        blob = subprocess.check_output(['git', 'show', 'bb9c45dd:' + name.replace('\\', '/')])
        assert hashlib.sha256(blob).hexdigest() == value, name
        assert data.replace(b'\r\n', b'\n') == blob.replace(b'\r\n', b'\n'), name
        result['checkout_eol_differences'].append(name)
    if name.endswith('.cs'):
        assert value in hashes, name
result['source_hashes_matching_candidate'] = len(sources)
result['pdb_metadata_dll_hash'] = metadata['sha256']

for run in ['runtime', 'runtime2']:
    exit_info, wer = read(run + '/retainedexit.json'), read(run + '/wer.json')
    assert exit_info['exitCode'] == 0 and not exit_info['forced']
    assert wer['secondsAfterExit'] >= 31 and wer['applicationLogReadable'] and not wer['matches']
    events = [json.loads(line) for line in z.read(run + '/events.jsonl').decode('utf-8-sig').splitlines()]
    states, closes = [], []
    latest = None
    for event in events:
        data = event['data']
        if data['kind'] == 'state':
            latest = data
            if data.get('speed') in [1.25, 1.5, 1.75] and not any(s['speed'] == data['speed'] and s['state'] == data['state'] for s in states):
                states.append({'at': event['at'], 'speed': data['speed'], 'state': data['state']})
        if data['kind'] == 'speed-close-enter':
            closes.append({'at': event['at'], 'pending': data['pending'], 'preceding_state': latest})
    assert any(e['data']['kind'] == 'speed-close-done' and e['data']['disposed'] and e['data']['completed'] for e in events)
    assert any(c['pending'] == 20 for c in closes)
    assert sum(e['data']['kind'] == 'initialized' for e in events) >= 2
    assert any(e['data']['kind'] == 'visual' and not e['data']['panel'] for e in events)
    assert not any(e['data']['kind'] in ['unhandled', 'startup-error', 'action-error'] for e in events)
    result['runtime'][run] = {'exit': exit_info, 'wer': wer, 'close': closes, 'speed_states': states}
print(json.dumps(result, indent=2))
