"""Fresh isolated restore and serial JIT verification; no deployment/UI activation."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
SDK = Path('C:/Program Files/dotnet')
os.environ['DOTNET_ROOT'] = str(SDK)
os.environ['PATH'] = str(SDK) + os.pathsep + os.environ['PATH']
stage = sys.argv[1]
dest = ROOT / 'artifacts/seek-responsive-integration' / stage
dest.mkdir(parents=True, exist_ok=False)
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True, encoding='utf-8').strip()
def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
app = ROOT / 'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj'
common = [f'-p:RestorePackagesPath={ROOT.as_posix()}/artifacts/seek-responsive-integration/packages', '-p:Platform=x64', '-p:PublishAot=false', f'-p:SolutionDir={ROOT.as_posix()}/src/']
if stage == 'Restore':
    cmd = ['restore', str(app), '--packages', str(ROOT/'artifacts/seek-responsive-integration/packages'), '--configfile', str(ROOT/'nuget.config'), '-p:NuGetAudit=false', *common]
elif stage == 'Tests':
    cmd = ['test', str(ROOT/'tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj'), '--no-restore', '-c', 'Release', '--logger', 'trx;LogFileName=all.trx', '--results-directory', str(dest/'trx')]
elif stage in ('Debug', 'Release'):
    cmd = ['build', str(app), '--no-restore', '-t:Rebuild', '-c', stage, *common, '-p:AppxPackageSigningEnabled=false', '-p:GenerateAppxPackageOnBuild=false', f'-p:BuildOutDir={(dest/"build-output").as_posix()}/']
elif stage in ('RuntimeGate', 'DebugRuntimeGate'):
    config = 'Debug' if stage == 'DebugRuntimeGate' else 'Release'
    cmd = ['msbuild', str(app), '-t:ValidateRuntimeDependencies', f'-p:Configuration={config}', *common]
else:
    raise ValueError(stage)
cmd.insert(0, str(SDK/'dotnet.exe'))
if stage in ('Debug', 'Release'):
    cmd.append(f'-bl:{dest / "build.binlog"}')
record = dict(head=git('rev-parse', 'HEAD'), sourceTree=git('rev-parse', 'HEAD:src'), kernel=git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD'), command=cmd, started=datetime.datetime.now(datetime.timezone.utc).isoformat())
record['sourceHashes'] = {p: sha(ROOT/p) for p in git('ls-files', 'src', 'tests').splitlines() if (ROOT/p).is_file()}
(dest/'provenance.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
with (dest/'command.log').open('wb') as log:
    result = subprocess.run(cmd, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
record['exitCode'] = result.returncode
record['completed'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
if stage in ('RuntimeGate', 'DebugRuntimeGate'):
    record['runtimeAssets'] = {str(p.relative_to(ROOT)): {'bytes': p.stat().st_size, 'sha256': sha(p)} for p in (app.parent/'Assets').glob('*/x64/*') if p.is_file()}
if stage == 'Tests' and result.returncode == 0:
    tree = ET.parse(dest/'trx/all.trx')
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    record['counters'] = tree.find('.//t:Counters', ns).attrib
    rows = tree.findall('.//t:UnitTestResult', ns)
    record['results'] = {r.attrib['testId']: r.attrib['outcome'] for r in rows}
    assert len(rows) == 577 and all(r.attrib['outcome'] == 'Passed' for r in rows)
record['outputs'] = {str(p.relative_to(ROOT)): sha(p) for p in (dest/'build-output').rglob('*') if p.is_file() and p.suffix in ('.dll', '.pdb', '.exe', '.pri', '.json', '.appxrecipe')}
if result.returncode == 0 and stage not in ('RuntimeGate', 'DebugRuntimeGate', 'Restore'):
    output = ROOT/'tests/BiliCopilot.UI.Tests/bin/Release/net11.0' if stage == 'Tests' else dest/'build-output/BiliCopilot.UI/bin'
    name = 'BiliCopilot.UI.Tests' if stage == 'Tests' else 'BiliCopilot.UI'
    helper = ROOT/'artifacts/searchbox-properties-integration/Metadata/bin/Release/net11.0/Metadata.dll'
    record['pdbHelperSha256'] = sha(helper)
    record['binaryHashes'] = {str(output/(name+ext)): sha(output/(name+ext)) for ext in ('.dll', '.pdb')}
    with (dest/'pdb.log').open('wb') as log:
        check = subprocess.run([str(SDK/'dotnet.exe'), str(helper), str(output/(name+'.dll')), str(output/(name+'.pdb')), str(dest/'pdb.json')], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    record['pdbExit'] = check.returncode
    assert check.returncode == 0, 'PDB source/identity check failed'
(dest/'provenance.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps({k:v for k,v in record.items() if k not in ('sourceHashes', 'outputs', 'results')}, indent=2))
sys.exit(result.returncode)
