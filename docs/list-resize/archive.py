"""Archive source, logs, assertions and raw samples, excluding binaries/package layouts."""
from pathlib import Path
import hashlib,json,zipfile
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-resize';out=r/'docs/list-resize/evidence.zip'
files=[]
for p in a.rglob('*'):
    if not p.is_file() or any(part in ('layout','bin','obj','.winapp') for part in p.relative_to(a).parts):continue
    if p.suffix.lower() in ('.json','.jsonl','.txt','.log','.png','.cs','.xaml','.csproj','.props','.patch','.trx','.py','.ps1'):
        files.append(p)
for stage in ('common-red-hot','common-cache-probe','shared-final','shared-pixel-diagnostic','shared-verified'):
    for p in (r/'artifacts/resize-diagnosis'/stage).iterdir():
        if p.is_file():files.append(p)
manifest=[]
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(files):
        name=p.relative_to(r/'artifacts').as_posix();data=p.read_bytes();z.writestr(name,data)
        manifest.append({'path':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    z.writestr('evidence-manifest.json',json.dumps(manifest,indent=2))
print(f'{len(files)} files, {out.stat().st_size} bytes: {out}')
