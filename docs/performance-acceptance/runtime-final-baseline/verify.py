"""Portable bytes and optional retained live layout verification; read-only."""
import gzip, hashlib, json, pathlib, sys, subprocess
from prepare import HERE, BASE, ROOT, digest, ledger
from gate import read, check

items=read(HERE/'ledger.json')
tracked=set(subprocess.check_output(['git','ls-files'],cwd=ROOT,text=True).splitlines()) if '--tracked' in sys.argv else None
for item in items:
    p=HERE/item.get('archive',item.get('image'))
    if tracked is not None: assert p.relative_to(ROOT).as_posix() in tracked,item['path']
    data=gzip.decompress(p.read_bytes()) if 'archive' in item else p.read_bytes()
    assert len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256'],item['path']
result={'portableFiles':len(items),'liveRuns':[]}
if '--live' in sys.argv:
    for arm in ('net10','net11'):
        assert ledger(BASE/arm/'fixture/frozen-output')==read(BASE/arm/'frozen-files.json')
    for run,arm in [('a1-net10','net10'),('b1-net11','net11'),('b2-net11','net11'),('a2-net10','net10')]:
        folder=BASE/run
        if not (folder/'accepted.json').exists():continue
        check(folder)
        expected={r['path'].lower():r['sha256'] for r in read(BASE/arm/'frozen-files.json') if r['path'].lower().endswith('.dll')}
        actual={p.relative_to(folder/'layout').as_posix().lower():digest(p) for p in (folder/'layout').rglob('*.dll')}
        assert expected==actual,run
        modules=read(folder/'modules.json')
        local=[]
        for m in modules:
            p=pathlib.Path(m['path'])
            if p.is_relative_to(folder/'layout'):
                assert digest(p).lower()==m['sha256'].lower(),m['name'];local.append(m)
        clr=next(m for m in local if m['name'].lower()=='coreclr.dll')
        assert clr['sha256'].lower()==digest(BASE/arm/'fixture/frozen-output/coreclr.dll')
        result['liveRuns'].append({'run':run,'dlls':len(actual),'loadedLocalModules':len(local),'coreclr':clr})
print(json.dumps(result,indent=2))
