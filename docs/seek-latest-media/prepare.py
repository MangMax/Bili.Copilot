"""Recreate the isolated host from the previously verified, read-only host source.

Requires the source preserved by combined-desktop-39df (its report contains the
offline package/source recovery scripts). Never touches a registered layout.
Run from the repository root; refuses to overwrite an existing host.
"""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import zipfile

r = Path.cwd()
d = Path(__file__).resolve().parent
s = r.parent / 't3-39df52c6/artifacts/combined-60cd/full'
f = r / 'artifacts/latest-seek/full'
assert s.is_dir(), 'Recover the frozen 39df host source first'
assert not f.exists(), 'Use a fresh owned destination; never overwrite a layout'
shutil.copytree(s, f, ignore=shutil.ignore_patterns('_build', 'bin', 'obj', '.git', 'layout*', 'runtime*', 'events.jsonl'))
files = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', 'src', 'tests', 'global.json']).decode().splitlines()
for n in files:
    if (r / n).is_file():
        (f / n).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(r / n, f / n)
for n in ['NuGet.Config', 'src/Directory.Packages.props', 'src/Desktop/BiliCopilot.UI/Package.appxmanifest']:
    shutil.copyfile(s / n, f / n)
for n in ['NuGet.Config', 'src/Desktop/BiliCopilot.UI/Package.appxmanifest']:
    p = f / n
    p.write_text(p.read_text(encoding='utf-8-sig').replace(str(s), str(f)).replace('Bili.Combined39df', 'Bili.LatestA1de').replace('bili-combined39df', 'bili-latesta1de'), encoding='utf8')
p = f / 'src/Desktop/BiliCopilot.UI/App.xaml.cs'
text = p.read_text(encoding='utf-8-sig')
start = text.index('        var instance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(Id);')
end = text.index('\n    private static MainWindow?', start)
text = text[:start] + '        _ = Id;\n        OfflineAcceptance.Start(this);\n    }\n' + text[end:]
p.write_text(text, encoding='utf8')
p = f / 'src/Desktop/BiliCopilot.UI/GlobalDependencies.cs'
p.write_text(p.read_text(encoding='utf-8-sig').replace('.AddNotificationViewModel()', '.AddNotificationViewModel()\n            .AddOfflineAcceptance()'), encoding='utf8')
p = f / 'src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs'
text = (d / p.name).read_text(encoding='utf8')
old = text.split('internal const string Root = @"')[1].split('";')[0]
p.write_text(text.replace(old, str(f)), encoding='utf8')
ledger = json.loads((d / 'package-closure.json').read_text(encoding='utf8'))
for item in ledger['packages']:
    p = f / 'feed' / Path(item['package']).name
    assert hashlib.sha256(p.read_bytes()).hexdigest() == item['packageSha']
command = json.loads((d / 'build3-command.json').read_text(encoding='utf8'))['command']
command = [x.replace(old, str(f)) for x in command]
subprocess.run(command, check=True)
for item in ledger['packages']:
    p = f / 'feed' / Path(item['package']).with_suffix('.snupkg').name
    with zipfile.ZipFile(p) as archive:
        name = next(n for n in archive.namelist() if n.endswith(f"/Danmaku.{item['name']}.pdb"))
        data = archive.read(name)
        assert hashlib.sha256(data).hexdigest() == item['pdbSha']
        (f / f"src/_build/x64/Release/BiliCopilot.UI/bin/Danmaku.{item['name']}.pdb").write_bytes(data)
for name in ['launch-retain.ps1', 'cleanup.ps1', 'run-ui.py', 'supplement.py', 'assertions.py']:
    shutil.copyfile(d / name, f.parent / name)
print('Built owned host. Launch and UI scripts remain separate explicit steps.')
