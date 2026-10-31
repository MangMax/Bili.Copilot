from pathlib import Path
import subprocess,json,time,os,sys
R=Path(__file__).resolve().parent/'full'; E=R/'runtime';os.environ['WINAPP_UI_WORKFLOW_ID']='remount-6273-full';records=[]
identity=json.loads((E/'identity.json').read_text(encoding='utf-8-sig'));pid=identity['pid']
def ui(*a,w=None):
 cmd=['winapp','ui',*map(str,a),*(['-w',str(w)] if w else ['-a',str(pid)]),'--on','local','--json']
 p=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8');records.append({'utc':time.time(),'cmd':cmd,'exit':p.returncode,'stdout':p.stdout,'stderr':p.stderr});(E/('commands-screen-'+sys.argv[1]+'.json')).write_text(json.dumps(records,indent=2));assert p.returncode==0,p.stdout+p.stderr
 return json.loads(p.stdout)
def data():return [json.loads(x)['data'] for x in (R/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
def latest(k):return next(x for x in reversed(data()) if x['kind']==k)
def wait(pred):
 end=time.time()+15
 while time.time()<end:
  if pred():return
  time.sleep(.2)
 raise AssertionError('state timeout')
windows=ui('list-windows')
end=time.time()+30
while not any(x['title']=='Offline full player 224' for x in windows):
 assert time.time()<end,'player startup timeout'
 time.sleep(.5);windows=ui('list-windows')
(E/('windows-'+sys.argv[1]+'.json')).write_text(json.dumps(windows,indent=2))
ctrl=next(x['hwnd'] for x in windows if 'controller' in x['title']);player=next(x['hwnd'] for x in windows if 'controller' not in x['title'] and x['title']=='Offline full player 224')
def invoke(s,w=ctrl):ui('invoke',s,w=w)
def snap(n):
 before={'utc':time.time(),'state':latest('state'),'visual':latest('visual'),'windows':ui('list-windows')}
 ui('screenshot','--capture-screen','--output',str(E/('screen-'+n+'.png')),w=player)
 (E/('screen-'+n+'.json')).write_text(json.dumps({'state':latest('state'),'visual':latest('visual'),'utc':time.time(),'before':before,'windowsAfter':ui('list-windows')},indent=2))
if sys.argv[1]=='matrix':
 wait(lambda:latest('state')['state'] in ['Playing','Paused'])
 if latest('state')['state']=='Playing':
  invoke('ShowControls');invoke('PlayPauseButton',w=player)
 wait(lambda:latest('state')['state']=='Paused')
 invoke('Seek20');time.sleep(.7);snap('baseline20')
 for i in range(1,4):
  before=latest('visual')['master'];invoke('UnloadDanmaku');wait(lambda:not latest('visual').get('renderer',False));time.sleep(.4);snap(f'unloaded{i}')
  invoke('LoadDanmaku');wait(lambda:latest('visual').get('renderer',False));time.sleep(1);snap(f'remount{i}')
  time.sleep(3);snap(f'remount{i}-stable')
  time.sleep(1);snap(f'remount{i}-static1')
  time.sleep(1);snap(f'remount{i}-static2');v=latest('visual');s=latest('state')
  assert v['master']!=before and v['paused'] and v['time']==20000 and v['rows'] and s['state']=='Paused' and s['position']==20,(s,v)
 invoke('ShowControls');invoke('ForwardButton',w=player);time.sleep(.7);snap('button-forward1')
 assert latest('state')['state']=='Paused' and latest('state')['position']==21
 invoke('BackwardButton',w=player);time.sleep(.7);snap('button-backward1')
 assert latest('state')['state']=='Paused' and latest('state')['position']==20
 ui('inspect',w=player)
 print('matrix completed')
elif sys.argv[1]=='close':
 invoke('CloseFull');wait(lambda:any(x['kind']=='closed' for x in data()));time.sleep(2);invoke('ExitOwned');print('exit requested')
