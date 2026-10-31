"""Same bounded playback/seek/remount/close sequence for both diagnostic rounds."""
from pathlib import Path
import json,subprocess,os,time,sys
root=Path.cwd()/"artifacts/legacy-exit34a1/full"
run=sys.argv[1]; out=root/run
pid=json.loads((out/"identity.json").read_text(encoding="utf-8-sig"))["pid"]
os.environ["WINAPP_UI_WORKFLOW_ID"]="legacy-exit34a1"
results=json.loads((out/"sequence.json").read_text()) if "--resume" in sys.argv else []
def ui(*args):
 p=subprocess.run(["winapp","ui",*map(str,args),"-a",str(pid),"--on","local","--json"],capture_output=True,text=True,encoding="utf-8")
 results.append(dict(at=time.time(),args=args,exit=p.returncode,output=p.stdout,error=p.stderr))
 (out/"sequence.json").write_text(json.dumps(results,indent=2))
 if p.returncode: raise RuntimeError(p.stdout+p.stderr)
 return p.stdout
def invoke(name,delay=1):
 ui("invoke",name);time.sleep(delay)
try:
 if "--resume" not in sys.argv:
  ui("wait-for","PauseResume","-t","30000")
  ui("inspect","--interactive")
  invoke("PauseResume")
  invoke("Seek20",3)
  invoke("Back1");invoke("Forward1");invoke("Seek21")
  invoke("Seek25",3)
  invoke("Repeated",3)
 invoke("PauseResume",3.6)
 invoke("PauseResume")
 invoke("UnloadDanmaku");invoke("LoadDanmaku")
 invoke("Seek20");invoke("Repeated",3)
 invoke("Seek25",3)
 invoke("CloseFull",2)
 invoke("ExitOwned",0)
finally:
 p=subprocess.run(["winapp","ui","yield","--on","local","--json"],capture_output=True,text=True,encoding="utf-8")
 (out/"yield.json").write_text(p.stdout)
