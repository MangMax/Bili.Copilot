"""Archive raw evidence and sources, including adverse attempts, never executable layouts."""
from pathlib import Path
import hashlib,json,subprocess,zipfile
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-p1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
controls=r/'src/Desktop/BiliCopilot.UI/Controls'
production=[controls/n for n in ('BasicCoverImage.cs','BlurCoverImage.cs','ReloadableCoverImage.cs','RepeaterCachePolicy.cs')]
production += [r/'src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml',r/'src/Desktop/BiliCopilot.UI/Controls/Message/NotifyMessageCardControl.xaml']
(a/'provenance.json').write_text(json.dumps({
    'parentBeforeFinalEvidenceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),
    'fixedReview':'f81fa979','reviewedCandidate':'1797c94a','baseline':'90e215f0',
    'productionSourceSha256':{p.relative_to(r).as_posix():sha(p) for p in production},
    'kernel':subprocess.check_output(['git','-C','externals/bili-kernel','rev-parse','HEAD'],cwd=r,text=True).strip(),
    'protectedInitialPatches':{p.name:sha(p) for p in (a/'initial').glob('*.patch')},
    'note':'Early retained-layout hashes are post-run file hashes; start-of-run binary-hashes.json is preserved where collected. No executable payload, dump, or external process memory is archived.'
},indent=2))
for stage in a.iterdir():
    if not stage.is_dir() or not (stage/'layout').exists():continue
    if not (stage/'binary-hashes.json').exists():
        (stage/'retained-layout-hashes.json').write_text(json.dumps({p.name:sha(p) for p in (stage/'layout').iterdir() if p.name in ('Harness.dll','Harness.exe','Richasy.WinUIKernel.Share.dll','Microsoft.WinUI.dll')},indent=2))
files=[]
for p in a.rglob('*'):
    if not p.is_file():continue
    rel=p.relative_to(a)
    if any(part in ('layout','bin','obj','.winapp','dependency-source') for part in rel.parts):continue
    if p.suffix.lower() in ('.json','.jsonl','.txt','.log','.png','.jpg','.webp','.avif','.cs','.xaml','.csproj','.props','.manifest','.appxmanifest','.trx','.html'):
        files.append(p)
manifest=[]
dest=r/'docs/list-resize/p1-evidence.zip'
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(files):
        name='list-p1/'+p.relative_to(a).as_posix();data=p.read_bytes();z.writestr(name,data)
        manifest.append(dict(path=name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
    z.writestr('manifest.json',json.dumps(manifest,indent=2))
with zipfile.ZipFile(dest) as z:
    for entry in json.loads(z.read('manifest.json')):
        data=z.read(entry['path'])
        assert len(data)==entry['bytes'] and hashlib.sha256(data).hexdigest()==entry['sha256']
print(len(manifest),'entries verified;',dest.stat().st_size,'bytes')
