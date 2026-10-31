import pathlib,subprocess,json,os,time
root=pathlib.Path.cwd()/'artifacts/overlay-review/full';out=root/'runtime';pid=json.loads((out/'identity.json').read_text(encoding='utf-8-sig'))['pid'];os.environ['WINAPP_UI_WORKFLOW_ID']='overlay-review-5759'
results=[]
def ui(*args):
 r=subprocess.run(['winapp','ui',*map(str,args),'-a',str(pid),'--json'],capture_output=True,text=True,encoding='utf-8');results.append({'args':args,'exit':r.returncode,'output':r.stdout,'error':r.stderr});(out/'batch.json').write_text(json.dumps(results,indent=2));assert r.returncode==0,r.stdout;return json.loads(r.stdout)
def nodes(o):
 if isinstance(o,dict):
  yield o
  for v in o.values():yield from nodes(v)
 elif isinstance(o,list):
  for v in o:yield from nodes(v)
def slider():
 j=ui('inspect','-d','12');a=[x['selector'] for x in nodes(j) if x.get('type')=='Slider' and not x.get('isOffscreen')]
 if not a:
  ui('invoke','ShowControls');ui('invoke','SpeedButton');time.sleep(.35);j=ui('inspect','-d','12');a=[x['selector'] for x in nodes(j) if x.get('type')=='Slider' and not x.get('isOffscreen')]
 assert len(a)==1,a;return a[0]
def check(rate):
 time.sleep(1.3);data=[json.loads(x)['data'] for x in (root/'events.jsonl').read_text().splitlines()];state=next(x for x in reversed(data) if x['kind']=='state');assert state['state']=='Playing' and abs(state['speed']-rate)<.01,state;ui('invoke','OverlaySnapshot');print('PASS Playing slider',rate,flush=True)
ui('set-value',slider(),1.75);check(1.75)
for round,rate in [(3,2.25)]:
 ui('invoke','Remount');time.sleep(1.8);ui('focus','SpeedButton');ui('invoke','SpeedButton');time.sleep(.35);ui('set-value',slider(),rate);check(rate)
ui('screenshot','-w','23399930','--output',str(out/'speed-remount3.png'))
ui('focus','SubtitleButton');ui('invoke','SubtitleButton');time.sleep(.4)
j=ui('inspect','-d','14');(out/'subtitle-after-tree.json').write_text(json.dumps(j,indent=2));assert any(x.get('name')=='Review subtitle 5759' for x in nodes(j)),'subtitle text absent'
ui('screenshot','-w','23399930','--output',str(out/'subtitle-after.png'))
print('PASS subtitle text after third remount',flush=True)
