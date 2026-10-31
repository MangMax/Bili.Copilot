from pathlib import Path
import hashlib,json,zipfile
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/legacy-logger-review4e91';F=O/'full'
files=list(O.glob('*.log'))+list(O.glob('*.json'))+list((F/'runtime').glob('*'))+[F/'events.jsonl']
files+=list((O/'compat-results').glob('*'))+list((O/'kernel-initial').glob('*.txt'))
files+=list((O/'metadata').glob('*.cs*'))
files+=[O/'boundary/Program.cs',O/'boundary/Boundary.csproj',O/'compat/Program.cs',O/'compat/Fixture.csproj',F/'NuGet.Config',F/'src/Directory.Packages.props']
files+=[F/'src/Desktop/BiliCopilot.UI'/n for n in ['App.xaml.cs','GlobalDependencies.cs','OfflineAcceptance.cs','Package.appxmanifest']]
manifest={}
with zipfile.ZipFile(D/'evidence.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(set(files)):
  if not p.is_file():continue
  b=p.read_bytes();key=p.relative_to(O).as_posix();z.writestr(key,b);manifest[key]={'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()}
(D/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
with zipfile.ZipFile(D/'evidence.zip') as z:
 for k,v in manifest.items():assert hashlib.sha256(z.read(k)).hexdigest()==v['sha256']
print(len(manifest),'evidence files verified')
