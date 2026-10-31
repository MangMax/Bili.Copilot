from pathlib import Path
import hashlib,json,shutil,subprocess,zipfile
R=Path.cwd();D=R/'docs/engine-61-design-draft/legacy-ready';O=R/'artifacts/reconstruct-final';OLD=D.parent/'legacy-paused-seek'
version='0.0.1-reconstruct0b4f.final'
def run(cmd,name):
 p=subprocess.run(cmd,capture_output=True,timeout=240);(O/(name+'.log')).write_bytes(p.stdout+p.stderr);print(name,p.returncode,flush=True);assert p.returncode==0
for kind in ['compat','boundary']:
 h=O/kind;h.mkdir()
 project=subprocess.check_output(['git','show','597dff0d:docs/engine-61-design-draft/c1a/Fixture.csproj'])
 (h/('Fixture.csproj' if kind=='compat' else 'Boundary.csproj')).write_bytes(project)
 code=subprocess.check_output(['git','show','597dff0d:docs/engine-61-design-draft/c1a/Program.cs']) if kind=='compat' else (D.parent/'legacy-reconstruct/Boundary.cs').read_bytes()
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
