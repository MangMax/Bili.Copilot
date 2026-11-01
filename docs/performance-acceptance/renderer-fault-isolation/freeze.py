"""Freeze small replay receipts, including failures; native payloads stay in owned artifacts."""
from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[3];target=Path(__file__).with_name('evidence.zip')
if target.exists():raise SystemExit('Refuse to overwrite existing evidence')
items={}
def add(path,name):items[name]=path.read_bytes()
small=root/'artifacts/fault-isolation'
for pattern in ('*.json','*.log','**/*.trx'):
 for p in small.glob(pattern):add(p,'tests-and-audits/'+str(p.relative_to(small)).replace('\\','/'))
for name in ('final','verified'):
 folder=root/'artifacts/home-aot'/name
 for p in folder.iterdir():
  if p.is_file():add(p,'runs/'+name+'/'+p.name)
 for p in (folder/'Logger').glob('*'):
  if p.is_file():add(p,'runs/'+name+'/Logger/'+p.name)
for name in ('native-fixed','native-final','native-verified'):
 folder=root/'artifacts/home-aot'/name
 for file in ('arguments.json','exit.txt','publish.log'):
  add(folder/file,'builds/'+name+'/'+file)
prior=root.parent/'t3-c10467bb/artifacts/home-aot/run-initial'
for p in prior.iterdir():
 if p.is_file() and p.suffix in ('.json','.jsonl','.log','.tsv','.cs'):add(p,'baseline-failed-native/'+p.name)
add(root.parent/'t3-c10467bb/docs/performance-acceptance/aot-dev-bounded-ad4d17d3.md','baseline-failed-native/report.md')
add(root/'artifacts/home-aot/fixture/obj/x64/Release/net11.0-windows10.0.22621.0/win-x64/native/BiliCopilot.UI.ilc.rsp','builds/final.ilc.rsp')
manifest=[{'path':n,'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()} for n,b in sorted(items.items())]
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
 for name,data in sorted(items.items()):z.writestr(name,data)
 z.writestr('manifest.json',json.dumps(manifest,indent=2))
with zipfile.ZipFile(target) as z:
 for item in manifest:
  b=z.read(item['path']);assert len(b)==item['bytes'] and hashlib.sha256(b).hexdigest()==item['sha256']
print(json.dumps({'path':str(target),'entries':len(manifest),'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()}))
