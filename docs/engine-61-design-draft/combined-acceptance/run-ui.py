"""Prepared, NOT EXECUTED. Run only after the user's desktop hold is lifted.
Run after launch-retain.ps1 records identity.json. Never selects another PID.
Screenshots need subsequent original-pixel review; state PASS is not visual PASS.
"""
from pathlib import Path
import json,subprocess,time,os,sys
R=Path.cwd();F=R/'artifacts/combined-60cd/full';E=F/sys.argv[1]
identity=json.loads((E/'identity.json').read_text(encoding='utf-8-sig'))
assert Path(identity['path']).resolve().is_relative_to(F.resolve())
pid=identity['pid'];os.environ['WINAPP_UI_WORKFLOW_ID']='combined-60cd-full'
records=[];results=[]
def write(n,x):(E/n).write_text(json.dumps(x,indent=2),encoding='utf8')
def ui(*args,w=None):
 cmd=['winapp','ui',*map(str,args),*(['-w',str(w)] if w else ['-a',str(pid)]),'--on','local','--json']
 p=subprocess.run(cmd,capture_output=True,text=True,encoding='utf8')
 records.append(dict(utc=time.time(),command=cmd,exit=p.returncode,stdout=p.stdout,stderr=p.stderr));write('commands.json',records)
 assert p.returncode==0,p.stdout+p.stderr
 return json.loads(p.stdout)
def events():
 lines=(F/'events.jsonl').read_text(encoding='utf-8-sig').splitlines();out=[]
 for line in lines:
  try:out.append(json.loads(line))
  except json.JSONDecodeError:pass # Concurrent append may leave a trailing partial line.
 return out
def latest(k):return next((x['data'] for x in reversed(events()) if x['data']['kind']==k),{})
def wait(p,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  if p():return
  time.sleep(.15)
 raise AssertionError('state timeout: '+json.dumps(latest('state')))
windows=ui('list-windows');ctrl=next(x['hwnd'] for x in windows if 'controller' in x.get('title',''))
def invoke(name):ui('invoke',name,w=ctrl)
def player():return next(x['hwnd'] for x in ui('list-windows') if x.get('title')=='Offline full player 224')
def check(name,p,detail=None):
 results.append(dict(name=name,pass_=bool(p),detail=detail));write('state-results.json',results)
 assert p,(name,detail)
def snap(name):
 before=dict(state=latest('state'),visual=latest('visual'),utc=time.time())
 ui('screenshot','--capture-screen','--output',str(E/(name+'.png')),w=player())
 write(name+'.json',dict(before=before,after=dict(state=latest('state'),visual=latest('visual'),utc=time.time())))
def pause():
 wait(lambda:latest('state').get('state') in ['Playing','Paused'])
 if latest('state')['state']=='Playing':invoke('PauseResume')
 wait(lambda:latest('state').get('state')=='Paused')
def seek20():
 invoke('Seek20');wait(lambda:latest('state').get('position')==20);time.sleep(.8)
def baseline(engine):
 v=latest('visual');s=latest('state');rows=v.get('rows',[])
 check(engine+'TargetFourModes',v.get('engine')==engine and v.get('paused') and s.get('state')=='Paused' and s.get('position')==20 and {r['text'] for r in rows}=={'ROLL18','REV18','PRIOR19','BOTTOM19'},dict(state=s,visual=v))
 check(engine+'TargetAges',all(abs(r['age']-(2000 if r['text'] in ['ROLL18','REV18'] else 1000))<2 for r in rows),rows)
 return v
try:
 pause();invoke('Config');seek20();baseline('Core');snap('core20')
 invoke('Legacy');wait(lambda:latest('visual').get('engine')=='Legacy');time.sleep(1);baseline('Legacy');snap('legacy20')
 invoke('Core');wait(lambda:latest('visual').get('engine')=='Core');time.sleep(1);v=baseline('Core');snap('core-return20')
 time.sleep(3.3);v2=baseline('Core');check('CorePausedStable',v['rows']==v2['rows'],[v,v2]);snap('core-stable20')
 for engine in ['Legacy','Core']:
  invoke(engine);time.sleep(1);seek20();base=baseline(engine);snap(engine.lower()+'-positive')
  invoke('Blank');time.sleep(.8);snap(engine.lower()+'-blank');check(engine+'BlankRows',not latest('visual').get('rows'),latest('visual'))
  invoke('Config');invoke('Seek21');time.sleep(.8);snap(engine.lower()+'-other');check(engine+'OtherText',any(r['text']=='FUTURE21' for r in latest('visual')['rows']))
  seek20();baseline(engine)
  invoke('ShowControls');ui('invoke','ForwardButton',w=player());wait(lambda:latest('state').get('position')==21)
  invoke('ShowControls');ui('invoke','BackwardButton',w=player());wait(lambda:latest('state').get('position')==20)
  invoke('Seek20');time.sleep(.8);baseline(engine);invoke('FastLatest');time.sleep(1);baseline(engine)
  check(engine+'ShortSameLatest',True,latest('state'))
 invoke('HideDanmaku');invoke('PauseResume');wait(lambda:latest('state').get('state')=='Playing')
 invoke('Legacy');time.sleep(.6);invoke('Core');time.sleep(.6);pause();seek20();invoke('ShowDanmaku');time.sleep(1);baseline('Core');snap('hidden-playing-restored')
 for i in range(1,4):
  old=latest('visual')['master'];invoke('UnloadDanmaku');wait(lambda:not latest('visual').get('renderer',False));snap(f'unloaded{i}')
  invoke('LoadDanmaku');time.sleep(1);v=baseline('Core');check(f'XLoadNewInstance{i}',v['master']!=old);snap(f'remount{i}')
  invoke('Remount');time.sleep(2);check(f'OverlayReload{i}',latest('overlay-snapshot').get('active') is True,latest('overlay-snapshot'))
 invoke('Seek21');time.sleep(.2);invoke('CloseFull');wait(lambda:latest('closed').get('disposed') is True)
 check('CloseAfterSeek',True,latest('closed'))
 invoke('OpenFull');pause();seek20();baseline('Core');snap('reopened')
 invoke('CloseFull');time.sleep(1);count=sum(x['data']['kind']=='close-enter' for x in events())
 invoke('OpenHeld');wait(lambda:latest('service-held').get('method')=='GetVideoPageDetailAsync')
 invoke('CloseFull');wait(lambda:sum(x['data']['kind']=='close-enter' for x in events())>count)
 check('ControlledLoadingClosePending',latest('close-enter').get('pendingInitialization') is True,latest('close-enter'))
 invoke('ReleaseService');time.sleep(1);check('ControlledLoadingCloseDisposed',latest('closed').get('disposed') is True,latest('closed'))
 invoke('OpenFull');pause();seek20();baseline('Core');snap('reopened-after-held')
 invoke('CloseFull');time.sleep(1);invoke('ExitOwned')
finally:
 # Keep process ownership/retained-exit recovery separate; do not kill anything.
 p=subprocess.run(['winapp','ui','yield','--on','local','--json'],capture_output=True,text=True,encoding='utf8')
 write('yield.json',dict(exit=p.returncode,stdout=p.stdout,stderr=p.stderr))
