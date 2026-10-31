"""Read-only validation and evidence archive. Does not launch any process under test."""
from pathlib import Path
import hashlib,io,json,subprocess,tarfile,zipfile
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/legacy-ready'
sha=lambda b:hashlib.sha256(b).hexdigest()
norm=lambda b:b.removeprefix(bytes([239,187,191])).replace(b'\r\n',b'\n')
version='0.0.1-preview6.ready06c7.1'
packages={}
for name in ['Legacy','Share']:
 p=O/f'clean/feed/Danmaku.{name}.{version}.nupkg'
 with zipfile.ZipFile(p) as z:
  dll=z.read(next(n for n in z.namelist() if n.endswith(f'/Danmaku.{name}.dll')))
 built=(O/f'full/src/_build/x64/Release/BiliCopilot.UI/bin/Danmaku.{name}.dll').read_bytes()
 assert dll==built
 packages[name]={'version':version,'nupkgSha256':sha(p.read_bytes()),'dllSha256':sha(dll),'fullBuildMatches':True}
same=[];diff=[]
with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git','archive','44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede','src']))) as t:
 for member in t.getmembers():
  if member.isfile() and member.name.endswith(('.cs','.xaml')):
   expected=t.extractfile(member).read();actual=(O/'full'/member.name).read_bytes()
   (same if norm(expected)==norm(actual) else diff).append(member.name)
assert set(diff)=={'src/Desktop/BiliCopilot.UI/App.xaml.cs','src/Desktop/BiliCopilot.UI/GlobalDependencies.cs'}
compat=json.loads((O/'clean/compat-results/results.json').read_text());assert len(compat)==23 and all(x['passed'] for x in compat)
assert (O/'clean/compat-results/public-api.txt').read_bytes()==(D.parent/'legacy-paused-seek/public-api-baseline.txt').read_bytes()
for n in ['DanmakuFrostMaster.cs','DanmakuRender.cs']:
 assert (O/'clean/source/src/Danmaku.Legacy'/n).read_bytes()==(D/'source'/n).read_bytes()
 assert 'Probe' not in (D/'source'/n).read_text(encoding='utf-8')
validation={'application':'44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede','parent':'e8143389f8765e25456003213d3eb2f4b3dde061',
 'source':{p.name:sha(p.read_bytes()) for p in (D/'source').glob('*.cs')},'packages':packages,
 'unchangedProductionCsXaml':len(same),'fixtureInjectionDifferences':diff,'compatPassed':len(compat),
 'fullApplicationBuild':'exit0; 0 errors; 1 protobuf warning; Release/x64/JIT',
 'fullApplicationRuntime':'NOT RUN: native matrix AV triggered requested stop boundary',
 'acceptance':'INCOMPLETE: before-ready Close and full application three remounts NOT PASSED'}
(D/'validation.json').write_text(json.dumps(validation,indent=2))
files=[]
for phase in ['red','green','matrix']:
 files+=list((O/phase/'evidence').rglob('*'))
 files+=list((O/phase/'feed').glob('*'))
 files+=list((O/phase).glob('*.log'))
 files+=list((O/phase/'source/src/Danmaku.Legacy').glob('*.cs'))
files+=list((O/'clean/feed').glob('*'))+list((O/'clean/compat-results').glob('*'))+list((O/'clean').glob('*.log'))
files+=[O/'clean/provenance.json',O/'full-build.log',O/'full/src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs']
files+=list(O.glob('kernel-*'))
manifest={}
with zipfile.ZipFile(D/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(set(files)):
  if p.is_file():
   name=p.relative_to(O).as_posix();b=p.read_bytes();z.writestr(name,b);manifest[name]={'sha256':sha(b),'bytes':len(b)}
(D/'evidence-manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(validation,indent=2));print('Archived',len(manifest),'files')
