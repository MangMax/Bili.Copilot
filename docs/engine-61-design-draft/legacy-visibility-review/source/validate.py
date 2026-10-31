from pathlib import Path
import json,hashlib,subprocess,io,tarfile,zipfile
R=Path.cwd();O=R/'artifacts/remount-6273';F=O/'full';sha=lambda b:hashlib.sha256(b).hexdigest(); norm=lambda b:b.removeprefix(b'\xef\xbb\xbf').replace(b'\r\n',b'\n')
result={}
for n in ['DanmakuRender.cs','DanmakuFrostMaster.cs']:
 expected=R/'docs/engine-61-design-draft'/('legacy-early-close' if n=='DanmakuRender.cs' else 'legacy-ready')/'source'/n
 actual=O/'clean/source/src/Danmaku.Legacy'/n
 assert norm(expected.read_bytes())==norm(actual.read_bytes())
 assert 'ProbeGate' not in actual.read_text(encoding='utf-8-sig') and 'ProbeAttempt' not in actual.read_text(encoding='utf-8-sig')
packages={}
for n in ['Legacy','Share']:
 p=next((O/'clean/feed').glob(f'Danmaku.{n}.*.nupkg'))
 with zipfile.ZipFile(p) as z:b=z.read(next(x for x in z.namelist() if x.endswith(f'/Danmaku.{n}.dll')))
 assert b==(F/f'src/_build/x64/Release/BiliCopilot.UI/bin/Danmaku.{n}.dll').read_bytes()
 if (F/'layout').exists():assert b==(F/f'layout/Danmaku.{n}.dll').read_bytes()
 packages[n]={'package':p.name,'sha256':sha(p.read_bytes()),'dllSha256':sha(b)}
core=next((Path.home()/'.nuget/packages/danmaku.core/0.0.1-preview6/lib').rglob('Danmaku.Core.dll')).read_bytes()
assert core==(F/'src/_build/x64/Release/BiliCopilot.UI/bin/Danmaku.Core.dll').read_bytes()==(F/'layout/Danmaku.Core.dll').read_bytes()
result['officialCoreSha256']=sha(core)
same=[];diff=[];closure={}
with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git','archive','44ce6cc5','src']))) as t:
 for m in t.getmembers():
  if m.isfile() and m.name.endswith(('.cs','.xaml')):
   expected=norm(t.extractfile(m).read());actual=norm((F/m.name).read_bytes())
   (same if expected==actual else diff).append(m.name)
   closure[m.name]={'archiveNormalizedSha256':sha(expected),'actualNormalizedSha256':sha(actual)}
assert set(diff)=={'src/Desktop/BiliCopilot.UI/App.xaml.cs','src/Desktop/BiliCopilot.UI/GlobalDependencies.cs'}
(O/'source-closure.json').write_text(json.dumps(closure,indent=2))
result.update(packages=packages,unchangedProductionFiles=len(same),fixtureInjections=diff)
assert len(same)==901
assert 'Version="0.0.1-preview6"' in (F/'src/Directory.Packages.props').read_text()
for n,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 assert (O/('kernel-'+n)).read_bytes()==subprocess.check_output(['git','-C','externals/bili-kernel',*args])
result['kernelPreserved']=True
(O/'validation.json').write_text(json.dumps(result,indent=2));print('source/package validation PASS')
