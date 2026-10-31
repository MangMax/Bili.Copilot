from pathlib import Path
import subprocess,json,time,os
r=Path.cwd()/'artifacts/legacy-logger-review4e91/full';out=r/'runtime';pid=json.loads((out/'identity.json').read_text(encoding='utf-8-sig'))['pid'];os.environ['WINAPP_UI_WORKFLOW_ID']='logger-review4e91';results=[]
def ui(*args):
 args=list(map(str,args));p=subprocess.run(['winapp','ui',*args,'-a',str(pid),'--json'],capture_output=True,text=True,encoding='utf-8');results.append({'args':args,'exit':p.returncode,'output':p.stdout,'error':p.stderr});(out/'batch-final.json').write_text(json.dumps(results,indent=2));assert p.returncode==0,p.stdout
ui('screenshot','-w','73010656','--output',out/'repeated-stable.png')
ui('invoke','PlayPauseButton');time.sleep(.7)
ui('screenshot','-w','73010656','--output',out/'resume-remaining.png');time.sleep(3.6)
ui('screenshot','-w','73010656','--output',out/'resume-expired.png')
ui('invoke','PlayPauseButton');time.sleep(.5)
ui('invoke','UnloadDanmaku');time.sleep(.8)
ui('invoke','LoadDanmaku');time.sleep(1.2)
ui('screenshot','-w','73010656','--output',out/'remounted.png')
print('batch completed')
