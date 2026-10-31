"""Fresh local source/package and managed review; no desktop or source-area writes."""
from pathlib import Path
import hashlib, json, shutil, subprocess, zipfile
R=Path.cwd(); D=Path(__file__).resolve().parent; O=R/'artifacts/legacy-ready/clean'; OLD=D.parent/'legacy-paused-seek'
O.mkdir(parents=True,exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(cmd,name):
 p=subprocess.run(cmd,cwd=R,capture_output=True,timeout=240);(O/(name+'.log')).write_bytes(p.stdout+p.stderr);print(name,p.returncode,flush=True);assert p.returncode==0
m=json.loads((OLD/'origin.json').read_text());a=Path(m['archive']);assert sha(a)==m['archiveSha256']
with zipfile.ZipFile(a) as z:z.extractall(O/'extract')
next((O/'extract').iterdir()).rename(O/'source'); S=O/'source'
assert all(sha(S/n)==h for n,h in m['files'].items())
for folder in [OLD/'source',D/'source']:
 for p in folder.glob('*.cs'):shutil.copyfile(p,S/'src/Danmaku.Legacy'/p.name)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(O/n).write_text('<Project/>')
version='0.0.1-preview6.ready06c7.1'
for n in ['Share','Legacy']:
 run(['dotnet','pack',str(S/f'src/Danmaku.{n}/Danmaku.{n}.csproj'),'-c','Release','-p:Platform=x64','-p:PackageVersion='+version,'-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false','-o',str(O/'feed')],'pack-'+n)
for kind in ['compat','boundary']:
 h=O/kind;h.mkdir()
 project=subprocess.check_output(['git','show','597dff0d:docs/engine-61-design-draft/c1a/Fixture.csproj'])
 (h/('Fixture.csproj' if kind=='compat' else 'Boundary.csproj')).write_bytes(project)
 code=subprocess.check_output(['git','show','597dff0d:docs/engine-61-design-draft/c1a/Program.cs']) if kind=='compat' else (D.parent/'legacy-logger-review/Boundary.cs').read_bytes().replace(b'Set(m, "_render", render);',b'Set(render, "_resourcesReady", true); Set(m, "_render", render);')
 (h/'Program.cs').write_bytes(code)
 run(['dotnet','build',str(h),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false'],kind+'-build')
 b=h/'bin/x64/Release/net11.0-windows10.0.22621.0'
 for n in ['Share','Legacy']:
  for ext,pack in [('dll','nupkg'),('pdb','snupkg')]:
   with zipfile.ZipFile(O/f'feed/Danmaku.{n}.{version}.{pack}') as z:
    entry=next(x for x in z.namelist() if x.endswith(f'/Danmaku.{n}.{ext}'));(b/f'Danmaku.{n}.{ext}').write_bytes(z.read(entry))
 if kind=='compat':
  (O/'compat-results').mkdir()
  run(['dotnet',str(b/'Fixture.dll'),str(O/'compat-results')],'compat-run')
  assert (O/'compat-results/public-api.txt').read_bytes()==(OLD/'public-api-baseline.txt').read_bytes()
 else:
  for case in ['reentry','closereentry','throwstop','throwclose','queue','close','stop']:run(['dotnet',str(b/'Boundary.dll'),case],case)
(O/'provenance.json').write_text(json.dumps({'parent':subprocess.check_output(['git','rev-parse','HEAD']).decode().strip(),'version':version,'originFilesVerified':len(m['files']),'files':{n:sha(S/n) for n in m['files']},'packages':{p.name:sha(p) for p in (O/'feed').glob('*')}},indent=2))
