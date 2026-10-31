"""Relocate the existing offline full consumer into this checkout only."""
import pathlib, subprocess, tarfile, io, shutil, re
r=pathlib.Path.cwd(); o=r/'artifacts/speed-close/full'; o.mkdir(parents=True,exist_ok=False)
prior=r.parent/'t3-224a783a/artifacts/full-independent224'
doc=r.parent/'t3-224a783a/docs/engine-61-design-draft/core-full-independent-review'
def archive(repo,rev,path,dest):
    with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git','-C',str(repo),'archive',rev]+([path] if path else [])))) as t:t.extractall(dest,filter='data')
archive(r,'HEAD','src',o)
archive(r/'externals/bili-kernel','HEAD','',o/'externals/bili-kernel')
# Overlay working candidate; no generated or build files from the old consumer.
for p in (r/'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel').glob('*.cs'):
    shutil.copy2(p,o/p.relative_to(r))
shutil.copy2(r/'src/Desktop/BiliCopilot.UI/Controls/Core/OverlayTransportControls.xaml.cs',o/'src/Desktop/BiliCopilot.UI/Controls/Core/OverlayTransportControls.xaml.cs')
for n in ['global.json','.gitignore']:shutil.copy2(r/n,o/n)
shutil.copytree(prior/'feed',o/'feed')
(o/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="pin" value="{o / "feed"}"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources><config><add key="globalPackagesFolder" value="{o / "packages"}"/></config></configuration>')
p=o/'src/Directory.Packages.props';s=p.read_text(encoding='utf-8-sig')
for n in ['Core','Share']:s=s.replace(f'Include="Danmaku.{n}" Version="0.0.1-preview6"',f'Include="Danmaku.{n}" Version="0.0.1-pxdpi.9dc8c6c9.clean"')
p.write_text(s,encoding='utf-8')
app=o/'src/Desktop/BiliCopilot.UI'
for folder in ['libmpv','ffmpeg','bbdown']:
    shutil.copytree(prior/'src/Desktop/BiliCopilot.UI/Assets'/folder,app/'Assets'/folder,dirs_exist_ok=True)
p=app/'Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace('Mang.BiliCopilot','Bili.SpeedCloseDc85').replace('bili-copilot','bili-speeddc85'),encoding='utf-8')
p=app/'App.xaml.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        var instance =',s.index('protected override void OnLaunched'));b=s.index('\n    private static MainWindow?',a)
p.write_text(s[:a]+'        _ = Id;\n        OfflineAcceptance.Start(this);\n    }\n'+s[b:],encoding='utf-8')
p=app/'GlobalDependencies.cs';p.write_text(p.read_text(encoding='utf-8-sig').replace('.AddNotificationViewModel()','.AddNotificationViewModel()\n            .AddOfflineAcceptance()'),encoding='utf-8')
s=pathlib.Path(__file__).with_name('OfflineAcceptance.cs').read_text(encoding='utf-8-sig')
s=re.sub(r'internal const string Root = @"[^"]+";',lambda _:f'internal const string Root = @"{o}";',s)
(app/'OfflineAcceptance.cs').write_text(s,encoding='utf-8')
shutil.copy2(prior/'fixture-media.mkv',o/'fixture-media.mkv')
print(o)
