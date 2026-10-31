"""Verify retained evidence, including failures. Exit zero is NOT product acceptance."""
from pathlib import Path
import hashlib,json,zipfile
D=Path(__file__).resolve().parent
with zipfile.ZipFile(D/'evidence.zip') as z:
 manifest=json.loads((D/'evidence-manifest.json').read_text())
 assert set(z.namelist())==set(manifest)
 for n,m in manifest.items():
  b=z.read(n);assert len(b)==m['bytes'] and hashlib.sha256(b).hexdigest()==m['sha256'],n
 def read(n):return json.loads(z.read(n))
 def events(phase,run='native'):return [json.loads(x) for x in z.read(f'{phase}/evidence/{run}/events.jsonl').splitlines()]
 red=events('red');green=events('green');matrix=events('matrix','native2')
 def event(rows,name):return next(x['data'] for x in rows if x['name']==name)
 r=event(red,'BeforeReady');assert r['device']==False and r['index']==1 and r['Time']==20000 and r['Paused'] and r['rows']=='[]'
 r=event(red,'AfterReady');assert r['device'] and r['index']==1 and r['rows']=='[]'
 assert not event(red,'NotReadyPausedTargetVisibleAfterReady')['passed']
 r=event(green,'BeforeReady');assert not r['device'] and r['index']==0
 assert event(green,'NotReadyPausedTargetVisibleAfterReady')['passed']
 for phase,run in [('red','native'),('green','native'),('matrix','native'),('matrix','native2')]:
  prefix=f'{phase}/evidence/{run}/'
  clean=read(prefix+'cleanup.json');assert clean['packagesRemaining']==0 and not clean['processRemaining']
  ex=read(prefix+'retainedexit.json');assert ex['retainedHandle'] and not ex['forced']
  wer=read(prefix+'wer.json');assert wer['applicationLogReadable'] and wer['secondsAfterExit']>=31
  if run=='native2':
   assert ex['exitCode']==-1073741819 and wer['matches']
   assert any('c0000005' in x['xml'] and 'Microsoft.Graphics.Canvas.dll' in x['xml'] and ex['path'] in x['xml'] and f"0x{ex['pid']:x}" in x['xml'] for x in wer['matches'])
  else:assert ex['exitCode']==0 and wer['matches']==[]
 expected=['NotReadyPausedTargetVisibleAfterReady','PauseKeepsTargetFrame','ReadyPlaybackAndResume','ResumedItemsExpire','BeforeReadyOnlyLatestTarget']
 assert [x['name'] for x in matrix if x.get('data') and isinstance(x['data'],dict) and 'passed' in x['data']]==expected
 assert all(event(matrix,n)['passed'] for n in expected)
 assert not any(x['name'] in ['BeforeReadyCloseNeverPublishes','SuiteCompleted'] for x in matrix)
 assert read('matrix/evidence/native2/results.json')['executionFailed']
 compat=read('clean/compat-results/results.json');assert len(compat)==23 and all(x['passed'] for x in compat)
 assert z.read('clean/compat-results/public-api.txt')==(D.parent/'legacy-paused-seek/public-api-baseline.txt').read_bytes()
validation=json.loads((D/'validation.json').read_text());assert validation['unchangedProductionCsXaml']==901
for n,h in validation['source'].items():assert hashlib.sha256((D/'source'/n).read_bytes()).hexdigest()==h
print(f'Evidence integrity OK: {len(manifest)} files. Red retained. Matrix incomplete with native AV. Full-app runtime NOT RUN.')
