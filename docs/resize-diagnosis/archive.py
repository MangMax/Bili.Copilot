"""Archive original small evidence, excluding deployment layouts and build caches."""
import hashlib, json, pathlib, zipfile
r=pathlib.Path(__file__).resolve().parents[2]
source=r/'artifacts/resize-diagnosis'
dest=pathlib.Path(__file__).parent
files=[]
for p in source.rglob('*'):
    rel=p.relative_to(source)
    if not p.is_file() or any(x in ('layout','bin','obj') for x in rel.parts): continue
    if rel.parts[0]=='host' and p.suffix not in ('.cs','.xaml','.csproj','.props','.json','.png','.appxmanifest'): continue
    if p.suffix in ('.log','.json','.jsonl','.txt','.trx','.png','.cs','.xaml','.csproj','.props','.patch','.appxmanifest'):
        files.append(p)
manifest=[]
with zipfile.ZipFile(dest/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(files):
        raw=p.read_bytes(); name=p.relative_to(source).as_posix()
        z.writestr(name,raw)
        manifest.append(dict(path=name,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
(dest/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
with zipfile.ZipFile(dest/'evidence.zip') as z:
    for f in manifest: assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
print(f'Archived and verified {len(manifest)} original files')
