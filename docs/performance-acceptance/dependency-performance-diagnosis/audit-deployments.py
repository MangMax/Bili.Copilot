"""After sampling, audit BOTH directions, including unexpected deployed DLLs."""
import hashlib, json, pathlib
root=pathlib.Path(__file__).resolve().parents[3]
data=root/'artifacts/dependency-performance-diagnosis'
runs=['d34-g1r-1-a','d34-g1r-2-b','d34-g1r-3-b','d34-g1r-4-a',
      'd34-g2-1-b','d34-g2-2-c','d34-g2-3-c','d34-g2-4-b']
targets=[(run,data/f'player-{run[-1]}',data/f'layout-{run}') for run in runs]
targets += [(f'controls-{arm}',data/f'controls-{arm}',data/f'controls-{arm}-layout') for arm in ('b','c')]
results=[]
for name,fixture,layout in targets:
    frozen=json.loads((fixture/'frozen-all-files.json').read_text(encoding='utf-8-sig'))
    expected={r['path'].replace('\\','/').lower():r['sha256'].lower() for r in frozen if r['path'].lower().endswith('.dll')}
    actual={p.relative_to(layout).as_posix().lower():p for p in layout.rglob('*') if p.is_file() and p.suffix.lower()=='.dll'}
    extra=sorted(actual.keys()-expected.keys());missing=sorted(expected.keys()-actual.keys());mismatches=[]
    for rel in actual.keys() & expected.keys():
        with actual[rel].open('rb') as f: sha=hashlib.file_digest(f,'sha256').hexdigest()
        if sha!=expected[rel]: mismatches.append(dict(path=rel,expected=expected[rel],actual=sha))
    results.append(dict(run=name,dlls=len(actual),extra=extra,missing=missing,mismatches=mismatches))
(data/'final-deployment-audit.json').write_text(json.dumps(results,indent=2))
assert all(not r['extra'] and not r['missing'] and not r['mismatches'] for r in results)
print(f'PASS: {len(results)} layouts, {sum(r["dlls"] for r in results)} DLLs, no missing/extra/mismatched DLLs')
