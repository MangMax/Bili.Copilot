"""Fresh complete-source arms; no production edits or prior app binaries."""
import hashlib, io, json, os, pathlib, shutil, subprocess, tarfile, sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
BASE = ROOT / 'artifacts/runtime-final-baseline'
HERE = pathlib.Path(__file__).resolve().parent
KERNEL = '0ae5e4e000a41a07f6696746706df11c06e8835b'

def run(args, cwd=ROOT, log=None):
    if log:
        with log.open('wb') as f:
            subprocess.run(args, cwd=cwd, stdout=f, stderr=subprocess.STDOUT, check=True)
    else:
        subprocess.run(args, cwd=cwd, check=True)

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def ledger(folder):
    return [{'path': p.relative_to(folder).as_posix(), 'bytes': p.stat().st_size,
             'sha256': digest(p)} for p in sorted(folder.rglob('*')) if p.is_file()]

def save(p, value):
    p.write_text(json.dumps(value, indent=2), encoding='utf-8')

def archive(repo, revision, dest, paths=()):
    dest.mkdir(parents=True, exist_ok=True)
    data = subprocess.check_output(['git', '-C', str(repo), 'archive', revision, *paths])
    with tarfile.open(fileobj=io.BytesIO(data)) as t:
        t.extractall(dest, filter='data')

if __name__ == '__main__':
    resume = sys.argv[1:] == ['--resume-after-net10-freeze']
    BASE.mkdir(parents=True, exist_ok=resume)
    initial = BASE / 'initial'
    kernel = ROOT / 'externals/bili-kernel'
    if not resume:
        initial.mkdir()
        (initial / 'kernel-index.patch').write_bytes(subprocess.check_output(['git','-C',str(kernel),'diff','--cached','--binary']))
        (initial / 'kernel-working.patch').write_bytes(subprocess.check_output(['git','-C',str(kernel),'diff','--binary']))
        save(initial/'kernel.json', {'head': subprocess.check_output(['git','-C',str(kernel),'rev-parse','HEAD'],text=True).strip()})
        bundle = ROOT / 'docs/performance-acceptance/dependency-upgrade/kernel.bundle'
        run(['git','-C',str(kernel),'bundle','verify',str(bundle)], log=initial/'bundle-verify.log')
        run(['git','-C',str(kernel),'fetch',str(bundle),KERNEL], log=initial/'fetch.log')
        run(['git','-C',str(kernel),'restore','--source='+KERNEL,'--staged','--worktree','.'])
        run(['git','-C',str(kernel),'checkout','--detach',KERNEL], log=initial/'checkout.log')
    for arm, sdk in [('net10','10.0.403'),('net11','11.0.100-rc.3.26522.148')]:
        if resume and arm=='net10':
            area=BASE/arm
            assert ledger(area/'fixture/frozen-output')==json.loads((area/'frozen-files.json').read_text(encoding='utf-8'))
            save(area/'package-graph.json',json.loads((area/'fixture/obj/project.assets.json').read_text(encoding='utf-8')))
            continue
        area = BASE / arm; area.mkdir()
        source = area / 'source'
        archive(ROOT,'76af64717dc7bd0e95e1d34c8622d5e8cad48805',source,('src','global.json','nuget.config','.gitignore'))
        archive(kernel,KERNEL,source/'externals/bili-kernel')
        save(source/'global.json', {'sdk': {'version':sdk,'rollForward':'disable','allowPrerelease':arm=='net11'}})
        if arm=='net10':
            for p in source.rglob('*.csproj'):
                s=p.read_text(encoding='utf-8-sig')
                if 'net11.0' in s: p.write_text(s.replace('net11.0','net10.0'),encoding='utf-8')
        fixture = area / 'fixture'
        run(['pwsh','-NoProfile','-File',str(ROOT/'docs/performance-acceptance/dependency-upgrade/prepare.ps1'),'-Destination',str(fixture),'-RunId','Final4F1'],log=area/'prepare.log')
        run(['pwsh','-NoProfile','-File',str(ROOT/'docs/performance-acceptance/net10-adaptation/prepare-observer.ps1'),'-Fixture',str(fixture)],log=area/'observer-prepare.log')
        p=fixture/'BiliCopilot.UI.csproj'
        s=p.read_text(encoding='utf-8-sig').replace(str(ROOT),str(source))
        if arm=='net10': s=s.replace('net11.0','net10.0')
        p.write_text(s,encoding='utf-8')
        shutil.copy2(source/'global.json',fixture/'global.json')
        # Both arms use identical main-window geometry; settle before markers.
        p=fixture/'IdleBaseline.cs';s=p.read_text(encoding='utf-8-sig')
        s=s.replace('        var app = (App)Application.Current;', '        main.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(120, 90, 1200, 800));\n        var app = (App)Application.Current;')
        s=s.replace('visible=main.AppWindow.IsVisible, hwnd=hwnd.ToInt64(), scale=root.XamlRoot.RasterizationScale', 'visible=main.AppWindow.IsVisible, hwnd=hwnd.ToInt64(), scale=root.XamlRoot.RasterizationScale, x=main.AppWindow.Position.X, y=main.AppWindow.Position.Y, width=main.AppWindow.Size.Width, height=main.AppWindow.Size.Height')
        p.write_text(s,encoding='utf-8')
        # Window-only capture: never --capture-screen (desktop).
        p=fixture/'run-ui.ps1';s=p.read_text(encoding='utf-8-sig').replace("'--capture-screen',",'');p.write_text(s,encoding='utf-8')
        save(area/'source-files.json',ledger(source))
        save(area/'fixture-inputs.json',ledger(fixture))
        command=['C:/Program Files/dotnet/dotnet.exe','build',str(fixture/'BiliCopilot.UI.csproj'),'-c','Release','-p:Platform=x64','-p:PublishAot=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false',f'-p:SolutionDir={source.as_posix()}/src/',f'-p:BuildOutDir={area.as_posix()}/references/']
        save(area/'command.json',command)
        run(['C:/Program Files/dotnet/dotnet.exe','--info'],cwd=fixture,log=area/'sdk.log')
        run(command,cwd=fixture,log=area/'build.log')
        outputs=list((fixture/'bin').rglob('AppxManifest.xml'))
        assert len(outputs)==1,outputs
        run(['pwsh','-NoProfile','-File',str(ROOT/'docs/performance-acceptance/net10-adaptation/freeze.ps1'),'-Fixture',str(fixture),'-BuildOutput',str(outputs[0].parent)],log=area/'freeze.log')
        save(area/'frozen-files.json',ledger(fixture/'frozen-output'))
        assets=json.loads((fixture/'obj/project.assets.json').read_text(encoding='utf-8'))
        save(area/'package-graph.json',assets)
        print(arm,'frozen',len(assets['libraries']),flush=True)
