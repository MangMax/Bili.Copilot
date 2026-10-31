from pathlib import Path
import argparse, hashlib, json, shutil, subprocess, zipfile

p = argparse.ArgumentParser()
p.add_argument('name')
p.add_argument('--red', action='store_true')
a = p.parse_args()
here = Path(__file__).resolve().parent
repo = here.parents[2]
old = here.parent / 'legacy-paused-seek'
root = repo / 'artifacts' / ('reconstruct-' + a.name)
assert not root.exists(), 'fresh name required'
m = json.loads((old / 'origin.json').read_text())
archive = Path(m['archive'])
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(archive) == m['archiveSha256']
root.mkdir(parents=True)
with zipfile.ZipFile(archive) as z: z.extractall(root / 'extract')
source = next((root / 'extract').iterdir())
source.rename(root / 'source'); source = root / 'source'
for n,h in m['files'].items(): assert sha(source/n) == h, n
for layer in ['legacy-ready','legacy-early-close'] + ([] if a.red else ['legacy-reconstruct']):
    for f in (here.parent/layer/'source').glob('*.cs'): shutil.copyfile(f,source/'src/Danmaku.Legacy'/f.name)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']: (root/n).write_text('<Project />')
shutil.copytree(old/'harness',root/'host')
shutil.copyfile(here/'Probe.cs',root/'host/Program.cs')
(root/'host/Assets').mkdir()
shutil.copyfile(source/'src/icon.png',root/'host/Assets/Logo.png')
version = '0.0.1-reconstruct0b4f.' + a.name
identity = 'BiliCopilot.Reconstruct0b4f' + a.name.replace('-','')
for n in ['Spike.csproj','Package.appxmanifest']:
    f=root/'host'/n
    f.write_text(f.read_text(encoding='utf-8-sig').replace('0.0.1-legacyseek1085f94e.2',version).replace('BiliCopilot.LegacySeek1085f94e',identity))
script=(old/'run.ps1').read_text(encoding='utf-8-sig').replace('BiliCopilot.LegacySeek1085f94e',identity).replace('legacy-seek-1085f94e','reconstruct-0b4f-'+a.name).replace("@('BACKWARD','REPEATED')","@('TOP','TOPSTABLE','BOTTOM','ROLL','REVERSE','BLANK','OTHER')").replace('-ne 12','-ne 18')
(root/'run.ps1').write_text(script,encoding='utf-8-sig')
(root/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="feed"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
def run(cmd,name):
    with (root/name).open('w') as log: r=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT)
    print(name,r.returncode,flush=True); r.check_returncode()
for pkg in ['Danmaku.Share','Danmaku.Legacy']:
    run(['dotnet','pack',str(source/'src'/pkg/(pkg+'.csproj')),'-c','Release','-p:Platform=x64','-p:PackageVersion='+version,'-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false','-o',str(root/'feed')],'pack-'+pkg+'.log')
run(['dotnet','build',str(root/'host/Spike.csproj'),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false','--configfile',str(root/'NuGet.Config')],'build.log')
(root/'origin.json').write_text(json.dumps(dict(red=a.red,version=version,identity=identity,files={n:sha(source/n) for n in m['files']}),indent=2))
print(root)
