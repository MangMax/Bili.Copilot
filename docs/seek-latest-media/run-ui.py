"""Prepared, NOT EXECUTED. Run only after the user's desktop hold is lifted.
Run after launch-retain.ps1 records identity.json. Never selects another PID.
Screenshots need subsequent original-pixel review; state PASS is not visual PASS.
"""
from pathlib import Path
import json,subprocess,time,os,sys
R=Path.cwd();F=R/'artifacts/latest-seek/full';E=F/sys.argv[1]
identity=json.loads((E/'identity.json').read_text(encoding='utf-8-sig'))
assert Path(identity['path']).resolve().is_relative_to(F.resolve())
pid=identity['pid'];os.environ['WINAPP_UI_WORKFLOW_ID']='latest-a1de'
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
wait(lambda:any('controller' in x.get('title','') for x in ui('list-windows')),60)
windows=ui('list-windows');ctrl=next(x['hwnd'] for x in windows if 'controller' in x.get('title',''))
def invoke(name):ui('invoke',name,w=ctrl)
def player():return next(x['hwnd'] for x in ui('list-windows') if x.get('title')=='Offline full player 224')
def check(name,p,detail=None,fatal=True):
 results.append(dict(name=name,pass_=bool(p),detail=detail));write('state-results.json',results)
 if fatal:assert p,(name,detail)
def snap(name):
 before=dict(state=latest('state'),visual=latest('visual'),utc=time.time())
 for attempt in range(3):
  try:
   ui('screenshot','--capture-screen','--output',str(E/(name+'.png')),w=player());break
  except AssertionError as ex:
   if 'foreground_not_target' not in str(ex) or attempt==2:raise
   write(name+'-capture-attempt'+str(attempt)+'.json',dict(error=str(ex),usable=False));time.sleep(.7)
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
 from assertions import positions
 detail=positions(v);check(engine+'ConfiguredPositions',all(x['pass_'] for x in detail),detail)
 return v
try:
 pause();invoke('Config');seek20()
 for engine in ['Core','Legacy']:
  invoke(engine);wait(lambda:latest('visual').get('engine')==engine);time.sleep(.8);seek20()
  baseline(engine)
  for iteration in range(10):
   invoke('FastLatest');time.sleep(2)
   state=latest('state');visual=latest('visual')
   check(engine+'Latest'+str(iteration),state.get('position')==20 and state.get('danmakuPosition')==20 and state.get('state')=='Paused' and visual.get('time')==20000,dict(state=state,visual=visual),fatal=False)
  snap(engine.lower()+'-latest20')
  invoke('Repeated');time.sleep(2);baseline(engine);check(engine+'RapidSequential',True,latest('state'))
  invoke('ShowControls');ui('invoke','ForwardButton',w=player());wait(lambda:latest('state').get('position')==21)
  invoke('ShowControls');ui('invoke','BackwardButton',w=player());wait(lambda:latest('state').get('position')==20)
  invoke('Seek20');time.sleep(.8);baseline(engine);check(engine+'ShortAndSame',True,latest('state'))
  snap(engine.lower()+'-short-same20')
  count=sum(e['data']['kind']=='closed' for e in events())
  invoke('QueuedClose');wait(lambda:sum(e['data']['kind']=='closed' for e in events())>count)
  check(engine+'QueuedSeekClose',latest('closed').get('disposed') is True,latest('closed'))
  invoke('OpenFull');pause();seek20()
 check('ActualLoadedThreeModules',len(latest('loaded-modules').get('modules',[]))==3,latest('loaded-modules'))
 errors=[e for e in events() if e['data']['kind'] in ['action-error','observe-error','startup-error','unhandled']]
 check('NoRuntimeErrors',not errors,errors,fatal=False)
 invoke('CloseFull');time.sleep(.5);invoke('ExitOwned')
finally:
 p=subprocess.run(['winapp','ui','yield','--on','local','--json'],capture_output=True,text=True,encoding='utf8')
 write('yield.json',dict(exit=p.returncode,stdout=p.stdout,stderr=p.stderr))
