"""Audit integration evidence without launching application processes."""
from pathlib import Path
import hashlib,json,subprocess,zipfile,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/'artifacts/renderer-fault-integration'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT,text=True).strip()
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
result={'head':git('rev-parse','HEAD'),'kernel':git('-C','externals/bili-kernel','rev-parse','HEAD')}
assert result['kernel']=='0ae5e4e000a41a07f6696746706df11c06e8835b'
assert not git('-C','externals/bili-kernel','status','--porcelain')
assert git('diff','--name-only','ad4d17d3','HEAD','--','src','externals','packages')=='src/Desktop/BiliCopilot.UI/Controls/Danmaku/VideoDanmakuPanel.xaml.cs'
assert not git('diff','ef802ad6','HEAD','--','src','tests','packages','externals')
result['stages']={}
for stage in ('PanelFresh','Full','Release','RuntimeGate'):
 r=json.loads((BASE/stage/'provenance.json').read_text())
 assert r['exitCode']==0
 for p,h in r['sourceHashes'].items():assert sha(ROOT/p)==h,p
 for p,h in r['outputs'].items():assert sha(BASE/p)==h,p
 result['stages'][stage]={'exitCode':r['exitCode'],'startedUtc':r['startedUtc'],'completedUtc':r['completedUtc'],'outputsVerified':len(r['outputs'])}
 if 'counters' in r:result['stages'][stage]['counters']=r['counters']
archive=ROOT/'docs/performance-acceptance/renderer-fault-isolation/evidence.zip'
with zipfile.ZipFile(archive) as z:
 manifest=json.loads(z.read('manifest.json'))
 for x in manifest:
  b=z.read(x['path']);assert len(b)==x['bytes'] and hashlib.sha256(b).hexdigest()==x['sha256']
 result['archive']={'sha256':sha(archive),'entriesVerified':len(manifest)}
 def names(tree):return sorted(x.attrib['testName'] for x in tree.findall('.//t:UnitTestResult',ns))
 assert names(ET.parse(BASE/'Full/trx/results.trx'))==names(ET.fromstring(z.read('tests-and-audits/full/full.trx')))
 result['fullTestNamesEqualHistorical577']=True
 native=[]
 for run in ('final','verified'):
  for x in json.loads(z.read('runs/'+run+'/native-inputs.json').decode('utf-8-sig')):
   p=Path(x['path']);row=dict(run=run,path=str(p),exists=p.is_file(),archivedSha256=x['sha256'])
   if p.is_file():
    row['currentSha256']=sha(p);row['matches']=row['currentSha256']==x['sha256'].lower();assert row['matches']
   native.append(row)
 result['historicalNativeFiles']=native
runtime=[]
for x in json.loads((BASE/'runtime-input-recovery.json').read_text()):
 p=ROOT/x['destination'];assert sha(p)==x['sha256']
 runtime.append(x)
app=BASE/'Release/build-output/BiliCopilot.UI/bin'
for rel in ('libmpv/x64/libmpv-2.dll','ffmpeg/x64/ffmpeg.exe','bbdown/x64/BBDown.exe'):
 source=ROOT/'src/Desktop/BiliCopilot.UI/Assets'/rel
 output=app/'Assets'/rel
 assert sha(source)==sha(output)
 runtime.append({'source':str(source),'output':str(output),'sha256':sha(output)})
assert (app/'resources.pri').stat().st_size>0
result['runtimeFiles']=runtime
assets=json.loads((ROOT/'src/Desktop/BiliCopilot.UI/obj/project.assets.json').read_text())
deps=json.loads((app/'BiliCopilot.UI.deps.json').read_text())
result['packages']=[]
version='0.0.1-seekd1fc.20261101.2'
for name in ('Danmaku.Core','Danmaku.Legacy','Danmaku.Share'):
 key=name+'/'+version
 assert [k for k in assets['libraries'] if k.startswith(name+'/')]==[key]
 assert [k for k in deps['libraries'] if k.startswith(name+'/')]==[key]
 package=ROOT/'packages/danmaku'/(name+'.'+version+'.nupkg')
 with zipfile.ZipFile(package) as z:
  dll=next(n for n in z.namelist() if n.endswith('/'+name+'.dll'))
  h=hashlib.sha256(z.read(dll)).hexdigest()
 assert sha(app/(name+'.dll'))==h
 result['packages'].append(dict(name=name,version=version,packageSha256=sha(package),outputDllSha256=h))
(BASE/'app-project.assets.json').write_bytes((ROOT/'src/Desktop/BiliCopilot.UI/obj/project.assets.json').read_bytes())
(BASE/'Release/BiliCopilot.UI.deps.json').write_bytes((app/'BiliCopilot.UI.deps.json').read_bytes())
for stage,name,directory in [('Full','BiliCopilot.UI.Tests',BASE/'Full/outputs/bin/BiliCopilot.UI.Tests/release'),('Release','BiliCopilot.UI',app)]:
 helper=ROOT/'artifacts/searchbox-properties-integration/Metadata/bin/Release/net11.0/Metadata.dll'
 cmd=['C:/Program Files/dotnet/dotnet.exe',str(helper),str(directory/(name+'.dll')),str(directory/(name+'.pdb')),str(BASE/stage/'pdb.json')]
 with (BASE/stage/'pdb.log').open('wb') as log:code=subprocess.run(cmd,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT).returncode
 assert code==0
 data=json.loads((BASE/stage/'pdb.json').read_text());docs=data['documents']
 result['stages'][stage]['pdb']={'helperSha256':sha(helper),'exitCode':code,'documents':len(docs),'readable':sum(x['exists'] for x in docs),'mismatches':sum(x['exists'] and not x['matches'] for x in docs)}
(BASE/'audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('historicalNativeFiles','runtimeFiles')},indent=2))
