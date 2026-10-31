"""Collect bounded exit evidence; this is not a product PASS assertion."""
from pathlib import Path
import json,hashlib,subprocess,zipfile
r=Path.cwd();d=Path(__file__).resolve().parent;o=r/"artifacts/legacy-exit34a1";f=o/"full"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rounds=[]
for name in ["round1","round2"]:
 p=f/name;events=[json.loads(l) for l in (p/"events.jsonl").read_text().splitlines()]
 exit=read(p/"retainedexit.json");wer=read(p/"wer.json")
 assert exit["retained"] and not exit["forced"] and wer["secondsAfterExit"]>=31
 assert exit["exitCode"]==0 and not wer["matches"]
 markers=[e for e in events if e["data"]["kind"] in ["close-enter","closed"] or e["data"]["kind"].startswith("exit-")]
 errors=[e for e in events if any(t in e["data"]["kind"] for t in ["error","exception","first-chance","unhandled","unobserved"])]
 assert not errors
 assert [e["data"]["kind"] for e in markers][-6:]==["exit-enter","exit-close-complete","exit-observer-stopped","exit-tray-released","exit-control-closed","exit-returned"]
 rounds.append(dict(name=name,exit=exit,wer=wer,markers=markers,exceptions=errors))
b=o/"kernel-initial"
for name,args in [("head",["rev-parse","HEAD"]),("status",["status","--porcelain"]),("index",["diff","--cached","--binary"]),("worktree",["diff","--binary"])]:
 assert (b/(name+".txt")).read_bytes()==subprocess.check_output(["git","-C",str(r/"externals/bili-kernel"),*args]),name
with zipfile.ZipFile(b/"untracked.zip") as z:
 names=set(filter(None,subprocess.check_output(["git","-C",str(r/"externals/bili-kernel"),"ls-files","--others","--exclude-standard","-z"]).decode().split("\0")))
 assert names==set(z.namelist())
 for n in names:assert z.read(n)==(r/"externals/bili-kernel"/n).read_bytes()
original=read(o/"original/retainedexit.json");assert original["exitCode"]==-1073741189
hashes={}
for name in ["Danmaku.Legacy.dll","Danmaku.Share.dll","Danmaku.Core.dll","BiliCopilot.UI.dll"]:
 paths=[f/"layout"/name,f/"layout2"/name,f/"src/_build/x64/Release/BiliCopilot.UI/bin"/name]
 values=[sha(p) for p in paths];assert len(set(values))==1
 hashes[name]=values[0]
assert hashes["Danmaku.Legacy.dll"]=="7d6e49171fe084e9787cf40d31166638ee3fa7e2e362bfafa4d64b2e13fd45c1"
cleanup=read(o/"cleanup.json");assert cleanup["packages"]==0 and cleanup["processes"]==0
report=dict(verdict="Original failure remains unexplained; no production or host lifecycle fix",original=original,rounds=rounds,hashes=hashes,source=read(o/"source-comparison.json"),kernelInitialPreserved=True,cleanup=cleanup)
(d/"validation.json").write_text(json.dumps(report,indent=2))
paths=[*sorted((o/"original").glob("*")),o/"full-build.log",o/"source-comparison.json",o/"cleanup.json",o/"cleanup-yield.json"]
for n in ["round1","round2"]:paths+=sorted((f/n).glob("*.json"))+[f/n/"events.jsonl"]
manifest={str(p.relative_to(o)):sha(p) for p in paths}
with zipfile.ZipFile(d/"evidence.zip","w",zipfile.ZIP_DEFLATED) as z:
 for p in paths:z.write(p,p.relative_to(o))
(d/"evidence-manifest.json").write_text(json.dumps(manifest,indent=2))
print("Evidence verified: original abnormal exit retained; two diagnostic exit 0; kernel unchanged; owned cleanup complete.")
