"""Read-only integrity audit. Exit 0 means preserved evidence, NOT acceptance PASS."""
from pathlib import Path
import json,zipfile,hashlib,xml.etree.ElementTree as ET
D=Path(__file__).resolve().parent
with zipfile.ZipFile(D/'evidence.zip') as z:
 m=json.loads((D/'manifest.json').read_text(encoding='utf-8'))
 for n,v in m.items():
  b=z.read(n);assert len(b)==v['bytes'] and hashlib.sha256(b).hexdigest()==v['sha256'],n
 def read(n):return json.loads(z.read(n).decode('utf-8-sig'))
 for run,p,f in [('lifecycle',1,4),('lifecycle2',5,0)]:
  e='immediate/evidence/'+run+'/';r=read(e+'results.json');x=read(e+'retainedexit.json');w=read(e+'wer.json')
  assert r['passed']==p and r['failed']==f and r['executionFailed']==bool(f)
  assert x['exitCode']==0 and x['retainedHandle'] and not x['forced'] and w['secondsAfterExit']>=31 and not w['matches']
  assert read(e+'cleanup.json')=={'packagesRemaining':0,'processRemaining':False}
  events=[json.loads(line) for line in z.read(e+'events.jsonl').decode('utf-8-sig').splitlines()]
  names=[x['name'] for x in events];assert 'Canvas.Loaded' not in names and 'Normal.Loaded' in names and 'Normal.Unloaded' in names
  assert any(c['args'][:2]==['ui','yield'] and c['exit']==0 for c in read(e+'commands.json'))
 e='full/runtime/';r=read(e+'results.json');x=read(e+'retainedexit.json');w=read(e+'wer.json')
 assert r['passed']==11 and r['failed']==2 and not r['acceptancePassed']
 assert [c['name'] for c in r['checks'] if not c['passed']]==['PausedRemountVisibleRound2','PausedRemountVisibleRound3']
 assert x['exitCode']==0 and x['retained'] and not x['forced'] and w['secondsAfterExit']>=31 and not w['matches']
 assert read(e+'cleanup.json')=={'packagesRemaining':0,'processRemaining':False}
 visual=read('visual-review.json');assert len(visual)==22
 for n,v in visual.items():assert v['viewed'] and hashlib.sha256(z.read(e+n)).hexdigest()==v['sha256']
 for c in read(e+'commands-screen-matrix.json'):assert c['exit']==0
 master=[]
 for i in range(1,4):
  v=read(e+f'screen-remount{i}-stable.json');master.append(v['visual']['master']);assert v['visual']['rows'] and v['visual']['paused'] and v['visual']['time']==20000 and v['state']['position']==20 and v['state']['state']=='Paused'
 assert len(set(master))==3
 for arm in ['logger','ready']:
  e='prior-evidence/'+arm+'/evidence/immediate-close/';x=read(e+'retainedexit.json');w=read(e+'wer.json');assert x['exitCode']==-1073741819 and w['secondsAfterExit']>=31
  matches=[]
  for entry in w['matches']:
   root=ET.fromstring(entry['xml']);d={n.attrib['Name']:n.text for n in root.findall('.//{*}EventData/{*}Data')}
   if d.get('AppPath')==x['path'] and int(d.get('ProcessId','0'),0)==x['pid']:matches.append(d)
  assert len(matches)==1 and matches[0]['ExceptionCode']=='c0000005'
 assert read('kernel-preserved.json')['allFourSnapshotsUnchanged']
 assert read('validation.json')['unchangedProductionFiles']==901
print('Evidence integrity PASS:',len(m),'files; lifecycle 5/5; full app 11 PASS / 2 visual FAIL; historical AV retained.')
