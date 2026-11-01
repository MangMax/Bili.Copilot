"""Verify the sealed revision, source/PDB closure and bounded owned native run."""
from pathlib import Path
from datetime import datetime
import json,hashlib,zipfile,xml.etree.ElementTree as ET
D=Path(__file__).resolve().parent
sha=lambda b:hashlib.sha256(b).hexdigest()
decode=lambda b:json.loads(b.decode('utf-8-sig'))
for n,h in decode((D/'manifest.json').read_bytes()).items():assert sha((D/n).read_bytes())==h,n
modules={}
for p in decode((D/'packages.json').read_bytes()):
 assert sha((D/p['path']).read_bytes())==p['sha256']
 if p['path'].endswith('.nupkg'):
  with zipfile.ZipFile(D/p['path']) as z:
   for n in z.namelist():
    if n.endswith('.dll'):modules[Path(n).stem]=sha(z.read(n))
   r=ET.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))))
   for e in r.findall('.//{*}dependency'):
    if e.attrib['id']=='Danmaku.Share':assert e.attrib['version']=='0.0.1-seekd1fc.20261101.2'
with zipfile.ZipFile(D/'source-snapshot.zip') as z:
 for a in decode(z.read('metadata.json')):
  assert modules[Path(a['path']).stem]==a['sha256'].lower()
  for d in a['documents']:
   b=z.read('source-documents/'+d['hash']+'.source');assert sha(b)==d['hash'].lower()
   n=d['source'].replace('\\','/').split('/seek-fix/',1)[1]
   if '/obj/' not in n:assert z.read(n).replace(b'\r\n',b'\n')==b.replace(b'\r\n',b'\n'),n
with zipfile.ZipFile(D/'evidence.zip') as z:
 ex,wer,cleanup=[decode(z.read('native/'+n+'.json')) for n in ['retainedexit','wer','cleanup']]
 assert ex['exitCode']==0 and ex['retained'] and not ex['forced']
 assert wer['applicationLogReadable'] and wer['secondsAfterExit']>=31 and not wer['matches']
 assert cleanup['remaining']==0 and not cleanup['forced']
 events=[json.loads(l) for l in z.read('native/events.jsonl').decode('utf-8-sig').splitlines()]
 data=[e['data'] for e in events];checks=[d for d in data if d['kind']=='assert']
 assert checks and all(c['passed'] for c in checks)
 assert not any(d['kind'].endswith('error') or d['kind']=='unhandled' for d in data)
 loaded=next(d['modules'] for d in data if d['kind']=='loaded-modules')
 assert {m['name']:m['sha'].lower() for m in loaded}==modules
 required=['DenseRapidSeekLatest','ReplacedListRejectsOldRebuild','NativePauseFreezesPositionAndAge','NativeResumeExpiresShortButKeepsLong','CloseDuringRebuildResponsive']
 assert all(any(c['name']==n for c in checks) for n in required)
 metrics=[]
 for i,e in enumerate(events):
  d=e['data']
  if d['kind'] not in ['visible-perf','long-visible','close-during-rebuild','hide-during-rebuild']:continue
  row=dict(d)
  if d['kind']=='visible-perf':
   j=max(j for j in range(i) if data[j]['kind']=='media-command-return' and data[j].get('position')==55 and data[j].get('succeeded'))
   q=next(q for q in events[j+1:i] if q['data']['kind']=='progress-vm' and q['data']['position']==55)
   row['mediaAcknowledgedToProgressMs']=(datetime.fromisoformat(q['at'])-datetime.fromisoformat(events[j]['at'])).total_seconds()*1000
   assert row['callMs']<100 and row['gap']<200 and row['mediaAcknowledgedToProgressMs']<100
  metrics.append(row)
 assert {m['count'] for m in metrics if m['kind']=='visible-perf'}=={300,12000}
 assert next(m for m in metrics if m['kind']=='close-during-rebuild')['pending']
 before,after=[decode(z.read('protected-'+n+'.json')) for n in ['before','after']]
 assert sorted(before,key=lambda p:p['Name'])==sorted(after,key=lambda p:p['Name'])
 before,after=[decode(z.read('process-'+n+'.json')) for n in ['before','after']]
 assert before==after
 trx=ET.fromstring(z.read('tests/replay44.trx'));counts=trx.find('.//{*}Counters').attrib
 assert counts['passed']=='44' and counts['failed']=='0' and counts['notExecuted']=='0'
 green=z.read('tests/tests-green.txt').decode('utf-8-sig');assert green.count('PASS ')==13 and 'Failed: 0' in green
 exits=decode(z.read('tests/probe-exits.json'));assert exits==dict(red=1,green=0)
 assert z.read('kernel-before.txt')==z.read('kernel-after.txt')
 summary=dict(version='0.0.1-seekd1fc.20261101.2',regressions=counts,probes=13,nativeAssertions=len(checks),nativeMetrics=metrics,modules=modules,wer=wer)
(D/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
print(json.dumps(summary,indent=2))
