from pathlib import Path
import json,hashlib,xml.etree.ElementTree as ET,subprocess
r=Path.cwd();o=r/'artifacts/overlay-review';f=o/'full';d=r/'docs/engine-61-design-draft/overlay-independent-review'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
result={'head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'tests':{}}
for name,p,count in [('speed8',r/'artifacts/speed-close/overlay-review/results.trx',8),('parentRegression',o/'regression/regression.trx',539)]:
 t=ET.parse(p);c=t.find('.//t:Counters',ns).attrib;rows=t.findall('.//t:UnitTestResult',ns)
 assert int(c['passed'])==count and int(c['failed'])==0 and len(rows)==count
 result['tests'][name]={'counters':c,'names':[x.attrib['testName'] for x in rows]}
meta=read(f/'consumer-pdb.json');hashes={x['hash'] for x in meta['documents']};sources=read(f/'source-equality.json')
for rel,info in sources.items():
 if rel.endswith('.cs'):assert info['consumer'] in hashes,rel
assert hashlib.sha256((f/'layout/BiliCopilot.UI.dll').read_bytes()).hexdigest()==meta['sha256']
mods=read(f/'runtime/modules.json');assert next(x for x in mods if x['module']=='BiliCopilot.UI.dll')['sha256'].lower()==meta['sha256']
result['sourceCount']=len(sources);result['pePdbAndLoadedModuleHashMatch']=True
rows=[json.loads(x) for x in (f/'runtime/events.jsonl').read_text().splitlines()];data=[x['data'] for x in rows]
assert not any(x['kind'] in ['unhandled','action-error','startup-error','retirement-error'] for x in data)
shots=[x for x in data if x['kind']=='overlay-snapshot'];remount=[x for x in shots if x['stage'].startswith('reloaded-')];unload=[x for x in shots if x['stage']=='unloaded']
assert len(remount)==4 and len(unload)==4 and len({x['instance'] for x in remount+unload})==1
assert all(not x['active'] and not x['speedTimer'] and not x['progressTimer'] and x['speedQueue']==0 and x['progressQueue']==0 and x['subtitleSourceNull'] for x in unload)
assert all(x['active'] and x['subtitleSourceMatches'] and x['state']=='Playing' for x in remount)
for rate in [1.5,1.75,1.25,2.25]:assert any(x['kind']=='state' and x['state']=='Playing' and x['speed']==rate for x in data)
close=next(x for x in data if x['kind']=='speed-close-enter');assert close['state']=='Playing' and close['pending']==20
assert any(x['kind']=='speed-close-done' and x['disposed'] and x['completed'] for x in data)
assert sum(x['kind']=='initialized' for x in data)==2
assert len({x['instance'] for x in shots})==2
assert 'Review subtitle 5759' in (f/'runtime/subtitle-visible-tree.json').read_text(encoding='utf-8-sig')
ret=read(f/'runtime/retainedexit.json');wer=read(f/'runtime/wer.json');cleanup=read(f/'runtime/cleanup.json')
assert ret['exitCode']==0 and ret['retained'] and not ret['forced']
assert wer['secondsAfterExit']>=31 and wer['applicationLogReadable'] and not wer['matches']
assert cleanup['packages']==0 and not cleanup['processPresent'] and read(f/'runtime/yield.json')['released']
result['runtime']={'remountRounds':len(remount),'sameInstance':remount[0]['instance'],'speedClose':close,'exit':ret,'wer':wer,'cleanup':cleanup,'snapshots':shots,'significantEvents':[x for x in rows if x['data']['kind'] in ['initialized','speed-close-enter','speed-close-done','owner-retired']]}
result['limits']=['Experimental Core/Share only for native consumer; formal preview6 JIT build separate.','Exact native call count not instrumented; single subscriptions verified in extracted adapter, no repeated observed speed effect.','Real queue-cancel timing not hit: before-unload speedQueue=0.','Synthetic subtitle menu item proves ItemsSource/display remount, not native subtitle rendering.','Dedicated WinUIAnalyzer and AOT not run; historical AV and Legacy seek/#49 holds unchanged.']
(d/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS tests, sources/PDB/loaded DLL, remount, Playing speed/close/reopen, exit/WER/cleanup')
