from pathlib import Path
import hashlib
import json
import zipfile

base = Path('artifacts/engine49-trace')
dest = Path('docs/performance-acceptance/engine-49-trace/evidence')
sha = '4d0f8691f08f1e143294402477e72306a1b38b6f'
digest = lambda data: hashlib.sha256(data).hexdigest()
sources = {}
for variant in ['original', 'candidate', 'trace']:
    root = base / variant / ('danmaku-kernel-' + sha)
    replay = Path('artifacts/engine49-trace-replay-prepared-3') / variant / root.name
    files = {}
    for p in sorted(root.rglob('*')):
        rel = p.relative_to(root)
        if p.is_file() and not {'bin', 'obj'}.intersection(rel.parts):
            files[rel.as_posix()] = digest(p.read_bytes())
            assert digest((replay / rel).read_bytes()) == files[rel.as_posix()], rel
    sources[variant] = dict(files=files, manifestSha256=digest(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()), replayExact=True)
sources['candidateChanges'] = [p for p in sources['original']['files'] if sources['original']['files'][p] != sources['candidate']['files'][p]]
sources['traceChanges'] = [p for p in sources['trace']['files'] if sources['candidate']['files'].get(p) != sources['trace']['files'][p]]
(dest / 'source-hashes.json').write_text(json.dumps(sources, indent=2), encoding='utf-8')
artifacts = []
for p in sorted(base.rglob('*')):
    if not p.is_file(): continue
    rel = p.relative_to(base)
    if p.suffix in ['.png', '.nupkg'] and 'packages' not in rel.parts or (p.name.startswith('Danmaku.') and p.suffix == '.dll' and ('layout' in rel.parts or ('bin' in rel.parts and 'ref' not in rel.parts))):
        artifacts.append(dict(path=str(p.resolve()), bytes=p.stat().st_size, sha256=digest(p.read_bytes())))
for p in sorted((base / 'feed').glob('*.nupkg')):
    with zipfile.ZipFile(p) as z:
        for name in z.namelist():
            if name.endswith('.dll'):
                data = z.read(name)
                runtime = (base / 'layout' / Path(name).name).read_bytes()
                assert data == runtime, name
                artifacts.append(dict(package=str(p), entry=name, sha256=digest(data), equalsRuntimeLayout=True))
(dest / 'artifact-hashes.json').write_text(json.dumps(artifacts, indent=2), encoding='utf-8')
print(json.dumps({k:v for k,v in sources.items() if k.endswith('Changes')}, indent=2))
print('All prepared source files replay byte-exact; all package DLLs equal runtime layout.')
