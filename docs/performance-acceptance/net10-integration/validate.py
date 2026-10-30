"""Serial build and immediate byte-preserving freeze; never deploys or launches UI."""
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import datetime
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[3]
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()
MERGE = git('rev-parse', 'HEAD')
BASE = ROOT / 'artifacts/net10-integration' / MERGE
STAGE = sys.argv[1]
DEST = BASE / STAGE
DEST.mkdir(parents=True, exist_ok=False)
def save(name, value):
    (DEST / name).write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf-8')
def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
def copy(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
def run(args, name):
    with (DEST / name).open('wb') as log:
        result = subprocess.run(args, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(f'{name}: exit {result.returncode}')
    return result.returncode

app = str(ROOT / 'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj')
solution = f'-p:SolutionDir={ROOT.as_posix()}/src/'
common = ['-p:Platform=x64', '-p:PublishAot=false', solution]
if STAGE in ('Debug', 'DebugClean', 'Release'):
    configuration = 'Debug' if STAGE == 'DebugClean' else STAGE
    command = ['dotnet', 'build', app, '-t:Rebuild', '-c', configuration, *common,
               '-p:AppxPackageSigningEnabled=false', '-p:GenerateAppxPackageOnBuild=false']
    if STAGE == 'DebugClean':
        command.append(f'-p:BuildOutDir={(DEST / "build-output").as_posix()}/')
elif STAGE == 'Visor':
    command = ['dotnet', 'build', str(ROOT / 'src/Desktop/BiliCopilot.Visor/BiliCopilot.Visor.csproj'), '-c', 'Release', *common]
elif STAGE == 'RuntimeGate':
    command = ['dotnet', 'msbuild', app, '-t:ValidateRuntimeDependencies', *common]
elif STAGE == 'Tests':
    command = ['dotnet', 'test', str(ROOT / 'tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj'), '-c', 'Release', '--logger', 'trx;LogFileName=integration-net10.trx', '--results-directory', str(DEST / 'trx')]
else:
    raise ValueError(STAGE)
metadata = {'merge': MERGE, 'sourceTree': git('rev-parse', 'HEAD:src'),
            'testTree': git('rev-parse', 'HEAD:tests'),
            'kernel': git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD'),
            'command': command, 'cwd': str(ROOT), 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
            'scope': 'JIT build/test and file gate only; no UI activation, deployment or loaded-module claim'}
save('provenance.json', metadata)
run(['dotnet', '--info'], 'sdk.log')
metadata['exitCode'] = run(command, 'command.log')
metadata['commandCompletedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
# Freeze before any subsequent build, including all reference bin directories.
config = STAGE if STAGE in ('Debug', 'Release') else 'Release'
if STAGE in ('Debug', 'DebugClean', 'Release', 'Visor'):
    build_root = DEST / 'build-output' if STAGE == 'DebugClean' else ROOT / 'src/_build/x64' / config
    for source in build_root.glob('*/bin'):
        if STAGE == 'Visor' and source.parent.name != 'BiliCopilot.Visor':
            continue
        shutil.copytree(source, DEST / 'outputs' / source.parent.name)
if STAGE == 'Tests':
    shutil.copytree(ROOT / 'tests/BiliCopilot.UI.Tests/bin/Release', DEST / 'outputs/tests')
for area in ('src', 'externals/bili-kernel/src', 'tests'):
    for source in (ROOT / area).rglob('project.assets.json'):
        copy(source, DEST / 'resolved-assets' / source.relative_to(ROOT))
for name in ('global.json', 'src/Directory.Build.props', 'src/Directory.Packages.props', 'externals/bili-kernel/src/Directory.Packages.props'):
    source = ROOT / name
    if source.exists():
        copy(source, DEST / 'source-config' / name)
if STAGE in ('Debug', 'DebugClean', 'Release'):
    output = DEST / 'outputs/BiliCopilot.UI'
    if not (output / 'BiliCopilot.UI.dll').exists():
        raise RuntimeError('Expected app output absent')
    activation = DEST / 'activation'
    shutil.copytree(output, activation)
    recipes = list(output.glob('*.build.appxrecipe'))
    if len(recipes) != 1:
        raise RuntimeError('Expected exactly one recipe')
    extras = []
    for node in ET.parse(recipes[0]).getroot().iter():
        if node.tag.split('}')[-1] != 'AppxPackagedFile':
            continue
        relative = node.findtext('PackagePath')
        if relative is None:
            relative = next((n.text for n in node if n.tag.split('}')[-1] == 'PackagePath'), None)
        target = (activation / relative).resolve()
        if not target.is_relative_to(activation.resolve()):
            raise RuntimeError('Recipe destination escapes activation copy')
        if not target.exists():
            source = pathlib.Path(node.attrib['Include'])
            copy(source, target)
            extras.append({'path': relative, 'source': str(source), 'sha256': digest(target)})
    removed = []
    for recipe in activation.rglob('*.appxrecipe'):
        removed.append({'path': str(recipe.relative_to(activation)), 'sha256': digest(recipe)})
        recipe.unlink()
    save('activation-preparation.json', {'addedContent': extras, 'removedRecipes': removed,
        'policy': 'Only independent activation copy changed. Original output recipes retained. Existing payload never overwritten; manifest identity unchanged. Not deployed.'})
metadata['runtimeconfigs'] = {str(p.relative_to(DEST)): json.loads(p.read_text(encoding='utf-8-sig')) for p in (DEST / 'outputs').rglob('*.runtimeconfig.json')} if (DEST / 'outputs').exists() else {}
metadata['freezeCompletedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
save('provenance.json', metadata)
files = [{'path': str(p.relative_to(DEST)), 'bytes': p.stat().st_size, 'sha256': digest(p)} for p in sorted(DEST.rglob('*')) if p.is_file()]
save('files.json', files)
print(json.dumps({'stage': STAGE, 'files': len(files), 'manifestSha256': digest(DEST / 'files.json'), 'directory': str(DEST)}))
