"""Seal owned evidence; never changes registered layouts or dependency versions."""
from pathlib import Path
import subprocess,json,hashlib,zipfile,shutil,difflib
R=Path.cwd();O=R/'artifacts/seek-responsive';F=O/'full';D=Path(__file__).resolve().parent
sha=lambda b:hashlib.sha256(b).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def save(n,v):(D/n).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
for name,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 assert (O/('kernel-'+name)).read_bytes()==git('-C',str(R/'externals/bili-kernel'),*args),name
save('kernel-unchanged.json',{n:sha((O/('kernel-'+n)).read_bytes()) for n in ['head','status','staged.patch','worktree.patch']})
closure=[]
for n in git('ls-files','src').decode().splitlines():
 if not n.endswith(('.cs','.xaml')):continue
 a=(R/n).read_bytes();b=(F/n).read_bytes()
 norm=lambda x:x.removeprefix(b'\xef\xbb\xbf').replace(b'\r\n',b'\n')
 closure.append(dict(path=n,source=sha(a),host=sha(b),equal=norm(a)==norm(b)))
assert {x['path'] for x in closure if not x['equal']}=={
 'src/Desktop/BiliCopilot.UI/App.xaml.cs','src/Desktop/BiliCopilot.UI/GlobalDependencies.cs',
 'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel/PlayerViewModel.Playback.cs'}
save('app-source-closure.json',closure)
packages=[];(D/'candidate-feed').mkdir(exist_ok=True)
for p in sorted((O/'candidate-feed').glob('*20261101.5.*')):
 target=D/'candidate-feed'/p.name
 if target.exists():assert target.read_bytes()==p.read_bytes()
 else:shutil.copyfile(p,target)
 packages.append(dict(path='candidate-feed/'+p.name,sha256=sha(p.read_bytes())))
save('packages.json',packages)
for name,base in [('core','698d546e5c9edf5c96b6871357dc532542457c45'),('legacy','d7984643f4792133e7664c55e01df6e95e9cbc21')]:
 (D/(name+'.patch')).write_bytes(git('-C',str(O/name),'diff',base,'HEAD'))
 assert not git('-C',str(O/name),'status','--porcelain')
with zipfile.ZipFile(D/'source-snapshot.zip','w',zipfile.ZIP_DEFLATED) as z:
 for name in ['core','legacy']:
  for n in git('-C',str(O/name),'ls-files').decode().splitlines():z.write(O/name/n,name+'/'+n)
 for p in (O/'candidate-audit/source-documents').glob('*.source'):z.write(p,'source-documents/'+p.name)
 z.write(O/'candidate-audit/metadata.json','metadata.json')
shutil.copyfile(F/'src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs',D/'OfflineAcceptance.cs')
runs=['red','green','supplement','candidate3','final','visible-red','visible-baseline','baseline-exact','coverage','coverage2']
with zipfile.ZipFile(D/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
 for run in runs:
  folder=F/run
  if not folder.exists():continue
  exit=json.loads((folder/'retainedexit.json').read_text(encoding='utf-8-sig'))
  wer=json.loads((folder/'wer.json').read_text(encoding='utf-8-sig'))
  cleanup=json.loads((folder/'cleanup.json').read_text(encoding='utf-8-sig'))
  assert exit['exitCode']==0 and exit['retained'] and not exit['forced']
  assert wer['secondsAfterExit']>=31 and not wer['matches'] and cleanup['remaining']==0
  for p in folder.iterdir():
   if p.is_file():z.write(p,run+'/'+p.name)
 for p in O.iterdir():
  if p.is_file() and (p.suffix in ['.log','.py','.ps1','.json'] or p.name.startswith('kernel-')):z.write(p,'preparation/'+p.name)
 for p in (O/'test-results').glob('*.trx'):z.write(p,'tests/'+p.name)
 for n in ['PlayerViewModel.Playback.cs']:
  z.write(F/'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel'/n,'host-injection/'+n)
 z.write(F/'baseline-bin/BiliCopilot.UI.build.appxrecipe','baseline-recipe.xml')
 z.write(F/'src/Directory.Packages.props','host-candidate-versions.props')
save('archives.json',{n:dict(sha256=sha((D/n).read_bytes()),files={i.filename:sha(zipfile.ZipFile(D/n).read(i.filename)) for i in zipfile.ZipFile(D/n).infolist()}) for n in ['evidence.zip','source-snapshot.zip']})
print('Sealed source snapshots, paired PDB documents, candidate packages and owned runs.')
