"""Local RX2 receipt fix; preserves original source and review evidence."""
import argparse, hashlib, json, pathlib, shutil, subprocess

p = argparse.ArgumentParser()
p.add_argument('--name', required=True)
a = p.parse_args()
root = pathlib.Path(__file__).resolve().parents[3]
review = root.parent/'t3-90bdd5f4'
delivery = root.parent/'t3-f7aca006'
out = root/'artifacts'/a.name
out.mkdir()
base = 'abfe2641813905eeb5e0374cb4f983f4ce2ff6af'
manifest = json.loads((root/'docs/engine-61-design-draft/rx2-r1/source.json').read_text())
bundle = pathlib.Path(manifest['artifacts']['candidate.bundle']['path'])
assert hashlib.sha256(bundle.read_bytes()).hexdigest() == manifest['artifacts']['candidate.bundle']['sha256']
source = out/'source'
subprocess.run(['git', '-c', 'core.autocrlf=false', 'clone', str(bundle), str(source)], check=True)
subprocess.run(['git', '-C', str(source), 'checkout', '--detach', base], check=True)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:
    (out/n).write_text('<Project />')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')

cases = (root/'docs/engine-61-design-draft/rx2-r1/ReviewCases.cs').read_text()
boundary = (review/'docs/engine-61-design-draft/rx2-r1-review/BoundaryCases.cs').read_text()
(out/'ReviewCases.cs').write_text(cases)
(out/'BoundaryCases.cs').write_text(boundary)
fixture = review/'artifacts/rx2-r1-independent-v3/artifacts/rx2-input/fixed/docs/engine-61-design-draft/c1a/Fixture.csproj'
program = (delivery/'docs/engine-61-design-draft/rx2/Program.cs').read_text().replace('ExecutionCases(Case);', 'ExecutionCases(Case); ReviewCases(Case); BoundaryCases(Case);')
dotnet = str(root.parent/'t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe')

def run(label, expected):
    stage = out/label
    stage.mkdir()
    shutil.copytree(source, stage/'source', ignore=shutil.ignore_patterns('.git','bin','obj'))
    (stage/'source/src/Danmaku.Core/Rx2Friend.cs').write_text('[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Rx2Probe")]')
    host = stage/'host'
    host.mkdir()
    (host/'Fixture.csproj').write_text(fixture.read_text().replace('<OutputType>', '<AssemblyName>Rx2Probe</AssemblyName><OutputType>'))
    (host/'Program.cs').write_text(program)
    (host/'ReviewCases.cs').write_text(cases)
    (host/'BoundaryCases.cs').write_text(boundary)
    shutil.copyfile(delivery/'docs/engine-61-design-draft/rx2/ExecutionProbe.cs', host/'ExecutionProbe.cs')
    commands = [([dotnet,'build',str(host/'Fixture.csproj'),'-c','Release','-m:1','-p:Platform=x64','-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false'],'build.txt'),
                ([dotnet,str(host/'bin/x64/Release/net11.0-windows10.0.22621.0/Rx2Probe.dll'),str(stage/'results.json')],'run.txt')]
    for command, log in commands:
        with (stage/log).open('w') as f:
            result = subprocess.run(command, stdout=f, stderr=subprocess.STDOUT, timeout=180)
        if log == 'build.txt': assert result.returncode == 0, stage/log
    rows = json.loads((stage/'results.json').read_text())
    assert len(rows) == len({x['name'] for x in rows}) == 36
    failures = [x['name'] for x in rows if not x['passed']]
    assert failures == expected, failures
    assert result.returncode == (1 if expected else 0)
    print(label, len(rows)-len(failures), failures, flush=True)

expected = ['R1_StopCallbackResumes_ReceiptMustBeSuperseded','R1_SetCallbackReplacesScene_ReceiptMustBeSuperseded','R1_ClearCallbackReplacesScene_ReceiptMustBeSuperseded']
run('red', expected)
from fix import apply
apply(source)
run('green', [])
subprocess.run(['git','-C',str(source),'diff','--check'],check=True)
with (out/'candidate.patch').open('wb') as f:
    subprocess.run(['git','-C',str(source),'diff','--binary',base],stdout=f,check=True)
print('Candidate source and red/green evidence:',out,flush=True)
