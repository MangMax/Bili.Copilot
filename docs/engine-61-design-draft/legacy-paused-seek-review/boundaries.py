"""Run controlled actual-assembly interleavings; no native objects are constructed."""
import json, pathlib, shutil, subprocess, zipfile
HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
ROOT = REPO / 'artifacts/legacy-seek-reviewafdc'
EVIDENCE = HERE / 'evidence'
host = ROOT / 'boundary'
host.mkdir(exist_ok=True)
shutil.copyfile(HERE / 'Boundary.cs', host / 'Program.cs')
shutil.copyfile(ROOT / 'compat/Fixture.csproj', host / 'Boundary.csproj')
def run(cmd, name):
    p = subprocess.run(cmd, cwd=REPO, capture_output=True, timeout=60)
    (EVIDENCE / name).write_bytes(p.stdout + p.stderr)
    print(name, 'exit', p.returncode, flush=True)
    return p.returncode
assert run(['dotnet','build',str(host / 'Boundary.csproj'),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false'], 'boundary-build.log') == 0
binary = host / 'bin/x64/Release/net11.0-windows10.0.22621.0'
results = []
for mode in ['candidate','baseline']:
    if mode == 'baseline':
        closure = json.loads((HERE.parent / 'legacy-paused-seek/closure.json').read_text())['source-red']
        with zipfile.ZipFile(closure['packagePath']) as z:
            (binary / 'Danmaku.Legacy.dll').write_bytes(z.read(closure['entry']))
    for case in (['queue','close','stop','reentry'] if mode == 'candidate' else ['stop','reentry']):
        expected = 2 if mode == 'candidate' and case == 'reentry' else 0
        code = run(['dotnet', str(binary / 'Boundary.dll'), case], f'{mode}-{case}.log')
        results.append(dict(mode=mode,case=case,exit=code,expected=expected))
        assert code == expected
(EVIDENCE / 'boundaries.json').write_text(json.dumps(results,indent=2))
print('Six controlled observations matched; candidate reentry is a reproduced deadlock, not a passing safety test.')
