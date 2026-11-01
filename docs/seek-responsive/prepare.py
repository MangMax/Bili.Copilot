from pathlib import Path
import subprocess, shutil, json, hashlib
R=Path.cwd(); O=R/'artifacts/seek-responsive'; F=O/'full'
assert not F.exists()
S=R.parent/'t3-39df52c6/artifacts/combined-60cd/full'
shutil.copytree(S,F,ignore=shutil.ignore_patterns('_build','bin','obj','.git','layout*','runtime*','events.jsonl'))
for name in subprocess.check_output(['git','ls-files','src','tests','global.json','.gitignore']).decode().splitlines():
 p=F/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/name,p)
(F/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{F / "feed"}"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
A=F/'src/Desktop/BiliCopilot.UI'
p=A/'Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('Mang.BiliCopilot','Bili.Seek870b').replace('bili-copilot','bili-seek870b'),encoding='utf8')
p=A/'App.xaml.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        var instance =',s.index('protected override void OnLaunched'));b=s.index('\n    private static MainWindow?',a)
p.write_text(s[:a]+'        _ = Id;\n        OfflineAcceptance.Start(this);\n    }\n'+s[b:],encoding='utf8')
p=A/'GlobalDependencies.cs';p.write_text(p.read_text(encoding='utf-8-sig').replace('.AddNotificationViewModel()','.AddNotificationViewModel()\n            .AddOfflineAcceptance()'),encoding='utf8')
s=(Path(__file__).resolve().parent/'OfflineAcceptance.cs').read_text(encoding='utf8');old=s.split('internal const string Root = @"')[1].split('";')[0]
(A/'OfflineAcceptance.cs').write_text(s.replace(old,str(F)),encoding='utf8')
for name,args in [('head',['rev-parse','HEAD']),('status',['status','--porcelain=v1']),('staged.patch',['diff','--cached','--binary']),('worktree.patch',['diff','--binary'])]:
 (O/('kernel-'+name)).write_bytes(subprocess.check_output(['git','-C',str(R/'externals/bili-kernel'),*args]))
# The candidate feed is opt-in for this disposable host only.
for p in (Path(__file__).resolve().parent/'candidate-feed').glob('*'):
 shutil.copyfile(p,F/'feed'/p.name)
p=F/'src/Directory.Packages.props';s=p.read_text(encoding='utf-8-sig')
import re
s=re.sub(r'(Include="Danmaku\.(?:Core|Legacy|Share)" Version=")[^"]+',r'\g<1>0.0.1-seek870b.20261101.5',s)
p.write_text(s,encoding='utf8')
print(F)

p=A/'ViewModels/Core/PlayerViewModel/PlayerViewModel.Playback.cs'
import zipfile
with zipfile.ZipFile(Path(__file__).resolve().parent/'evidence.zip') as z:
 s=z.read('host-injection/PlayerViewModel.Playback.cs').decode('utf8')
p.write_text(s,encoding='utf8')
