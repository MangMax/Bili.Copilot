from pathlib import Path
import subprocess, hashlib, json, shutil, zipfile, io, tarfile
R=Path.cwd(); O=R/'artifacts/combined-60cd'; D=Path(__file__).resolve().parent
O.mkdir(parents=True,exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
def git(*args): return subprocess.check_output(['git',*args],cwd=R)
def save(n,x): (O/n).write_text(json.dumps(x,indent=2),encoding='utf8')
def files(p):return {str(f.relative_to(p)).replace('\\','/'):sha(f.read_bytes()) for f in p.rglob('*') if f.is_file() and not any(v in ('bin','obj','.git') for v in f.relative_to(p).parts)}
if not (O/'kernel-backup').exists():
 b=O/'kernel-backup';b.mkdir()
 for n,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
  (b/n).write_bytes(git('-C',str(R/'externals/bili-kernel'),*args))
 save('initial.json',dict(head=git('rev-parse','HEAD').decode().strip(),gitlink=git('ls-tree','HEAD','externals/bili-kernel').decode(),kernel=files(b)))
C=R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/source'
L=R.parent/'t3-0b4f0501/artifacts/reconstruct-final/source'
c=files(C/'src/Danmaku.Share');l=files(L/'src/Danmaku.Share')
diff=[n for n in sorted(c.keys()|l.keys()) if c.get(n)!=l.get(n)]
save('share-comparison.json',dict(core=c,legacy=l,differences=diff))
print('Share differences',diff)
for n in diff:
 assert (L/'src/Danmaku.Share'/n).read_text(encoding='utf-8-sig')==(C/'src/Danmaku.Share'/n).read_text(encoding='utf-8-sig'),n
save('share-normalized.json',dict(equal=True,rawDifferences=diff,reason='BOM/newline only; normalized exact text equality, no API or behavior difference',selected='0.0.1-reconstruct0b4f.final'))
for label,p,expect in [('Core',R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/clean/feed','b15225ecec93b08943ecdbf889c74bebe8873e7851b43cc3c1feb98fc84f97fd'),('Legacy',R.parent/'t3-0b4f0501/artifacts/reconstruct-final/feed','2cc03ebaf8557cbf0abb96344276af717ba7a756541e0223a29a353313adfabf')]:
 zpath=next(p.glob('Danmaku.'+label+'.*.nupkg'))
 with zipfile.ZipFile(zpath) as z:
  dll=next(n for n in z.namelist() if n.endswith('Danmaku.'+label+'.dll'));assert sha(z.read(dll))==expect
  print(label,z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))).decode())
if (O/'full').exists(): raise SystemExit('audit done; existing full untouched')
F=O/'full';F.mkdir()
def archive(repo,rev,path,out):
 out.mkdir(parents=True,exist_ok=True)
 with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git','-C',str(repo),'archive',rev]+([path] if path else [])))) as t:t.extractall(out,filter='data')
archive(R,'44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede','src',F)
archive(R.parent/'t3-06c7e93e/externals/bili-kernel','0ae5e4e000a41a07f6696746706df11c06e8835b','',F/'externals/bili-kernel')
for n in ['global.json','.gitignore']:shutil.copyfile(R/n,F/n)
(F/'feed').mkdir()
for p,patterns in [(R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/clean/feed',['Danmaku.Core.*']), (R.parent/'t3-0b4f0501/artifacts/reconstruct-final/feed',['Danmaku.Legacy.*','Danmaku.Share.*'])]:
 for pat in patterns:
  for f in p.glob(pat):shutil.copyfile(f,F/'feed'/f.name)
(F/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{F / "feed"}"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
p=F/'src/Directory.Packages.props';s=p.read_text(encoding='utf-8-sig')
for n,v in [('Core','0.0.1-pxdpi.9dc8c6c9.clean'),('Legacy','0.0.1-reconstruct0b4f.final'),('Share','0.0.1-reconstruct0b4f.final')]:s=s.replace(f'Include="Danmaku.{n}" Version="0.0.1-preview6"',f'Include="Danmaku.{n}" Version="{v}"')
p.write_text(s,encoding='utf8')
A=F/'src/Desktop/BiliCopilot.UI';prior=R.parent/'t3-0b4f0501/artifacts/reconstruct-app/full'
for n in ['libmpv','ffmpeg','bbdown']:shutil.copytree(prior/'src/Desktop/BiliCopilot.UI/Assets'/n,A/'Assets'/n,dirs_exist_ok=True)
shutil.copyfile(prior/'fixture-media.mkv',F/'fixture-media.mkv');shutil.copyfile(prior/'libmpv-2.dll',F/'libmpv-2.dll')
p=A/'Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('Mang.BiliCopilot','Bili.Combined60cd').replace('bili-copilot','bili-combined60cd'),encoding='utf8')
p=A/'App.xaml.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        var instance =',s.index('protected override void OnLaunched'));b=s.index('\n    private static MainWindow?',a)
p.write_text(s[:a]+'        _ = Id;\n        OfflineAcceptance.Start(this);\n    }\n'+s[b:],encoding='utf8')
p=A/'GlobalDependencies.cs';p.write_text(p.read_text(encoding='utf-8-sig').replace('.AddNotificationViewModel()','.AddNotificationViewModel()\n            .AddOfflineAcceptance()'),encoding='utf8')
print('Prepared',F)
