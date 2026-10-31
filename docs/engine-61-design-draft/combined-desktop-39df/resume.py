from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path.cwd();O=R/'artifacts/combined-60cd';O.mkdir(parents=True,exist_ok=True)
b=O/'kernel-backup';b.mkdir()
for n,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 (b/n).write_bytes(subprocess.check_output(['git','-C',str(R/'externals/bili-kernel'),*args]))
print('kernel', (b/'head').read_text().strip(),len((b/'status').read_text().splitlines()))
S=R.parent/'t3-60cd31cb/artifacts/combined-60cd/full';F=O/'full'
shutil.copytree(S,F,ignore=shutil.ignore_patterns('_build','bin','obj','.git'))
manifest={}
for p in S.rglob('*'):
 if p.is_file() and not any(x in ('_build','bin','obj','.git') for x in p.relative_to(S).parts):
  rel=str(p.relative_to(S));h=hashlib.sha256(p.read_bytes()).hexdigest();assert h==hashlib.sha256((F/rel).read_bytes()).hexdigest();manifest[rel]=h
(O/'copy-source-verified.json').write_text(json.dumps(manifest,indent=2))
for name in ['NuGet.Config','src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs']:
 p=F/name;p.write_text(p.read_text(encoding='utf-8-sig').replace(str(S),str(F)),encoding='utf8')
p=F/'src/Desktop/BiliCopilot.UI/Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('Bili.Combined60cd','Bili.Combined39df').replace('bili-combined60cd','bili-combined39df'),encoding='utf8')
shutil.copyfile(R/'docs/engine-61-design-draft/combined-acceptance/launch-retain.ps1',O/'launch-retain.ps1')
cmd=json.loads((R/'docs/engine-61-design-draft/combined-acceptance/build-command.json').read_text())['command'];cmd=[s.replace(str(S),str(F)) for s in cmd]
with (O/'build.log').open('w',encoding='utf8') as f:p=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
(O/'build-command.json').write_text(json.dumps(dict(command=cmd,exit=p.returncode),indent=2));print('build',p.returncode)
