"""Offline independent receipt replay; never launches or changes an app.
Usage: python <this file> <extracted archive root>
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

root = Path(sys.argv[1])
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
manifest = read(root / 'manifest.json')
for name, info in manifest.items():
    data = (root / name).read_bytes()
    assert len(data) == info['bytes']
    assert hashlib.sha256(data).hexdigest() == info['sha256'], name
print('Manifest payloads verified:', len(manifest))
for name in ('real-article05', 'real-article2', 'fixed-article05', 'fixed-article2'):
    p = subprocess.run([sys.executable, str(root / 'replay/check.py'), str(root / 'runs' / name)], capture_output=True, text=True)
    d = json.loads(p.stdout)
    failed = [c['name'] for c in d['checks'] if not c['passed']]
    expected = [f'AllObservedVisibleIdentitiesIncludingNull_{n}' for n in (50, 500)] if name.startswith('real-') else []
    assert set(failed) == set(expected), (name, p.stderr, failed)
    assert p.returncode == (2 if expected else 0)
    print(name, 'checks', len(d['checks']), 'expected failures', failed)
    role_bad = observed = 0
    for count in (50, 500):
        raw = read(root / 'runs' / name / f'matrix-continuity-{count}.json')
        for sample in raw['samples']:
            for row in sample['rows']:
                observed += 1
                images = row['images']
                role_bad += not (len(images) == 2 and images[0]['source'].endswith(f'/article-{row["index"]}.png') and images[1]['source'].endswith(f'/article-avatar-{row["index"]}.png'))
    assert (role_bad > 0) == bool(expected)
    print(name, 'role-specific cover/avatar rows:', observed, 'bad:', role_bad)
ledger = {(r['stage'], r['count']): r for r in read(root / 'receipts/summary.json')}
stages = sorted({k[0] for k in ledger})
total = 0
for name in stages:
    stage = root / 'runs' / name
    if not (stage / 'video-pixels.json').exists():
        continue
    rows = read(stage / 'video-pixels.json')
    for r in rows:
        data = (stage / 'pixel-grids' / r['raw']['grid']).read_bytes()
        fraction = sum(max(rgb) < 6 for rgb in zip(data[::3], data[1::3], data[2::3])) / (len(data) / 3)
        assert abs(fraction - r['background']) < 1e-12
        total += 1
    output = subprocess.check_output([sys.executable, str(root / 'replay/summarize.py'), str(stage)], text=True)
    for line in output.splitlines():
        actual = json.loads(line)
        expected = ledger[name, actual['count']]
        for key in ('blankMs', 'blankMaxMs', 'pixelSamples', 'maxRealized', 'bindingBadFrames', 'resizeMax'):
            assert actual[key] == expected[key], (name, key)
print('All pixel classifications and duration summaries matched; samples:', total)
print('Scope: offline archive replay only; no native rerun, published AOT test, or lost Temp recovery.')
