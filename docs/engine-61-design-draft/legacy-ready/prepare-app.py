"""Archive current production tree, fixed kernel, and inject offline fixture only."""
from pathlib import Path
import subprocess,tarfile,io,shutil,re,json,zipfile
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/legacy-ready';F=O/'full'
def archive(repo,rev,path,out):
 out.mkdir(parents=True,exist_ok=True)
 with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git','-C',str(repo),'archive',rev]+([path] if path else [])))) as t:t.extractall(out,filter='data')
assert not F.exists()
archive(R,'44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede','src',F)
archive(R/'externals/bili-kernel','0ae5e4e000a41a07f6696746706df11c06e8835b','',F/'externals/bili-kernel')
for n in ['global.json','.gitignore']:shutil.copyfile(R/n,F/n)
shutil.copytree(O/'clean/feed',F/'feed')
(F/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{F / "feed"}"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
p=F/'src/Directory.Packages.props';s=p.read_text(encoding='utf-8-sig')
for n in ['Legacy','Share']:s=s.replace(f'Include="Danmaku.{n}" Version="0.0.1-preview6"',f'Include="Danmaku.{n}" Version="0.0.1-preview6.ready06c7.1"')
p.write_text(s,encoding='utf-8')
A=F/'src/Desktop/BiliCopilot.UI';prior=R.parent/'t3-575992d4/artifacts/overlay-review/full'
for n in ['libmpv','ffmpeg','bbdown']:shutil.copytree(prior/'src/Desktop/BiliCopilot.UI/Assets'/n,A/'Assets'/n,dirs_exist_ok=True)
shutil.copyfile(prior/'fixture-media.mkv',F/'fixture-media.mkv')
shutil.copyfile(next((A/'Assets/libmpv').rglob('*.dll')),F/'libmpv-2.dll')
p=A/'Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('Mang.BiliCopilot','Bili.Ready06c7').replace('bili-copilot','bili-ready06c7'),encoding='utf-8')
p=A/'App.xaml.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        var instance =',s.index('protected override void OnLaunched'));b=s.index('\n    private static MainWindow?',a)
p.write_text(s[:a]+'        _ = Id;\n        OfflineAcceptance.Start(this);\n    }\n'+s[b:],encoding='utf-8')
p=A/'GlobalDependencies.cs';p.write_text(p.read_text(encoding='utf-8-sig').replace('.AddNotificationViewModel()','.AddNotificationViewModel()\n            .AddOfflineAcceptance()'),encoding='utf-8')
s=(D/'OfflineAcceptance.cs').read_text(encoding='utf-8-sig');s=re.sub(r'internal const string Root = @"[^"]+";',lambda _:f'internal const string Root = @"{F}";',s)
(A/'OfflineAcceptance.cs').write_text(s,encoding='utf-8')
cmd=['dotnet','build',str(A/'BiliCopilot.UI.csproj'),'-c','Release','-p:Platform=x64','-p:PublishAot=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false','-p:NuGetAudit=false','-p:SolutionDir='+str(F/'src')+'/', '--configfile',str(F/'NuGet.Config')]
with (O/'full-build.log').open('w',encoding='utf-8') as log:p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT)
print('full build',p.returncode);p.check_returncode()
