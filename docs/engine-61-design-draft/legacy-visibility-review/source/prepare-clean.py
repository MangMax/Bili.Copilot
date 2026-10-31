from pathlib import Path
import subprocess, json, hashlib, zipfile, shutil, sys
R=Path.cwd(); D=Path(__file__).resolve().parent; phase=sys.argv[1]; O=D/phase
O.mkdir(exist_ok=False)
def git(path,rev='21d76cff'): return subprocess.check_output(['git','show',rev+':'+path]).decode('utf-8-sig')
base='docs/engine-61-design-draft/'
m=json.loads(git(base+'legacy-paused-seek/origin.json')); a=Path(m['archive'])
assert hashlib.sha256(a.read_bytes()).hexdigest()==m['archiveSha256']
with zipfile.ZipFile(a) as z:z.extractall(O/'extract')
next((O/'extract').iterdir()).rename(O/'source'); S=O/'source'
assert all(hashlib.sha256((S/n).read_bytes()).hexdigest()==h for n,h in m['files'].items())
for n in ['DanmakuRender.cs','DanmakuFrostMaster.cs']:
    text=git(base+('legacy-early-close' if n=='DanmakuRender.cs' else 'legacy-ready')+'/source/'+n)
    (S/'src/Danmaku.Legacy'/n).write_text(text,encoding='utf-8')
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(O/n).write_text('<Project/>')
version='0.0.1-preview6.remount6273.'+phase
shutil.copytree(R/(base+'legacy-paused-seek/harness'),O/'host'); shutil.copyfile(D/'Probe.cs',O/'host/Program.cs')
(O/'host/Assets').mkdir();shutil.copyfile(S/'src/icon.png',O/'host/Assets/Logo.png')
p=O/'host/Spike.csproj';p.write_text(p.read_text(encoding='utf-8-sig').replace('0.0.1-legacyseek1085f94e.2',version))
identity='BiliCopilot.Remount6273'+phase
p=O/'host/Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('BiliCopilot.LegacySeek1085f94e',identity))
(O/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="feed"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
def run(cmd,name):
    with (O/(name+'.log')).open('w') as f:p=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
    print(phase,name,p.returncode,flush=True);p.check_returncode()
for n in ['Share','Legacy']:run(['dotnet','pack',str(S/f'src/Danmaku.{n}/Danmaku.{n}.csproj'),'-c','Release','-p:Platform=x64','-p:PackageVersion='+version,'-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false','-o',str(O/'feed')],'pack-'+n)
run(['dotnet','build',str(O/'host/Spike.csproj'),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false','--configfile',str(O/'NuGet.Config')],'build')
s=git(base+'legacy-paused-seek/run.ps1').replace('BiliCopilot.LegacySeek1085f94e',identity).replace('legacy-seek-1085f94e','remount-6273').replace("@('BACKWARD','REPEATED')","@()").replace('$cases.Count -ne 12','$cases.Count -ne 5').replace('.Count -ne 12','.Count -ne 5')
s=s.replace("}catch{$failed=$true;", " Run @('ui','screenshot','--on','local','-a',\"$ownedPid\",'--output',\"$evidence/DONE.png\",'--json') | Out-Null\n}catch{$failed=$true;")
(O/'run.ps1').write_text(s,encoding='utf-8-sig')
