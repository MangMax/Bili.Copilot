"""Offline closure only. Does not launch/register apps or issue desktop input."""
from pathlib import Path
import json,hashlib,subprocess,zipfile,ast,shutil,tarfile,io
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/combined-60cd';F=O/'full';A=F/'src/Desktop/BiliCopilot.UI';B=F/'src/_build/x64/Release/BiliCopilot.UI/bin'
sha=lambda b:hashlib.sha256(b).hexdigest()
git=lambda *a:subprocess.check_output(['git',*a],cwd=R)
def save(n,x):(D/n).write_text(json.dumps(x,indent=2),encoding='utf8')
packages=json.loads((O/'package-audit/packages.json').read_text())
assets=json.loads((A/'obj/project.assets.json').read_text())
libs={k:v for k,v in assets['libraries'].items() if k.startswith('Danmaku.')}
assert set(libs)=={'Danmaku.Core/0.0.1-pxdpi.9dc8c6c9.clean','Danmaku.Legacy/0.0.1-reconstruct0b4f.final','Danmaku.Share/0.0.1-reconstruct0b4f.final'}
for lib,r in packages.items():
 p=B/f'Danmaku.{lib}.dll';assert sha(p.read_bytes())==r['dllSha']
 cache=next(Path(folder)/libs[k]['path'] for folder in assets['packageFolders'] for k in libs if k.startswith('Danmaku.'+lib+'/') and (Path(folder)/libs[k]['path']).exists())
 cached=next(cache.rglob(f'Danmaku.{lib}.dll'));assert sha(cached.read_bytes())==r['dllSha']
 r.update(build=str(p),buildSha=sha(p.read_bytes()),cache=str(cached),cacheSha=sha(cached.read_bytes()))
 shutil.copyfile(O/f'package-audit/Danmaku.{lib}.pdb',B/f'Danmaku.{lib}.pdb')
save('package-closure.json',dict(packages=packages,resolved=libs,targets={t:{k:v for k,v in vs.items() if k.startswith('Danmaku.')} for t,vs in assets['targets'].items()},actualLoaded='NOT RUN: desktop held for AOT task',cache='Existing shared NuGet cache, not a clean-machine restore'))
with tarfile.open(fileobj=io.BytesIO(git('archive','44ce6cc5','src'))) as t:
 baseline={m.name:t.extractfile(m).read() for m in t.getmembers() if m.isfile() and m.name.endswith(('.cs','.xaml'))}
report=[]
for n,expected in baseline.items():
 if not n.endswith(('.cs','.xaml')):continue
 actual=(F/n).read_bytes()
 norm=lambda b:b.removeprefix(b'\xef\xbb\xbf').replace(b'\r\n',b'\n')
 report.append(dict(path=n,baselineSha=sha(expected),actualSha=sha(actual),normalizedEqual=norm(expected)==norm(actual)))
diff=[r['path'] for r in report if not r['normalizedEqual']]
assert set(diff)=={'src/Desktop/BiliCopilot.UI/App.xaml.cs','src/Desktop/BiliCopilot.UI/GlobalDependencies.cs'},diff
save('app-source-closure.json',dict(base='44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede',files=report,differences=diff,extra=['OfflineAcceptance.cs']))
# Compare content, not ancestry: reviewed speed changes were integrated independently.
speed='src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel/PlayerViewModel.Speed.cs'
assert (F/speed).read_text(encoding='utf-8-sig')==(R/speed).read_text(encoding='utf-8-sig')
overlay='src/Desktop/BiliCopilot.UI/Controls/Core/OverlayTransportControls.xaml.cs'
patch=git('diff','44ce6cc5','HEAD','--',overlay).decode('utf8')
save('application-baseline.json',dict(speedPartialMatchesParent=True,overlayDiff=patch,note='Only Overlay seek path differs; 44ce retains reviewed explicit short-seek routing. Speed and unload lifecycle are preserved. No HEAD production file overlays applied.'))
backup=O/'kernel-backup';kernel={}
for n,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 current=git('-C',str(R/'externals/bili-kernel'),*args);assert current==(backup/n).read_bytes(),n;kernel[n]=sha(current)
assert '0ae5e4e000a41a07f6696746706df11c06e8835b' in git('ls-tree','HEAD','externals/bili-kernel').decode()
save('kernel-unchanged.json',kernel)
for p in D.glob('*.py'):ast.parse(p.read_text(encoding='utf8'),filename=str(p))
assert json.loads((O/'build-command.json').read_text())['exit']==0
log=(O/'build.log').read_text(encoding='utf8');assert 'NU1605' not in log and 'NU1608' not in log
for n in ['share-comparison.json','share-normalized.json','initial.json','build-command.json','build.log','prepare.log']:shutil.copyfile(O/n,D/n)
for n in ['metadata.json','packages.json']:shutil.copyfile(O/'package-audit'/n,D/n)
save('status.json',dict(offlinePreparation='PASS',runtimeAdmission='PENDING_NOT_EXECUTED',desktop='Never acquired, no launch, registration or UI input; hold per user steering',build=dict(exit=0,warnings=1,errors=0),singleEngineMatrices='Not rerun',performance='Not evaluated; Legacy O(N^2) worst case remains',history='All prior FAIL/UNKNOWN preserved',remaining=['actual DLL loading and owned identity','paused Core-Legacy-Core; hidden/playing switches','four-mode positions/ages/remaining lifetimes','short forward/backward/same/latest seek','three reload/xLoad cycles','close during controlled loading and after seek; reopen','original-pixel crops plus blank/different-text negatives','retained exit and >=31s exact PID/path WER; cleanup/yield']))
with zipfile.ZipFile(D/'offline-evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in (F/'feed').iterdir():z.write(p,'feed/'+p.name)
 for p in (O/'package-audit/source-documents').iterdir():z.write(p,'source-documents/'+p.name)
 z.write(A/'obj/project.assets.json','project.assets.json')
 for p in (O/'package-audit').glob('*.nuspec'):z.write(p,'nuspec/'+p.name)
 z.write(R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/package-audit/tool/Program.cs','metadata-tool/Program.cs')
 z.write(R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/package-audit/tool/Inspect.csproj','metadata-tool/Inspect.csproj')
 for p in (O/'package-audit').glob('*.dll'):z.write(p,'binaries/'+p.name)
 for p in (O/'package-audit').glob('*.pdb'):z.write(p,'binaries/'+p.name)
save('offline-evidence-manifest.json',dict(sha256=sha((D/'offline-evidence.zip').read_bytes()),files={p.filename:sha(zipfile.ZipFile(D/'offline-evidence.zip').read(p.filename)) for p in zipfile.ZipFile(D/'offline-evidence.zip').infolist()}))
print('Offline closure PASS; runtime NOT EXECUTED; source count',len(report),'PDB documents',sum(len(x['documents']) for x in json.loads((D/'metadata.json').read_text())))
