from pathlib import Path
import hashlib,json,shutil,subprocess,zipfile,sys
R=Path.cwd(); D=Path(__file__).resolve().parent; phase=sys.argv[1]; O=R/'artifacts/legacy-ready'/phase
O.mkdir(parents=True,exist_ok=False)
m=json.loads((D.parent/'legacy-paused-seek/origin.json').read_text()); a=Path(m['archive'])
assert hashlib.sha256(a.read_bytes()).hexdigest()==m['archiveSha256']
with zipfile.ZipFile(a) as z:z.extractall(O/'extract')
next((O/'extract').iterdir()).rename(O/'source'); S=O/'source'
assert all(hashlib.sha256((S/n).read_bytes()).hexdigest()==h for n,h in m['files'].items())
src=D/'source' if phase!='red' else D.parent/'legacy-logger-fix/source'
for p in src.glob('*.cs'):shutil.copyfile(p,S/'src/Danmaku.Legacy'/p.name)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(O/n).write_text('<Project/>')
version='0.0.1-preview6.ready06c7.'+phase
# Temporary experiment-only observation; no behavior change or callback under a lock.
p=S/'src/Danmaku.Legacy/DanmakuRender.cs'; s=p.read_text(encoding='utf-8-sig')
s=s.replace('private const float StandardCanvasWidth', 'internal static readonly System.Threading.ManualResetEventSlim ProbeGate = new(true);\n        internal static readonly System.Threading.ManualResetEventSlim ProbeAttempt = new(false);\n        private const float StandardCanvasWidth')
s=s.replace('if (_device == null || _isStopped)', 'ProbeAttempt.Set();\n            if (_device == null || _isStopped)')
s=s.replace('Logger.Log(args.Reason.ToString());','if (!ProbeGate.Wait(8000)) throw new TimeoutException("resource probe gate");\n            Logger.Log(args.Reason.ToString());')
p.write_text(s,encoding='utf-8')
p=S/'src/Danmaku.Legacy/DanmakuFrostMaster.cs';s=p.read_text(encoding='utf-8-sig').replace('_updateEvent.WaitOne();','_updateEvent.WaitOne();\n                    DanmakuRender.ProbeAttempt.Set();');p.write_text(s,encoding='utf-8')
shutil.copytree(D.parent/'legacy-paused-seek/harness',O/'host'); shutil.copyfile(D/('RedProbe.cs' if phase in ['red','green'] else 'Probe.cs'),O/'host/Program.cs')
(O/'host/Assets').mkdir();shutil.copyfile(S/'src/icon.png',O/'host/Assets/Logo.png')
p=O/'host/Spike.csproj';p.write_text(p.read_text(encoding='utf-8-sig').replace('0.0.1-legacyseek1085f94e.2',version))
p=O/'host/Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('BiliCopilot.LegacySeek1085f94e','BiliCopilot.Ready06c7'+phase))
(O/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="feed"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
def run(cmd,name):
 with (O/(name+'.log')).open('w') as f:p=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
 print(name,p.returncode,flush=True);p.check_returncode()
for n in ['Share','Legacy']:run(['dotnet','pack',str(S/f'src/Danmaku.{n}/Danmaku.{n}.csproj'),'-c','Release','-p:Platform=x64','-p:PackageVersion='+version,'-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false','-o',str(O/'feed')],'pack-'+n)
run(['dotnet','build',str(O/'host/Spike.csproj'),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false','--configfile',str(O/'NuGet.Config')],'build')
s=(D.parent/'legacy-paused-seek/run.ps1').read_text(encoding='utf-8-sig').replace('BiliCopilot.LegacySeek1085f94e','BiliCopilot.Ready06c7'+phase).replace('legacy-seek-1085f94e','legacy-ready-06c7').replace("@('BACKWARD','REPEATED')","@('READY')").replace('$cases.Count -ne 12','$cases.Count -ne '+('1' if phase in ['red','green'] else '6')).replace('.Count -ne 12','.Count -ne '+('1' if phase in ['red','green'] else '6'))
(O/'run.ps1').write_text(s,encoding='utf-8-sig')
