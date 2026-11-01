"""Local integration verification; never launches or packages the app."""
import datetime, hashlib, json, os, subprocess, sys
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
SDK=Path('C:/Program Files/dotnet')
os.environ['DOTNET_ROOT']=str(SDK)
os.environ['PATH']=str(SDK)+os.pathsep+os.environ['PATH']
stage=sys.argv[1]
run_name=sys.argv[2] if len(sys.argv)>2 else stage
base=ROOT/'artifacts/renderer-fault-integration'
dest=base/run_name
dest.mkdir(parents=True,exist_ok=False)
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT,text=True).strip()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
app=ROOT/'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj'
common=['-p:Platform=x64','-p:PublishAot=false',f'-p:SolutionDir={ROOT.as_posix()}/src/',f'-p:RestorePackagesPath={ROOT.as_posix()}/artifacts/seek-responsive-integration/packages']
if stage in ('Panel','Full'):
 project='BiliCopilot.DanmakuPanel.Tests' if stage=='Panel' else 'BiliCopilot.UI.Tests'
 cmd=['test',str(ROOT/'tests'/project),'-c','Release','--artifacts-path',str(dest/'outputs'),'--logger','trx;LogFileName=results.trx','--results-directory',str(dest/'trx'),'-p:NuGetAudit=false']
elif stage=='Release':
 cmd=['build',str(app),'-t:Rebuild','-c','Release',*common,'-p:NuGetAudit=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false',f'-p:BuildOutDir={(dest/"build-output").as_posix()}/',f'-bl:{dest/"build.binlog"}']
elif stage=='RuntimeGate':cmd=['msbuild',str(app),'-t:ValidateRuntimeDependencies','-p:Configuration=Release',*common]
else:raise ValueError(stage)
cmd.insert(0,str(SDK/'dotnet.exe'))
r={'head':git('rev-parse','HEAD'),'kernel':git('-C','externals/bili-kernel','rev-parse','HEAD'),'command':cmd,'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceHashes':{p:sha(ROOT/p) for p in git('ls-files','src','tests').splitlines() if (ROOT/p).is_file()}}
(dest/'provenance.json').write_text(json.dumps(r,indent=2),encoding='utf-8')
with (dest/'command.log').open('wb') as log:r['exitCode']=subprocess.run(cmd,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT).returncode
r['completedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
if stage in ('Panel','Full') and r['exitCode']==0:
 ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
 tree=ET.parse(dest/'trx/results.trx');rows=tree.findall('.//t:UnitTestResult',ns)
 r['counters']=tree.find('.//t:Counters',ns).attrib
 assert len(rows)==(12 if stage=='Panel' else 577) and all(x.attrib['outcome']=='Passed' for x in rows)
r['outputs']={str(p.relative_to(base)):sha(p) for p in dest.rglob('*') if p.is_file() and p.suffix in ('.dll','.pdb','.exe','.pri')}
(dest/'provenance.json').write_text(json.dumps(r,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in r.items() if k not in ('sourceHashes','outputs')},indent=2))
sys.exit(r['exitCode'])
