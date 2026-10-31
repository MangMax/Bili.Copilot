from pathlib import Path
import hashlib,json,zipfile
R=Path.cwd();D=Path(__file__).resolve().parent; files=set()
for arm in ['red','green','clean','final','extended','extendedfinal','regression','earlyclose','lifetimes']:
    root=R/'artifacts'/('reconstruct-'+arm)
    files.update(p for p in root.glob('*') if p.is_file())
    for folder in ['evidence','feed','compat-results']:
        files.update(p for p in (root/folder).rglob('*') if p.is_file())
    for n in ['host/Program.cs','host/Spike.csproj','host/Package.appxmanifest','boundary/Program.cs']:
        if (root/n).is_file():files.add(root/n)
root=R/'artifacts/reconstruct-app'
files.update(p for p in root.glob('*') if p.is_file())
for folder in ['full/runtime','full/runtime2']:
    files.update(p for p in (root/folder).rglob('*') if p.is_file())
files.add(root/'full/events.jsonl')
files.add(root/'full/src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs')
root=R/'artifacts/legacy-reconstruct'
files.update(p for p in root.glob('*') if p.is_file())
for folder in ['pixels-red','pixels-clean','pixels-final','app-pixels-first','app-pixels-first40','app-pixels-final']:
    files.update(p for p in (root/folder).rglob('*') if p.is_file())
manifest={}
with zipfile.ZipFile(D/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(files):
        n=p.relative_to(R/'artifacts').as_posix();raw=p.read_bytes();z.writestr(n,raw);manifest[n]=hashlib.sha256(raw).hexdigest()
(D/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
print('sealed',len(manifest),'files')
