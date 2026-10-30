import pathlib,shutil,subprocess,tarfile,io,json,hashlib
root=pathlib.Path.cwd();base=root/'artifacts/tray-foreground';old=base/'fixture';green=base/'green/fixture'
assert not green.exists()
shutil.copytree(old,green,ignore=shutil.ignore_patterns('bin','obj','frozen-output','frozen-*.json'))
s=(root/'src/Desktop/BiliCopilot.UI/App.xaml.cs').read_text(encoding='utf-8-sig').replace('"Richasy.BiliCopilot"','"TrayDiag.EE493"').replace('GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);','InitializeTrayLifecycle(); Offline48.Start();')
(green/'App.xaml.cs').write_text(s,encoding='utf-8')
# Independent production tree for fresh build/tests; red archive remains unchanged.
prod=base/'production';assert not prod.exists()
shutil.copytree(base/'source',prod,ignore=shutil.ignore_patterns('bin','obj','_build'))
data=subprocess.check_output(['git','archive','HEAD','tests'])
with tarfile.open(fileobj=io.BytesIO(data)) as t:t.extractall(prod,filter='data')
shutil.copyfile(root/'src/Desktop/BiliCopilot.UI/App.xaml.cs',prod/'src/Desktop/BiliCopilot.UI/App.xaml.cs')
print(green)
