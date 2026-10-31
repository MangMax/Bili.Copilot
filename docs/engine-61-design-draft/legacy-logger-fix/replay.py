from pathlib import Path
import json,shutil,subprocess,zipfile,hashlib
r=Path.cwd();d=r/'docs/engine-61-design-draft/legacy-logger-fix';old=d.parent/'legacy-paused-seek';o=r/'artifacts/legacy-logger-fix';o.mkdir(parents=True,exist_ok=True)
m=json.loads((old/'origin.json').read_text());a=Path(m['archive']);assert hashlib.sha256(a.read_bytes()).hexdigest()==m['archiveSha256']
with zipfile.ZipFile(a) as z:z.extractall(o/'extract')
s=next((o/'extract').iterdir())
for name in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(o/name).write_text('<Project />')
for p in (old/'source').glob('*.cs'):shutil.copyfile(p,s/'src/Danmaku.Legacy'/p.name)
f=s/'src/Danmaku.Legacy/DanmakuRender.cs';t=f.read_text(encoding='utf-8-sig')
t=t.replace('public void Stop()\n        {','public void Stop() => Stop(true);\n\n        internal void Stop(bool log)\n        {').replace('Logger.Log("DanmakuRender is stopped");','if (log) Logger.Log("DanmakuRender is stopped");')
t=t.replace('public void Close()\n        {\n            Stop();','public void Close() => Close(true);\n\n        internal void Close(bool log)\n        {\n            Stop(log);').replace('Logger.Log("DanmakuRender is closed");','if (log) Logger.Log("DanmakuRender is closed");');f.write_text(t,encoding='utf-8')
f=s/'src/Danmaku.Legacy/DanmakuFrostMaster.cs';t=f.read_text(encoding='utf-8-sig')
t=t.replace('_render?.Stop();\n                lock (_updateTimeQueue) _updateTimeQueue.Clear();\n            }','_render?.Stop(false);\n                lock (_updateTimeQueue) _updateTimeQueue.Clear();\n            }\n\n            // External loggers may reenter Seek: never invoke them holding playback.\n            if (_render != null) Logger.Log("DanmakuRender is stopped");',1)
t=t.replace('_render?.Close();\n                Logger.Log("DanmakuFrostMaster is closed");\n            }','_render?.Close(false);\n            }\n\n            if (_render != null)\n            {\n                Logger.Log("DanmakuRender is stopped");\n                Logger.Log("DanmakuRender is closed");\n            }\n            Logger.Log("DanmakuFrostMaster is closed");',1)
f.write_text(t,encoding='utf-8')
(d/'source').mkdir(exist_ok=True)
for n in ['DanmakuFrostMaster.cs','DanmakuRender.cs']:shutil.copyfile(s/'src/Danmaku.Legacy'/n,d/'source'/n)
p=subprocess.run(['dotnet','build',str(s/'src/Danmaku.Legacy/Danmaku.Legacy.csproj'),'-c','Release','-p:Platform=x64','-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false'],capture_output=True);(o/'build.log').write_bytes(p.stdout+p.stderr);print('build',p.returncode,flush=True);assert p.returncode==0
b=o/'boundary';shutil.copytree(Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-afdc1233/artifacts/legacy-seek-reviewafdc/boundary/bin/x64/Release/net11.0-windows10.0.22621.0'),b,dirs_exist_ok=True)
# Use the new compiled candidate, not the package left by the review baseline run.
dll=next((s/'src/Danmaku.Legacy/bin/x64/Release').rglob('Danmaku.Legacy.dll'));shutil.copyfile(dll,b/'Danmaku.Legacy.dll')
for case in ['reentry','queue','close','stop']:
 p=subprocess.run(['dotnet',str(b/'Boundary.dll'),case],capture_output=True,timeout=20);(d/(case+'.txt')).write_bytes(p.stdout+p.stderr);print(case,p.returncode,p.stdout.decode('utf-8',errors='replace'),flush=True);assert p.returncode==0
(d/'source.json').write_text(json.dumps({'origin':m['engineCommit'],'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (d/'source').glob('*.cs')},'dll':str(dll),'dllSha256':hashlib.sha256(dll.read_bytes()).hexdigest()},indent=2))
