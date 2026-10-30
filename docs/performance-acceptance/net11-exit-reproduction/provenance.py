"""Source/package closure and byte hashes, without business data."""
import gzip
import hashlib
import json
import pathlib
import subprocess

base = pathlib.Path('artifacts/exit-reproduction')
fixture = base / 'fixture'
source = base / 'source/src/Desktop/BiliCopilot.UI'
def sha(data):
    return hashlib.sha256(data).hexdigest()
rows = []
for p in source.rglob('*.cs'):
    if 'obj' in p.relative_to(source).parts or 'bin' in p.relative_to(source).parts:
        continue
    q = fixture / p.relative_to(source)
    a, b = p.read_bytes(), q.read_bytes()
    try:
        equal = a.decode('utf-8-sig').replace('\r\n', '\n').rstrip('\n') == b.decode('utf-8-sig').replace('\r\n', '\n').rstrip('\n')
    except UnicodeDecodeError:
        equal = a == b
    rows.append(dict(path=p.relative_to(source).as_posix(), source=sha(a), fixture=sha(b), normalizedEqual=equal))
expected = {'App.xaml.cs', 'GlobalDependencies.cs', 'Forms/MainWindow.xaml.cs', 'Forms/PlayerWindow.cs',
            'Toolkits/FontCatalog.cs', 'Toolkits/Pure/TrayLifecycle.cs'}
assert {x['path'] for x in rows if not x['normalizedEqual']} == expected
(base / 'source-comparison.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', 'e55180', 'docs/performance-acceptance/net11-primary']).decode().splitlines()
old = next(p for p in paths if 'player-fixture/obj/project.assets.json.gz' in p)
a = json.loads(gzip.decompress(subprocess.check_output(['git', 'show', 'e55180:' + old])))
b = json.loads((fixture / 'obj/project.assets.json').read_text(encoding='utf-8-sig'))
def packages(graph):
    return {k: v.get('sha512') for k, v in graph['libraries'].items() if v['type'] == 'package'}
x, y = packages(a), packages(b)
result = dict(original=old, originalPackages=len(x), diagnosticPackages=len(y),
              missing=sorted(x.keys() - y.keys()), extra=sorted(y.keys() - x.keys()),
              different=[k for k in x.keys() & y.keys() if x[k] != y[k]])
assert x == y
(base / 'package-comparison.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
ledger = json.loads((fixture / 'frozen-all-files.json').read_text())
for item in ledger:
    p = fixture / 'frozen-output' / item['path']
    with p.open('rb') as file:
        assert hashlib.file_digest(file, 'sha256').hexdigest() == item['sha256'], item['path']
print(json.dumps(dict(productionCs=len(rows), normalizedEqual=sum(x['normalizedEqual'] for x in rows), packages=len(y), frozenFiles=len(ledger))))
