"""Archive only this task's safe evidence, not layouts, account trees or application data."""
import pathlib,json,gzip,hashlib,subprocess,shutil,sys
root=pathlib.Path.cwd();base=root/'artifacts/tray-foreground';dest=pathlib.Path(__file__).parent/'evidence'
physical='--physical' in sys.argv
dest.mkdir(exist_ok=physical)
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(p,rel):
    q=dest/rel;q.parent.mkdir(parents=True,exist_ok=True)
    actual=q if p.suffix=='.png' else q.with_name(q.name+'.gz')
    assert not actual.exists(),actual
    if p.suffix=='.png':shutil.copyfile(p,q)
    else:q.with_name(q.name+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))
if physical:
    for run in ('physical-red','physical-green'):
        for p in (base/run).iterdir():
            if p.is_file():save(p,pathlib.Path(run)/p.name)
    for name in ('final-state-after-physical.json','yield-after-physical.json','toolchain.json'):
        save(base/name,pathlib.Path(name))
    sys.exit(0)
for run in ('run1','logged1','run2','green1'):
    for p in (base/run).iterdir():
        if p.is_file():save(p,pathlib.Path(run)/p.name)
for name in ('build1.log','tests-targeted.log','tests-suite.log','production-build.log','observer.stderr','observer2.stderr','green-observer.stderr','logged-observer.stderr','final-state.json','yield.json'):
    save(base/name,pathlib.Path(name))
save(base/'green/build.log',pathlib.Path('green-build.log'))
for p in (base/'tests').glob('*.trx'):save(p,pathlib.Path('tests')/p.name)
for arm,path in [('red',base/'fixture'),('green',base/'green/fixture')]:
    for name in ('frozen-all-files.json','frozen-content-provenance.json'):save(path/name,pathlib.Path(arm)/name)
    save(path.parent/'fixture-input-files.json',pathlib.Path(arm)/'fixture-input-files.json')
    assets=json.loads((path/'obj/project.assets.json').read_text(encoding='utf-8-sig'))
    closure={k:v.get('sha512') for k,v in assets['libraries'].items() if v['type']=='package'}
    (dest/arm/'packages.json').write_text(json.dumps(closure,indent=2))
    save(path/'App.xaml.cs',pathlib.Path(arm)/'App.xaml.cs')
# Public upstream source freeze; immutable references are in the report.
vendor=base/'vendor'
sources=[vendor/'DesktopWindowImpl.actual.cpp',vendor/'WinUIEx.WindowExtensions.cs']
n=next((vendor/'notify').iterdir())
sources += [n/'src/libs/H.NotifyIcon.Shared'/name for name in ('TaskbarIcon.MouseEvents.cs','TaskbarIcon.ContextMenu.WinRT.PopupMenu.cs')]
sources += [n/'src/libs/H.NotifyIcon/PopupMenus/PopupMenu.cs',n/'src/libs/H.NotifyIcon/Core/MessageWindow.cs']
(dest/'vendor-hashes.json').write_text(json.dumps({str(p.relative_to(vendor)):sha(p) for p in sources},indent=2))
production=base/'production'
sourceRows=[]
for name in subprocess.check_output(['git','ls-files','src','tests','global.json'],text=True).splitlines():
    p=root/name;q=production/name
    if q.exists():
        assert sha(p)==sha(q),name
        sourceRows.append(dict(path=name,sha256=sha(p)))
(dest/'production-source.json.gz').write_bytes(gzip.compress(json.dumps(sourceRows,indent=2).encode(),mtime=0))
build=base/'production-build'
files=[dict(path=str(p.relative_to(build)),size=p.stat().st_size,sha256=sha(p)) for p in build.rglob('*') if p.is_file()]
(dest/'production-build-files.json.gz').write_bytes(gzip.compress(json.dumps(files,indent=2).encode(),mtime=0))
packageHashes={}
for name in ('h.notifyicon','h.notifyicon.winui','winuiex','microsoft.windowsappsdk','microsoft.windowsappsdk.winui'):
    version={'h.notifyicon':'2.4.1','h.notifyicon.winui':'2.4.1','winuiex':'2.9.3'}.get(name,'2.6.4')
    p=pathlib.Path.home()/'.nuget/packages'/name/version/(name+'.'+version+'.nupkg')
    packageHashes[name+'/'+version]=sha(p)
(dest/'package-file-hashes.json').write_text(json.dumps(packageHashes,indent=2))
screens=[dict(path=str(p.relative_to(dest)),sha256=sha(p),viewed=True) for p in dest.rglob('*.png')]
(dest/'screenshots-reviewed.json').write_text(json.dumps(screens,indent=2))
(dest/'run1-exit-incomplete.json').write_text(json.dumps(dict(status='BLOCKED',reason='Package removed before observer copied LocalState; no persisted retained-handle exit code or final queries. Do not infer PASS from process disappearance.'),indent=2))
print(json.dumps(dict(files=sum(p.is_file() for p in dest.rglob('*')),productionSourceFiles=len(sourceRows),productionBuildFiles=len(files),screenshots=len(screens))))
