"""Freeze small integration receipts; keep binaries/binlog in local artifacts."""
from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[3]
base=root/'artifacts/renderer-fault-integration'
output=Path(__file__).with_name('evidence.zip')
if output.exists():raise SystemExit('Refuse to overwrite existing evidence')
files=list(base.glob('*.json'))+list(base.glob('*.log'))
for stage in ('Panel','PanelFresh','Full','Release','RuntimeGate'):
 d=base/stage
 files+=list(d.glob('*.json'))+list(d.glob('*.log'))+list(d.glob('trx/*.trx'))
files+=[root/'artifacts/searchbox-properties-integration/Metadata/Program.cs',root/'artifacts/searchbox-properties-integration/Metadata/Metadata.csproj']
manifest={}
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(set(files)):
  b=p.read_bytes();name=p.relative_to(root).as_posix()
  manifest[name]={'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()};z.writestr(name,b)
 z.writestr('manifest.json',json.dumps(manifest,indent=2))
with zipfile.ZipFile(output) as z:
 for n,r in manifest.items():
  b=z.read(n);assert len(b)==r['bytes'] and hashlib.sha256(b).hexdigest()==r['sha256']
print(json.dumps({'entries':len(manifest),'bytes':output.stat().st_size,'sha256':hashlib.sha256(output.read_bytes()).hexdigest()}))
