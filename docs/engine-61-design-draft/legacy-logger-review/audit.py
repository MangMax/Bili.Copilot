from pathlib import Path
import json,hashlib,zipfile,subprocess
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/legacy-logger-review4e91';F=O/'full';V='0.0.1-preview6.loggerreview4e91.1'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
origin=read(D.parent/'legacy-paused-seek/origin.json');changed=[]
for n,h in origin['files'].items():
 if sha(O/'source'/n)!=h:changed.append(n)
assert sorted(Path(n).name for n in changed)==['DanmakuFrostMaster.cs','DanmakuRender.cs']
for n in changed:assert sha(O/'source'/n)==sha(D.parent/'legacy-logger-fix/source'/Path(n).name)
packages={};modules=read(F/'runtime/modules.json')
for n in ['Legacy','Share']:
 p=O/f'feed/Danmaku.{n}.{V}.nupkg'
 with zipfile.ZipFile(p) as z:
  entry=next(x for x in z.namelist() if x.endswith(f'/Danmaku.{n}.dll'));h=hashlib.sha256(z.read(entry)).hexdigest()
 paths=[F/f'layout/Danmaku.{n}.dll',F/f'src/_build/x64/Release/BiliCopilot.UI/bin/Danmaku.{n}.dll',Path.home()/f'.nuget/packages/danmaku.{n.lower()}/{V}/'/entry]
 assert all(sha(x)==h for x in paths)
 assert next(x['sha256'].lower() for x in modules if x['name']==f'Danmaku.{n}.dll')==h
 packages[n]={'package':str(p),'packageSha256':sha(p),'entry':entry,'dllSha256':h,'paths':list(map(str,paths))}
results=read(O/'compat-results/results.json');assert len(results)==23 and all(x['passed'] for x in results)
assert (O/'compat-results/public-api.txt').read_bytes()==(D.parent/'legacy-paused-seek/public-api-baseline.txt').read_bytes()
meta=read(O/'consumer-pdb.json');assert meta['sha256']==sha(F/'layout/BiliCopilot.UI.dll')==next(x['sha256'].lower() for x in modules if x['name']=='BiliCopilot.UI.dll')
docs={x['hash'] for x in meta['documents']};sources=read(O/'source-equality.json')
critical=[x for x in sources if x['path'].endswith('.cs') and ('/PlayerViewModel/' in x['path'] or '/DanmakuRenderViewModel/' in x['path'] or '/Controls/Core/' in x['path'] or 'VideoDanmakuPanel' in x['path'])]
assert all(x['consumerSha256'] in docs for x in critical)
rows=[json.loads(x) for x in (F/'events.jsonl').read_text().splitlines()];data=[x['data'] for x in rows]
assert any(x['kind']=='state' and x['position']==20 and x['state']=='Paused' and x['danmakuPaused'] for x in data)
assert any(x['kind']=='closed' and x['disposed'] and x['remaining']==0 for x in data)
exit=read(F/'runtime/retainedexit.json');wer=read(F/'runtime/wer.json');cleanup=read(F/'runtime/cleanup.json')
assert exit['retained'] and not exit['forced'] and exit['exitCode']==-1073741189
assert wer['secondsAfterExit']>=31 and wer['applicationLogReadable'] and len(wer['matches'])>=1
assert cleanup=={'packages':0,'processPresent':False}
kernel=R/'externals/bili-kernel'
assert subprocess.check_output(['git','-C',str(kernel),'status','--porcelain'])==(O/'kernel-initial/status.txt').read_bytes()
assert subprocess.check_output(['git','-C',str(kernel),'diff','--binary'])==(O/'kernel-initial/worktree.txt').read_bytes()
report={'parent':subprocess.check_output(['git','rev-parse','feat/performance-45']).decode().strip(),'originFiles':45,'changed':changed,'version':V,'packages':packages,'compat':{'passed':23,'failed':0,'skipped':0,'apiByteEqual':True},'productionSourceFilesEqual':len(sources),'criticalPdbDocumentsVerified':len(critical),'fullBuild':'full-build-2.log: 0 warnings, 0 errors','exit':exit,'wer':wer,'cleanup':cleanup,'nativeVerdict':'FAIL: abnormal exit; short seek blocked by production threshold; paused redraw visibility not consistently proved; remount empty','kernelUnchanged':True}
(D/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Evidence verified; native verdict FAIL retained, not a passing run.')
