"""Read-only verification of sealed candidate/evidence; writes only summary.json."""
from pathlib import Path
from collections import Counter
from datetime import datetime
import hashlib, json, zipfile, xml.etree.ElementTree as ET

D = Path(__file__).resolve().parent
sha = lambda b: hashlib.sha256(b).hexdigest()
decode = lambda b: json.loads(b.decode('utf-8-sig'))
for name, manifest in decode((D/'archives.json').read_bytes()).items():
    assert sha((D/name).read_bytes()) == manifest['sha256'], name
    with zipfile.ZipFile(D/name) as z:
        assert set(z.namelist()) == set(manifest['files'])
        for n, h in manifest['files'].items():
            assert sha(z.read(n)) == h, n

modules = {}
for package in decode((D/'packages.json').read_bytes()):
    p = D/package['path']
    assert sha(p.read_bytes()) == package['sha256'], p
    if p.suffix == '.nupkg':
        with zipfile.ZipFile(p) as z:
            for n in z.namelist():
                if n.endswith('.dll') and Path(n).name.startswith('Danmaku.'):
                    modules[Path(n).stem] = sha(z.read(n))
assert set(modules) == {'Danmaku.Core', 'Danmaku.Legacy', 'Danmaku.Share'}
with zipfile.ZipFile(D/'source-snapshot.zip') as z:
    for assembly in decode(z.read('metadata.json')):
        assert not assembly['diagnosticNames']
        assert modules[Path(assembly['path']).stem] == assembly['sha256'].lower()
        for doc in assembly['documents']:
            assert sha(z.read('source-documents/'+doc['hash']+'.source')) == doc['hash'].lower()

summary = {'candidate': '0.0.1-seek870b.20261101.5', 'runs': {}}
with zipfile.ZipFile(D/'evidence.zip') as z:
    runs = sorted({n.split('/')[0] for n in z.namelist() if n.endswith('/retainedexit.json')})
    for run in runs:
        ex, wer, cleanup = [decode(z.read(run+'/'+n+'.json')) for n in ['retainedexit','wer','cleanup']]
        assert ex['retained'] and ex['exitCode'] == 0 and not ex['forced'], run
        assert wer['applicationLogReadable'] and wer['secondsAfterExit'] >= 31 and not wer['matches'], run
        assert cleanup['remaining'] == 0 and not cleanup['forced'], run
        events = [json.loads(l) for l in z.read(run+'/events.jsonl').decode('utf-8-sig').splitlines()]
        assertions = [e['data'] for e in events if e['data']['kind'] == 'assert']
        metrics = []
        for i, e in enumerate(events):
            d = e['data']
            if d['kind'] not in ['visible-perf', 'hide-during-rebuild', 'long-visible', 'close-during-rebuild']:
                continue
            row = dict(d)
            if d['kind'] == 'visible-perf':
                candidates = [j for j in range(i) if events[j]['data']['kind'] == 'media-command-return'
                              and events[j]['data'].get('position') == 55 and events[j]['data'].get('succeeded')]
                if candidates:
                    j = candidates[-1]
                    progress = next((q for q in events[j+1:i] if q['data']['kind'] == 'progress-vm'
                                     and abs(q['data']['position']-55) < 0.1), None)
                    if progress:
                        row['mediaAcknowledgedToProgressMs'] = (datetime.fromisoformat(progress['at'])-datetime.fromisoformat(events[j]['at'])).total_seconds()*1000
            metrics.append(row)
        summary['runs'][run] = {'assertions': len(assertions), 'failed': [a['name'] for a in assertions if not a['passed']], 'metrics': metrics}
        if run == 'baseline-exact':
            dense = next(m for m in metrics if m['kind']=='visible-perf' and m['engine']=='Win2D' and m['count']==12000)
            assert dense['callMs'] > 1000 and dense['mediaAcknowledgedToProgressMs'] > 1000
            assert 'VisibleMediaSeekResponsive' in summary['runs'][run]['failed']
        if run == 'coverage2':
            assert assertions and all(a['passed'] for a in assertions)
            assert not any(e['data']['kind'].endswith('error') or e['data']['kind']=='unhandled' for e in events)
            counts = Counter(a['name'] for a in assertions)
            for name in ['SlowRollingHistoryIsNotClipped','ReplacedListRejectsOldRebuild','HiddenDenseListNotSubmitted',
                         'HiddenPlaybackClockContinues','SeekBeforeDelayedNetworkReturns','ShowPreservesPause',
                         'NativePauseFreezesPositionAndAge','NativeResumeExpiresShortButKeepsLong']:
                assert counts[name] == 2, name
            visible = [m for m in metrics if m['kind']=='visible-perf']
            assert len(visible) == 4 and {m['count'] for m in visible} == {300,12000}
            for m in visible:
                assert m['callMs'] < 100 and m['gap'] < 250 and m['mediaAcknowledgedToProgressMs'] < 100, m
            closing = [m for m in metrics if m['kind']=='close-during-rebuild']
            assert len(closing)==2 and all(m['pending'] and m['elapsedMs'] < 500 for m in closing)
            loaded = next(e['data']['modules'] for e in events if e['data']['kind']=='loaded-modules')
            assert {m['name']:m['sha'].lower() for m in loaded} == modules
    before, after = [decode(z.read('preparation/protected-'+n+'.json')) for n in ['before','after']]
    assert sorted(before,key=lambda p:p['Name']) == sorted(after,key=lambda p:p['Name'])
    trx = ET.fromstring(z.read('tests/final-regressions.trx'))
    counter = trx.find('.//{*}Counters').attrib
    assert counter['passed']=='44' and counter['failed']=='0' and counter['total']=='44'
    summary['regressions'] = counter
assert 'coverage2' in summary['runs'] and 'baseline-exact' in summary['runs']
(D/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print('Verified candidate hashes/PDB sources, true red, dense/ordinary native green, 44 regressions, protected packages and all owned exits/cleanup.')
