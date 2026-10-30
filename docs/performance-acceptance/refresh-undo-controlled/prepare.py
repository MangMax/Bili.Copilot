"""Extend the retained RC3 full-source offline host; never mutate its evidence."""
import pathlib, shutil, subprocess, sys, json, hashlib
root = pathlib.Path.cwd()
old = pathlib.Path(sys.argv[1]).resolve()
dest = root / 'artifacts/refresh-undo/fixture'
assert not dest.exists()
shutil.copytree(old, dest, ignore=shutil.ignore_patterns('bin', 'obj', 'frozen-output', 'frozen-*.json'))
ui = root / 'src/Desktop/BiliCopilot.UI'
# Refresh all production bytes, preserving only the established offline composition.
for name in subprocess.check_output(['git','ls-files','src/Desktop/BiliCopilot.UI'], text=True).splitlines():
    p = root / name
    rel = p.relative_to(ui)
    if str(rel) in ('App.xaml.cs', 'GlobalDependencies.cs', 'BiliCopilot.UI.csproj', 'Package.appxmanifest', 'Package.Dev.appxmanifest'):
        continue
    (dest / rel).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(p, dest / rel)
p=dest/'BiliCopilot.UI.csproj'
s=p.read_text(encoding='utf-8-sig')
import re
s=re.sub(r'C:\\Users\\hp\\.t3\\worktrees\\Bili.Copilot\\t3-e497cf43\\artifacts\\timer-fix\\stop-only\\source', lambda _:str(root), s)
p.write_text(s,encoding='utf-8')
for name in ('Package.appxmanifest','Package.Dev.appxmanifest'):
    p=dest/name
    s=p.read_text(encoding='utf-8-sig')
    s=re.sub(r'(<Identity\s+Name=")[^"]+', r'\1Mang.BiliCopilot.UndoControlled.T283',s)
    p.write_text(s,encoding='utf-8')
# Reuse external-service/settings overrides, but route discovery through our barriers.
p=dest/'Fixture.cs'; s=p.read_text(encoding='utf-8-sig')
s=s.replace('static readonly Dictionary<string,object?> Settings', 'internal static readonly Dictionary<string,object?> Settings')
s=s.replace('        Log("boundary", new {method=m.Name});', '        if (ControlledRefresh.Handles(m.Name)) return ControlledRefresh.Call(m,a);\n        Log("boundary", new {method=m.Name});')
start=s.index('    public static void Start()'); end=s.index('    static void Check(',start)
s=s[:start]+'    public static void Start() => ControlledRefresh.Start();\n'+s[end:]
p.write_text(s,encoding='utf-8')
shutil.copyfile(pathlib.Path(__file__).with_name('ControlledRefresh.cs'),dest/'ControlledRefresh.cs')
shutil.copyfile(root/'global.json',dest/'global.json')
shutil.copyfile(root/'src/Directory.Packages.props',dest/'Directory.Packages.props')
shutil.copyfile(root/'nuget.config',dest/'nuget.config')
(dest/'source-provenance.txt').write_text(subprocess.check_output(['git','rev-parse','HEAD'],text=True))
print(dest)
