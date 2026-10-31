"""Independent managed replay; never launches a desktop or changes app dependencies."""
import hashlib, json, pathlib, shutil, subprocess, zipfile
HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
OUT = REPO / 'artifacts/legacy-seek-reviewafdc'
EVIDENCE = HERE / 'evidence'
EVIDENCE.mkdir(exist_ok=True)
def run(cmd, name):
    p = subprocess.run(cmd, cwd=REPO, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
    (EVIDENCE / name).write_bytes(p.stdout)
    print(name, 'exit', p.returncode, flush=True)
    assert p.returncode == 0, p.stdout.decode(errors='replace')
def gitfile(name):
    return subprocess.check_output(['git', 'show', '597dff0d4d1a034906b89154e936aa1df8dadb1e:docs/engine-61-design-draft/c1a/' + name])
host = OUT / 'compat'
host.mkdir(exist_ok=True)
(host / 'Program.cs').write_bytes(gitfile('Program.cs'))
(host / 'Fixture.csproj').write_bytes(gitfile('Fixture.csproj'))
run(['dotnet', 'build', str(host / 'Fixture.csproj'), '-c', 'Release', '-p:Platform=x64', '-p:NuGetAudit=false'], 'compat-build.log')
binary = host / 'bin/x64/Release/net11.0-windows10.0.22621.0'
for mode in ['source', 'package']:
    target = EVIDENCE / mode
    target.mkdir(exist_ok=True)
    if mode == 'package':
        closure = json.loads((HERE.parent / 'legacy-paused-seek/closure.json').read_text())['final']
        assert hashlib.sha256(pathlib.Path(closure['packagePath']).read_bytes()).hexdigest() == closure['packageSha256']
        feed = pathlib.Path(closure['packagePath']).parent
        for name in ['Legacy', 'Share']:
            for ext in ['dll', 'pdb']:
                package_ext = 'nupkg' if ext == 'dll' else 'snupkg'
                with zipfile.ZipFile(feed / f'Danmaku.{name}.0.0.1-legacyseek1085f94e.2.{package_ext}') as z:
                    entry = next(n for n in z.namelist() if n.endswith(f'/Danmaku.{name}.{ext}'))
                    (binary / f'Danmaku.{name}.{ext}').write_bytes(z.read(entry))
        assert hashlib.sha256((binary / 'Danmaku.Legacy.dll').read_bytes()).hexdigest() == closure['dllSha256']
    run(['dotnet', str(binary / 'Fixture.dll'), str(target)], mode + '-run.log')
    assert (target / 'public-api.txt').read_bytes() == (HERE.parent / 'legacy-paused-seek/public-api-baseline.txt').read_bytes()
    results = json.loads((target / 'results.json').read_text())
    assert len(results) == 23 and all(x['passed'] for x in results)
print('Source and actual retained package: 23/23 each; API bytes equal; DLL/PDB document checks executed.')
