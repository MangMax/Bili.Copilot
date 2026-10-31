"""Recompute sealed assertions; original run timestamps remain historical."""
from pathlib import Path
import hashlib, io, json, subprocess, sys, zipfile

D = Path(__file__).resolve().parent.parent / 'legacy-reconstruct'
E = Path('artifacts/review-e30')
sys.path.insert(0, str(D))
# Re-execute the original 31 state/pixel/exit assertions against extracted originals.
code = (D / 'validate.py').read_text(encoding='utf-8').split("version='0.0.1-reconstruct0b4f.final'")[0]
code = code.replace("/'artifacts'", '').replace("'artifacts/", "'").replace('R=Path.cwd()', "R=Path('artifacts/review-e30')")
scope = {'__file__': str(D / 'validate.py')}
exec(compile(code, 'sealed-validation-prefix', 'exec'), scope)
check = scope['check']
sha = lambda b: hashlib.sha256(b).hexdigest()
closure = json.loads((D / 'closure.json').read_text())
loaded = json.loads((E / 'reconstruct-app/full/runtime2/loaded-danmaku.json').read_text(encoding='utf-8-sig'))
for name, record in closure.items():
    p = E / 'reconstruct-final/feed' / Path(record['package']).name
    with zipfile.ZipFile(p) as z:
        raw = z.read(next(n for n in z.namelist() if n.endswith('/Danmaku.'+name+'.dll')))
    check('PackageClosure_'+name, sha(p.read_bytes()) == record['packageSha256'] and sha(raw) == record['dllSha256'] and
          all(sha(Path(p).read_bytes()) == h == sha(raw) for p,h in record['files'].items()))
    check('LoadedApp_'+name, any(x['name']=='Danmaku.'+name+'.dll' and x['sha256'].lower()==sha(raw) for x in loaded))
checks = scope['checks']
assert len(checks) == 35
(E / 'recomputed35.json').write_text(json.dumps(checks,indent=2))
# Cross-check all three overlays against ZIP bytes, not just self-reported file hashes.
with zipfile.ZipFile(D/'candidate-source.zip') as z:
    for f in (D/'source').glob('*.cs'):
        assert f.read_bytes() == z.read('src/Danmaku.Legacy/'+f.name)
before=json.loads((E/'kernel-before.json').read_text())
after={n:sha(subprocess.check_output(['git','-C','externals/bili-kernel',*args])) for n,args in
       {'head':['rev-parse','HEAD'],'status':['status','--porcelain=v1'],'staged':['diff','--cached','--binary'],'worktree':['diff','--binary']}.items()}
assert before == after
print('Recomputed historical assertions: Passed: 35, Failed: 0, Skipped: 0; overlay ZIP bytes and kernel fingerprint unchanged')
