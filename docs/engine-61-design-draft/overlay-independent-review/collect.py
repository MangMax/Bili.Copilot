from pathlib import Path
import hashlib,json,zipfile
r=Path.cwd();o=r/'artifacts/overlay-review';f=o/'full';d=r/'docs/engine-61-design-draft/overlay-independent-review'
files=list(o.glob('*.log'))+list((f/'runtime').glob('*'))+list((o/'kernel-initial').glob('*'))
files+=list((o/'regression').glob('*.trx'))+list((o/'adapter/expanded').glob('*.cs'))+list((o/'adapter/expanded').glob('*.csproj'))+list((o/'adapter/expanded').glob('*.log'))
for folder in [r/'artifacts/speed-close/overlay-review',r/'artifacts/overlay-remount/independent']:
 files += [p for p in folder.glob('*') if p.suffix in ['.cs','.csproj','.json','.log','.trx']]
files += list((o/'metadata').glob('*.cs'))+list((o/'metadata').glob('*.csproj'))
files += [f/x for x in ['source-equality.json','consumer-pdb.json','NuGet.Config','src/Directory.Packages.props']]
files += [f/'src/Desktop/BiliCopilot.UI'/x for x in ['App.xaml.cs','GlobalDependencies.cs','OfflineAcceptance.cs','Package.appxmanifest']]
files += [f/'src/_build/x64/Release/BiliCopilot.UI/bin'/x for x in ['BiliCopilot.UI.runtimeconfig.json','AppxManifest.xml']]
files += list((r/'artifacts/speed-subset-integration').rglob('command.log'))+list((r/'artifacts/speed-subset-integration').rglob('provenance.json'))
files += list((f/'src/Desktop/BiliCopilot.UI/obj').rglob('OverlayTransportControls.g.cs'))
manifest={}
with zipfile.ZipFile(d/'evidence.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(set(files)):
  if not p.is_file():continue
  b=p.read_bytes();key=p.relative_to(r).as_posix();z.writestr(key,b);manifest[key]={'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()}
(d/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
with zipfile.ZipFile(d/'evidence.zip') as z:
 assert set(z.namelist())==set(manifest)
 for k,v in manifest.items():
  b=z.read(k);assert len(b)==v['bytes'] and hashlib.sha256(b).hexdigest()==v['sha256']
print(len(manifest),'files archived and verified;', (d/'evidence.zip').stat().st_size,'bytes')
