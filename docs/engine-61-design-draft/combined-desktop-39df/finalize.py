from pathlib import Path
import json,hashlib,zipfile,shutil,subprocess
R=Path.cwd();O=R/'artifacts/combined-60cd';F=O/'full';D=R/'docs/engine-61-design-draft/combined-desktop-39df'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
save=lambda n,x:(D/n).write_text(json.dumps(x,indent=2),encoding='utf8')
summary=[]
for run in ['runtime1','runtime2','runtime3']:
 E=F/run
 identity=read(E/'identity.json');ex=read(E/'retainedexit.json');wer=read(E/'wer.json');clean=read(E/'cleanup.json')
 assert ex['pid']==identity['pid'] and ex['path']==identity['path'] and ex['retained'] and not ex['forced'] and ex['exitCode']==0
 assert wer['secondsAfterExit']>=31 and wer['applicationLogReadable'] and not wer['matches'] and clean['remaining']==0
 summary.append(dict(run=run,pid=ex['pid'],path=ex['path'],exitCode=ex['exitCode'],werSeconds=wer['secondsAfterExit'],matches=wer['matches'],cleanup=clean))
save('exit-summary.json',summary)
events=[json.loads(s) for s in (F/'runtime3/events.jsonl').read_text().splitlines()]
assert not [e for e in events if e['data']['kind'] in ['unhandled','startup-error','action-error','observe-error']]
loaded=next(e['data'] for e in events if e['data']['kind']=='loaded-modules')
packages=read(D/'package-closure.json')['packages'];metadata=read(D.parent/'combined-acceptance/metadata.json')
for m in loaded['modules']:
 p=next(p for p in packages if 'Danmaku.'+p['name']==m['name']);md=next(x for x in metadata if x['path'].endswith(m['name']+'.dll'))
 assert m['sha'].lower()==p['dllSha'] and m['pdbSha'].lower()==p['pdbSha'] and m['mvid']==md['mvid']
 assert Path(m['path']).resolve().is_relative_to((F/'layout39df-3').resolve())
save('loaded-closure.json',dict(loaded=loaded,pairing='DLL CodeView/PDB/document checksums from unchanged combined-acceptance/metadata.json and offline-evidence.zip',symbols='Matching PDB files placed beside loaded DLLs; this is symbol pairing, not a claim that the runtime loads PDBs.'))
source=R.parent/'t3-60cd31cb/artifacts/combined-60cd/full';manifest=read(O/'copy-source-verified.json')
assert all(sha(source/n)==h for n,h in manifest.items());save('source-unchanged.json',dict(files=len(manifest),allHashesUnchanged=True))
before=read(O/'packages-before.json');after=read(O/'packages-after.json');before=before if isinstance(before,list) else [before];after=after if isinstance(after,list) else [after]
assert sorted(before,key=lambda x:x['Name'])==sorted(after,key=lambda x:x['Name'])
save('protected-identities.json',dict(packagesUnchanged=True,before=before,after=after,initialProcesses=read(O/'processes-before.json'),finalProcesses=read(O/'processes-after.json'),note='Read-only snapshots; user AotDev restarted from PID50412 to PID19868. No operation targeted either PID or their layout.'))
allResults={r:read(F/r/'state-results.json') for r in ['runtime1','runtime2','runtime2/supplement','runtime3','runtime3/continuation']}
save('assertion-summary.json',{r:dict(passed=sum(x['pass_'] for x in a),failed=[x['name'] for x in a if not x['pass_']]) for r,a in allResults.items()})
failures=[dict(run=r,result=x) for r,a in allResults.items() for x in a if not x['pass_'] and not x['name'].endswith('PausedPositionAgeStable')]
save('latest-counterexamples.json',failures)
for run in ['runtime2','runtime3']:
 es=[json.loads(s) for s in (F/run/'events.jsonl').read_text().splitlines()]
 # First failed/latest-final target period is kept in full events; this small excerpt contains all explicit seek completions.
 save(run+'-seek-trace.json',[e for e in es if e['data']['kind'] in ['command','seek-request']])
for n in ['build2.log','build2-command.json','build3.log','build3-command.json']:
 shutil.copyfile(O/n,D/n)
shutil.copyfile(F/'runtime3/tight-displacement.json',D/'tight-displacement.json')
# Keep the clean original-pixel evidence readily reviewable, all other raw evidence in the archive.
shutil.copytree(F/'runtime1/pixels',D/'pixels',dirs_exist_ok=True)
shutil.copyfile(F/'runtime3/core-latest-media-time.png',D/'core-latest-media-time.png')
with zipfile.ZipFile(D/'runtime-evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
 for run in ['runtime1','runtime2','runtime3']:
  for p in (F/run).rglob('*'):
   if p.is_file():z.write(p,str(p.relative_to(F)))
with zipfile.ZipFile(D/'runtime-evidence.zip') as z:
 ledger={x.filename:hashlib.sha256(z.read(x.filename)).hexdigest() for x in z.infolist()}
save('runtime-evidence-manifest.json',dict(sha256=sha(D/'runtime-evidence.zip'),files=ledger))
save('status.json',dict(runtimeAdmission='FAIL_CONTINUOUS_LATEST',productionChanges=False,formalDependenciesChanged=False,gitlinkChanged=False,failedRequirement='Concurrent 21 -> 19 -> 20 can settle at 19 in BOTH engines',retainedExitRuns=3,visualEvidence='runtime1 original unscaled crops, blank/different-text negatives; runtime3 occluded crops REJECTED',history='Prior AV/c000027b/UNKNOWN/device-reset/AOT/performance remain; Legacy worst-case O(N^2) not signed off'))
print('Evidence verified; runtime admission FAIL_CONTINUOUS_LATEST. Archive bytes', (D/'runtime-evidence.zip').stat().st_size)
