"""Read-only verification of the sealed evidence and the reviewed source tree."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

d = Path(__file__).resolve().parent
r = d.parent.parent
ledger = json.loads((d / 'evidence-manifest.json').read_text(encoding='utf8'))
sha = lambda b: hashlib.sha256(b).hexdigest()
assert sha((d / 'evidence.zip').read_bytes()) == ledger['sha256']
with zipfile.ZipFile(d / 'evidence.zip') as z:
    assert set(z.namelist()) == set(ledger['files'])
    for name, expected in ledger['files'].items():
        assert sha(z.read(name)) == expected, name
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    for name, total, passed, failed in [('red.trx', 26, 24, 2), ('verified-final.trx', 37, 37, 0)]:
        counters = ET.fromstring(z.read('tests/' + name)).find('t:ResultSummary/t:Counters', ns).attrib
        assert (int(counters['total']), int(counters['passed']), int(counters['failed'])) == (total, passed, failed)
    packages = json.loads((d / 'package-closure.json').read_text(encoding='utf8'))['packages']
    for run in ['runtime1', 'runtime2', 'runtime3']:
        read = lambda n: json.loads(z.read(run + '/' + n).decode('utf-8-sig'))
        identity, ex, wer, cleanup = [read(n) for n in ['identity.json', 'retainedexit.json', 'wer.json', 'cleanup.json']]
        assert ex['retained'] and not ex['forced'] and ex['exitCode'] == 0
        assert ex['pid'] == identity['pid'] and ex['path'] == identity['path']
        assert wer['secondsAfterExit'] >= 31 and wer['applicationLogReadable'] and not wer['matches']
        assert cleanup['remaining'] == 0 and cleanup['identity'] == 'Bili.LatestA1de'
        assert read('yield.json')['exit'] == 0
        results = read('state-results.json')
        assert all(x['pass_'] for x in results)
        if run == 'runtime1':
            assert len(results) == 46
            for engine in ['Core', 'Legacy']:
                assert {f'{engine}Latest{i}' for i in range(10)} <= {x['name'] for x in results}
        events = [json.loads(line) for line in z.read(run + '/events.jsonl').decode('utf-8-sig').splitlines()]
        loaded = next(x['data']['modules'] for x in events if x['data']['kind'] == 'loaded-modules')
        for pkg in packages:
            module = next(m for m in loaded if m['name'] == 'Danmaku.' + pkg['name'])
            assert module['sha'].lower() == pkg['dllSha'] and module['pdbSha'].lower() == pkg['pdbSha']
            assert str(Path(identity['path']).parent).lower() == str(Path(module['path']).parent).lower()
    # The second Legacy capture has no intervening media write/control action.
    commands = json.loads(z.read('runtime3/commands.json'))
    shots = [i for i, c in enumerate(commands) if 'screenshot' in c['command']]
    assert len(shots) == 2
    assert all('invoke' not in c['command'] for c in commands[shots[0] + 1:shots[1]])
for entry in json.loads((d / 'app-source-closure.json').read_text(encoding='utf8'))['files']:
    source = (r / entry['path']).read_bytes()
    assert sha(source) == entry['sourceSha'] or sha(source.replace(b'\r\n', b'\n')) == entry['sourceLfSha'], entry['path']
assert json.loads((d / 'protected-identities.json').read_text(encoding='utf8'))['unchanged']
kernel = json.loads((d / 'kernel-unchanged.json').read_text(encoding='utf8'))
assert all(kernel[n]['equal'] for n in ['head', 'status', 'staged.patch', 'worktree.patch'])
assert '0ae5e4e' in kernel['gitlink']
print('PASS: sealed hashes, controlled red, 37 tests, bounded desktop states, loaded closure, retained exits, WER, cleanup, source')
print('Pixel interpretation is manual: see README; discarded frames are not visual PASS.')
