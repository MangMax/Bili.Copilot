import pathlib,hashlib,json,zipfile
r=pathlib.Path(__file__).resolve().parents[2]; src=r/'artifacts/resize-review'; dst=r/'docs/resize-review'
files=[]
for p in src.iterdir():
    if p.is_file() and p.suffix in ('.json','.log') and not p.name.startswith('kernel-initial'): files.append(p)
for stage in ('run','run-final','run-verified'):
    files.extend(p for p in (src/stage).iterdir() if p.is_file())
manifest=[]
with zipfile.ZipFile(dst/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(files):
        b=p.read_bytes(); name=p.relative_to(src).as_posix();z.writestr(name,b)
        manifest.append(dict(path=name,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(dst/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
with zipfile.ZipFile(dst/'evidence.zip') as z:
    for f in manifest: assert hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
print('Verified',len(manifest),'review artifacts')
