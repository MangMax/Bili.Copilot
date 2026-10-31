import pathlib, zipfile, hashlib, json, os
r=pathlib.Path.cwd(); a=r/'artifacts/speed-close'; d=pathlib.Path(__file__).parent
files={}
for p in a.glob('*.log'):files[p.name]=p
files['test-validation.json']=a/'test-validation.json'
for run in ['red','green1','final','final2','suite']:
    for p in (a/run).glob('*'):
        if p.is_file():files[f'{run}/{p.name}']=p
o=a/'full'
for run in ['runtime','runtime2']:
    for p in (o/run).glob('*'):
        if p.is_file():files[f'{run}/{p.name}']=p
for p in o.glob('*.json'):files[p.name]=p
app=o/'src/Desktop/BiliCopilot.UI'
for name in ['App.xaml.cs','GlobalDependencies.cs','Package.appxmanifest','OfflineAcceptance.cs']:
    files[f'fixture/{name}']=app/name
files['fixture/Directory.Packages.props']=o/'src/Directory.Packages.props'
for layout in ['layout','layout2']:
    files[f'{layout}/BiliCopilot.UI.runtimeconfig.json']=o/layout/'BiliCopilot.UI.runtimeconfig.json'
    files[f'{layout}/AppxManifest.xml']=o/layout/'AppxManifest.xml'
backup=pathlib.Path(os.environ['TEMP'])/'bili-dc85fdad-kernel-initial'
for p in backup.glob('*'):
    if p.is_file():files[f'kernel-initial/{p.name}']=p
manifest={}
with zipfile.ZipFile(d/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
    for name,p in sorted(files.items()):
        data=p.read_bytes();z.writestr(name,data)
        manifest[name]={'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
(d/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
print('Evidence archived; no runtime or package mutations.')
