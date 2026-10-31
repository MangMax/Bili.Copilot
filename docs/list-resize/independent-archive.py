"""Package small review evidence, never package layouts or executable payloads."""
from pathlib import Path
import hashlib,json,zipfile
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-review'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for stage in ('baseline-two-pane','candidate-two-pane','candidate-image-checks','baseline-user50','candidate-user50'):
    d=a/stage
    files=[p for p in (d/'layout').glob('*') if p.name in ('Harness.exe','Harness.dll','Richasy.WinUIKernel.Share.dll','Microsoft.WinUI.dll')]
    (d/'binary-hashes.json').write_text(json.dumps([{'file':p.name,'sha256':sha(p)} for p in files],indent=2))
host=a/'host'
(a/'review-provenance.json').write_text(json.dumps({'candidate':'1797c94aa7c6fc54226bb4ea8f0efef90a49f5bd','parent':'90e215f0d303c7ae6487f1e1cbf261f677b3d1ef','identity':'BiliCopilot.ListReview3301.Owned','kernel':'0ae5e4e000a41a07f6696746706df11c06e8835b','kernelInitialPatchSha256':sha(a/'kernel-initial.patch'),'assets':{p.name:sha(p) for p in (host/'Assets').iterdir() if p.is_file()},'note':'No original stage DLL attestation is inferred from inherited probe-provenance.json.'},indent=2))
files=[]
for p in a.rglob('*'):
    if not p.is_file() or any(part in ('layout','bin','obj','.winapp') for part in p.relative_to(a).parts):continue
    if p.name=='kernel-initial.patch':continue
    if p.suffix.lower() in ('.json','.jsonl','.txt','.log','.png','.cs','.xaml','.csproj','.props','.manifest','.appxmanifest','.html'):
        files.append(p)
manifest=[]
dest=r/'docs/list-resize/independent-evidence.zip'
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(files):
        name=p.relative_to(r/'artifacts').as_posix();data=p.read_bytes();z.writestr(name,data)
        manifest.append({'path':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    z.writestr('manifest.json',json.dumps(manifest,indent=2))
with zipfile.ZipFile(dest) as z:
    for entry in json.loads(z.read('manifest.json')):
        data=z.read(entry['path']);assert len(data)==entry['bytes'] and hashlib.sha256(data).hexdigest()==entry['sha256']
print(len(files),'entries verified;',dest.stat().st_size,'bytes')
