"""Audit the bounded speed integration and seal fresh evidence, without UI work."""
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
PARENT = 'bd02775bd9a48d7e62c169a6367eec58707dad8e'
BASE = ROOT / 'artifacts/speed-subset-integration' / (PARENT + '-fresh')

def git(*args, **kwargs):
    return subprocess.check_output(['git', *args], cwd=ROOT, **kwargs)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def normalized(data):
    return data.replace(b'\r\n', b'\n')

patch = git('diff', '7ac24a44', '161a47c3', '--', 'src')
names = git('diff', '--name-only', '7ac24a44', '161a47c3', '--', 'src').decode().splitlines()
assert len(names) == 5
playback = git('show', '161a47c3:src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel/PlayerViewModel.Playback.cs')
helper = playback[playback.index(b'    internal Func<bool> CapturePlaybackValidity()'):playback.index(b'    private void OnDanmakuPlayerPropertyChanged')]
with tempfile.TemporaryDirectory(prefix='speed-subset-index-') as tmp:
    env = dict(os.environ, GIT_INDEX_FILE=str(Path(tmp) / 'index'))
    git('read-tree', PARENT, env=env)
    subprocess.run(['git', 'apply', '--cached', '--check', '-'], input=patch, cwd=ROOT, env=env, check=True)
    subprocess.run(['git', 'apply', '--cached', '-'], input=patch, cwd=ROOT, env=env, check=True)
    for name in names:
        expected = git('show', ':' + name, env=env)
        if name.endswith('.Speed.cs'):
            expected = expected.replace(b'    private int _pendingSpeedChanges;', helper + b'    private int _pendingSpeedChanges;')
        assert normalized((ROOT / name).read_bytes()) == normalized(expected), name
assert not git('diff', PARENT, '--', 'tests', 'global.json', 'src/Directory.Packages.props', 'externals')
actual = set(git('diff', '--name-only', PARENT, '--', 'src').decode().splitlines())
actual.update(git('ls-files', '--others', '--exclude-standard', 'src').decode().splitlines())
assert actual == set(names), actual
assert git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD').strip() == b'0ae5e4e000a41a07f6696746706df11c06e8835b'
assert not git('-C', 'externals/bili-kernel', 'status', '--porcelain')

# Preserve and validate historical evidence separately from fresh parent tests.
historical = json.loads((HERE / 'evidence-manifest.json').read_text())
with zipfile.ZipFile(HERE / 'evidence.zip') as archive:
    assert set(archive.namelist()) == set(historical)
    for name, row in historical.items():
        data = archive.read(name)
        assert len(data) == row['bytes'] and sha(data) == row['sha256'], name
for name in git('ls-tree', '-r', '--name-only', 'd5c519dd', '--', str(HERE.relative_to(ROOT)).replace('\\', '/')).decode().splitlines():
    if name.endswith('/probe.py'):
        continue
    data = (ROOT / name).read_bytes()
    original = git('show', 'd5c519dd:' + name)
    if name.endswith('/README.md'):
        assert normalized(data).startswith(normalized(original))
    else:
        assert data == original, name

ledger = []
def archive_file(source, relative):
    data = source.read_bytes()
    target = HERE / 'integration-evidence' / (relative + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(data, mtime=0))
    assert gzip.decompress(target.read_bytes()) == data
    ledger.append({'path': relative, 'bytes': len(data), 'sha256': sha(data)})

ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def trx(path, expected):
    tree = ET.parse(path)
    counters = tree.find('.//t:Counters', ns).attrib
    rows = tree.findall('.//t:UnitTestResult', ns)
    assert len(rows) == expected == int(counters['total']) == int(counters['passed'])
    assert int(counters['failed']) == 0 and all(row.attrib['outcome'] == 'Passed' for row in rows)
    return {'counters': counters, 'names': [row.attrib['testName'] for row in rows]}

summary = {'parent': PARENT, 'productionSource': '161a47c3873927865a48fb35daf419b6d1b68e8e',
           'candidate': 'bb9c45dd37053327f7cd1c1c176757968a3d8296',
           'review': 'd5c519dd4924997ae41ab0358c728b63763e8f7e',
           'productionFiles': {name: sha((ROOT / name).read_bytes()) for name in names},
           'historicalArchiveFilesVerified': len(historical), 'stages': {}}
probe = ROOT / 'artifacts/speed-close/parent-subset-fresh'
summary['freshProbe'] = trx(probe / 'results.trx', 8)
for name in ['results.trx', 'run.log', 'source.json', 'Production.cs', 'Speed.cs', 'Adapters.cs', 'Tests.cs', 'Probe.csproj']:
    archive_file(probe / name, 'probe/' + name)
for stage in ['Tests', 'DebugClean', 'Release', 'RuntimeGate']:
    folder = BASE / stage
    files = json.loads((folder / 'files.json').read_text())
    assert {str(p.relative_to(folder)) for p in folder.rglob('*') if p.is_file()} - {'files.json'} == {r['path'] for r in files}
    for row in files:
        data = (folder / row['path']).read_bytes()
        assert len(data) == row['bytes'] and sha(data) == row['sha256'], (stage, row['path'])
    provenance = json.loads((folder / 'provenance.json').read_text(encoding='utf-8'))
    assert provenance['exitCode'] == 0
    for name in names:
        assert provenance['workingSourceSha256'][str(Path(name))] == sha((ROOT / name).read_bytes())
    summary['stages'][stage] = {'exitCode': 0, 'command': provenance['command'], 'filesVerified': len(files)}
    for name in ['command.log', 'sdk.log', 'provenance.json', 'files.json']:
        archive_file(folder / name, stage + '/' + name)
    if stage == 'Tests':
        path = folder / 'trx/speed-subset-suite.trx'
        summary['freshParentSuite'] = trx(path, 539)
        archive_file(path, 'Tests/speed-subset-suite.trx')
summary['evidence'] = ledger
(HERE / 'integration-summary.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps({'probe': 8, 'parentSuite': 539, 'historicalArchiveFiles': len(historical), 'stages': list(summary['stages']), 'productionFiles': len(names)}))
