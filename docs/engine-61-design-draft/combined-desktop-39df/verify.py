from pathlib import Path
import hashlib,json,subprocess,zipfile,tarfile,io,shutil
R=Path.cwd();O=R/'artifacts/combined-60cd';F=O/'full';D=R/'docs/engine-61-design-draft/combined-desktop-39df';B=F/'src/_build/x64/Release/BiliCopilot.UI/bin'
sha=lambda b:hashlib.sha256(b).hexdigest()
git=lambda *a:subprocess.check_output(['git',*a],cwd=R)
save=lambda n,x:(D/n).write_text(json.dumps(x,indent=2),encoding='utf8')
kernel={}
for n,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 current=git('-C',str(R/'externals/bili-kernel'),*args);assert current==(O/'kernel-backup'/n).read_bytes(),n;kernel[n]=sha(current)
assert '0ae5e4e000a41a07f6696746706df11c06e8835b' in git('ls-tree','HEAD','externals/bili-kernel').decode()
save('kernel-unchanged.json',kernel)
with tarfile.open(fileobj=io.BytesIO(git('archive','44ce6cc5','src'))) as t:
 baseline={m.name:t.extractfile(m).read() for m in t.getmembers() if m.isfile() and m.name.endswith(('.cs','.xaml'))}
norm=lambda b:b.removeprefix(b'\xef\xbb\xbf').replace(b'\r\n',b'\n')
report=[dict(path=n,baselineSha=sha(b),actualSha=sha((F/n).read_bytes()),equal=norm(b)==norm((F/n).read_bytes())) for n,b in baseline.items()]
diff=[x['path'] for x in report if not x['equal']];assert set(diff)=={'src/Desktop/BiliCopilot.UI/App.xaml.cs','src/Desktop/BiliCopilot.UI/GlobalDependencies.cs'}
save('app-source-closure.json',dict(base='44ce6cc5',files=report,differences=diff,fixtureSha=sha((F/'src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs').read_bytes())))
assets=json.loads((F/'src/Desktop/BiliCopilot.UI/obj/project.assets.json').read_text())
libs={k:v for k,v in assets['libraries'].items() if k.startswith('Danmaku.')}
assert set(libs)=={'Danmaku.Core/0.0.1-pxdpi.9dc8c6c9.clean','Danmaku.Legacy/0.0.1-reconstruct0b4f.final','Danmaku.Share/0.0.1-reconstruct0b4f.final'}
packages=[]
for lib in ['Core','Legacy','Share']:
 pkg=next((F/'feed').glob(f'Danmaku.{lib}.*.nupkg'))
 frozen=O.parent/'combined-restored-60cd/binaries'/f'Danmaku.{lib}.dll'
 with zipfile.ZipFile(pkg) as z:
  data=z.read(next(n for n in z.namelist() if n.endswith(f'/Danmaku.{lib}.dll')))
 assert sha(data)==sha(frozen.read_bytes())==sha((B/f'Danmaku.{lib}.dll').read_bytes())
 pdb=frozen.with_suffix('.pdb');shutil.copyfile(pdb,B/pdb.name)
 packages.append(dict(name=lib,package=str(pkg),packageSha=sha(pkg.read_bytes()),dllSha=sha(data),pdbSha=sha(pdb.read_bytes())))
save('package-closure.json',dict(packages=packages,resolved=libs,targets={t:{k:v for k,v in vs.items() if k.startswith('Danmaku.')} for t,vs in assets['targets'].items()}))
for name in ['build-command.json','build.log','copy-source-verified.json','packages-before.json','processes-before.json']:
 shutil.copyfile(O/name,D/name)
assert json.loads((O/'build-command.json').read_text())['exit']==0
print('Local source, kernel and frozen package closure verified.')
