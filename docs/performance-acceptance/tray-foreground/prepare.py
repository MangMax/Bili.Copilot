"""Fresh offline full-source host. No writes to the retained fixture or references."""
import pathlib, subprocess, shutil, tarfile, io, re, json, hashlib
root=pathlib.Path.cwd(); base=root/'artifacts/tray-foreground'; base.mkdir(parents=True,exist_ok=False)
old=root.parent/'t3-2831208f/artifacts/refresh-undo/fixture'
source=base/'source'; source.mkdir()
def archive(repo, rev, dest, paths=()):
    data=subprocess.check_output(['git','-C',str(repo),'archive',rev,*paths])
    with tarfile.open(fileobj=io.BytesIO(data)) as t:t.extractall(dest,filter='data')
archive(root,'HEAD',source,('src','global.json','nuget.config','.gitignore'))
kernel=source/'externals/bili-kernel';kernel.mkdir(parents=True)
archive(root.parent/'t3-2831208f/externals/bili-kernel','0ae5e4e000a41a07f6696746706df11c06e8835b',kernel)
fixture=base/'fixture'
shutil.copytree(old,fixture,ignore=shutil.ignore_patterns('bin','obj','frozen-output','frozen-*.json'))
ui=root/'src/Desktop/BiliCopilot.UI'
special={'App.xaml.cs','GlobalDependencies.cs','BiliCopilot.UI.csproj','Package.appxmanifest','Package.Dev.appxmanifest'}
for name in subprocess.check_output(['git','ls-files','src/Desktop/BiliCopilot.UI'],text=True).splitlines():
    p=root/name; rel=p.relative_to(ui)
    if str(rel) in special:continue
    (fixture/rel).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,fixture/rel)
p=fixture/'BiliCopilot.UI.csproj';s=p.read_text(encoding='utf-8-sig')
s=s.replace(str(root.parent/'t3-2831208f'),str(source));p.write_text(s,encoding='utf-8')
for name in ('Package.appxmanifest','Package.Dev.appxmanifest'):
    p=fixture/name;s=p.read_text(encoding='utf-8-sig');s=re.sub(r'(<Identity\s+Name=")[^"]+',r'\1Mang.BiliCopilot.TrayDiag.EE493',s);p.write_text(s,encoding='utf-8')
s=(ui/'App.xaml.cs').read_text(encoding='utf-8-sig').replace('"Richasy.BiliCopilot"','"TrayDiag.EE493"').replace('GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);','InitializeTrayLifecycle(); Offline48.Start();')
(fixture/'App.xaml.cs').write_text(s,encoding='utf-8')
p=fixture/'Fixture.cs';s=p.read_text(encoding='utf-8-sig').replace('public static void Start() => ControlledRefresh.Start();','public static void Start() => TrayProbe.Start();').replace('        if (ControlledRefresh.Handles(m.Name)) return ControlledRefresh.Call(m,a);','')
p.write_text(s,encoding='utf-8')
shutil.copyfile(pathlib.Path(__file__).with_name('TrayProbe.cs'),fixture/'TrayProbe.cs')
# Reuse only native/resource build assets, never app data or prior executable code.
assets=source/'src/Desktop/BiliCopilot.UI/Assets'
shutil.copytree(old/'Assets',assets,dirs_exist_ok=True)
print(base)
