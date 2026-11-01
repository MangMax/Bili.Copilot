"""Audit the local receipts and produce a compact, source-backed handoff ledger."""
from pathlib import Path, PureWindowsPath
from datetime import datetime, timedelta
import hashlib, json, subprocess, sys, zipfile
root=Path(__file__).resolve().parents[3];base=root/'artifacts/recycled-cards'
out=root/'artifacts/scroll-continuity'
def J(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*a):return subprocess.check_output(['git',*a],cwd=root,text=True).strip()
results=[];groups={};intervals=[];summaries=[]
for stage in sorted(base.iterdir()):
 if not (stage/'identity.json').exists():continue
 identity=J(stage/'identity.json');ex=J(stage/'exit.json');wer=J(stage/'wer-exact.json')
 assert identity['pid']==ex['pid']==wer['pid'] and identity['path']==ex['path']==wer['path'],stage
 assert ex['absent'] and ex['retainedHasExited'] and ex['retainedExitCode']==0 and ex['observedSeconds']>=31 and not wer['events'],stage
 assert (stage/'remaining-owned-packages.txt').read_text(encoding='utf-8-sig').strip()=='0',stage
 assert 'released' in J(stage/'yield.json'),stage
 intervals.append((datetime.fromisoformat(identity['startedUtc']),datetime.fromisoformat(ex['closeUtc'])+timedelta(seconds=ex['observedSeconds'])))
 sources={PureWindowsPath(r['Path']).name:r['Hash'].lower() for r in J(stage/'source-hashes.json')}
 for name,digest in sources.items():assert sha(stage/'sources'/name)==digest,(stage,name)
 binary={PureWindowsPath(r['Path']).name:r['Hash'].lower() for r in J(stage/'binary-hashes.json')}
 for name,digest in binary.items():assert sha(stage/'layout'/name)==digest,(stage,name)
 key=binary['Harness.dll']
 if key in groups:assert groups[key]['sources']==sources,stage
 else:groups[key]={'sources':sources,'runs':[]}
 groups[key]['runs'].append(stage.name)
 check_exit=None;failures=set()
 if (stage/'checks.json').exists():assert J(stage/'checks.json')['failed']==0,stage
 if (stage/'matrix-continuity-50.json').exists():
  check=subprocess.run([sys.executable,str(Path(__file__).with_name('check.py')),str(stage)],capture_output=True,text=True)
  (stage/'continuity-check-output.txt').write_text(check.stdout+check.stderr)
  check_exit=check.returncode
  known_source_red=stage.name in ('real-article05','real-article2')
  failures={c['name'] for c in J(stage/'continuity-checks.json')['checks'] if not c['passed']}
  if known_source_red:
   assert check.returncode==2 and failures=={'AllObservedVisibleIdentitiesIncludingNull_50','AllObservedVisibleIdentitiesIncludingNull_500'},(stage,failures)
  else:assert check.returncode==0,stage
  summary=subprocess.check_output([sys.executable,str(Path(__file__).with_name('summarize.py')),str(stage)],text=True)
  summaries.extend(json.loads(line) for line in summary.splitlines())
  if (stage/'video-pixels.json').exists():
   visual=subprocess.run([sys.executable,str(Path(__file__).with_name('visual-check.py')),str(stage)],capture_output=True,text=True)
   (stage/'visual-check-output.txt').write_text(visual.stdout+visual.stderr)
   assert visual.returncode in (0,2)
  else:visual=None
  raw=J(stage/'matrix-continuity-500.json')
  if raw.get('realistic'):
   names=[f'article-{i}.png' for i in range(500)]+[f'article-avatar-{i}.png' for i in range(500)] if raw['family']=='Article' else [f'poster-{i}.png' for i in range(500)]
  elif raw['family']=='User':names=[f'user-{i}.png' for i in range(500)]
  else:names=['Cover.png']
 else:visual=None;names=['Cover.png']
 assets={name:sha(stage/'layout/Assets'/name) for name in names}
 assert all(digest==sha(base/'host/Assets'/name) for name,digest in assets.items()),stage
 (stage/'deployed-assets.json').write_text(json.dumps(assets,indent=2))
 results.append({'stage':stage.name,'pid':identity['pid'],'retainedExit':ex['retainedExitCode'],'observedSeconds':ex['observedSeconds'],'identityCheckExit':check_exit,'knownIdentityFailures':sorted(failures),'visualExit':None if visual is None else visual.returncode})
builds=[]
for p in sorted(out.glob('build*.log')):
 receipt=p.with_name(p.stem+'-receipt.json')
 if receipt.exists():
  r=J(receipt);start=datetime.fromisoformat(r['startedUtc']);end=datetime.fromisoformat(r['completedUtc']);assert r['exitCode']==0
  kind='explicit command receipt'
 else:
  times=next(r for r in J(out/'build-file-times.json') if r['Name']==p.name)
  start=datetime.fromisoformat(times['CreationTimeUtc']);end=datetime.fromisoformat(times['LastWriteTimeUtc']);kind='log filesystem bounds, no explicit command receipt'
 assert not any(start<b and end>a for a,b in intervals),p
 builds.append({'name':p.name,'start':start.isoformat(),'end':end.isoformat(),'basis':kind})
with zipfile.ZipFile(root/'docs/list-resize/mount-diagnosis-evidence.zip') as z:
 for name in ('BasicCoverImage.cs','BlurCoverImage.cs','ReloadableCoverImage.cs','RecommendationCoverImage.cs'):
  assert (base/'host'/name).read_bytes()==z.read('replay/host/'+name),name
assert git('-C','externals/bili-kernel','rev-parse','HEAD')=='0ae5e4e000a41a07f6696746706df11c06e8835b'
assert not git('-C','externals/bili-kernel','status','--porcelain')
assert git('diff','--name-only','6133ae590daf474d606e7b0afbdbf21e857b8ed1','--','src').splitlines()==['src/Desktop/BiliCopilot.UI/Controls/Components/ArticleCardControl/PartitionArticlePresenter.xaml']
protected=J(out/'protected-processes.json');current=J(out/'final-processes.json')
assert all(any(p['ProcessId']==q['ProcessId'] and p['ExecutablePath']==q['ExecutablePath'] for q in current) for p in protected)
assert not any(p['Name'].startswith('BiliCopilot.Recycle') for p in J(out/'final-packages.json'))
ledger={'head':git('rev-parse','HEAD'),'parent':git('rev-parse','feat/performance-45'),'kernel':git('-C','externals/bili-kernel','rev-parse','HEAD'),'runs':results,'binaryGroups':groups,'builds':builds,'protectedProcessesUnchanged':True,'productionDiff':git('diff','6133ae590daf474d606e7b0afbdbf21e857b8ed1','--','src')}
(out/'audit.json').write_text(json.dumps(ledger,indent=2));(out/'summary.json').write_text(json.dumps(summaries,indent=2))
print(json.dumps({'runs':len(results),'binaryGroups':len(groups),'continuousCases':len(summaries),'strictVisualRedRuns':sum(r['visualExit']==2 for r in results),'audit':'passed; visual reds deliberately retained'}))
