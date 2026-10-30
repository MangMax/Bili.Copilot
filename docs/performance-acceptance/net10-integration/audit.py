"""Verify frozen sets, archive small evidence as original-byte gzip, summarize gates."""
import datetime
import gzip
import hashlib
import json
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
MERGE = '5e628048815afc1602d882bcf7e9aa4cbde74b21'
BASE = ROOT / 'artifacts/net10-integration' / MERGE
def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()
def ancestor(commit, tip, kernel=False):
    command = ['git'] + (['-C', 'externals/bili-kernel'] if kernel else [])
    return subprocess.run(command + ['merge-base', '--is-ancestor', commit, tip], cwd=ROOT).returncode == 0

assert git('rev-parse', MERGE + '^1') == 'eff8d24c87620cf2b6025a11b8f8ddda27b1a116'
assert git('rev-parse', MERGE + '^2') == 'cec615167b60352b4a63a732709c5107af65d8c1'
assert not git('diff', 'cec6151', MERGE, '--', 'src', 'tests', 'global.json', 'externals')
included = ['79ffd4f1', '60abad78', 'cce01fd6', '0ac4a89f', '42836305', 'f19f030', '2f1ae8f']
assert all(ancestor(x, MERGE) for x in included)
assert not ancestor('3367dd98a1285feb7e912eab22454be4397dbe32', MERGE)
assert git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD') == '0ae5e4e000a41a07f6696746706df11c06e8835b'
assert not git('-C', 'externals/bili-kernel', 'status', '--porcelain')
assert all(ancestor(x, 'HEAD', True) for x in ['6501985d0f30c55c28b3f78c6e045c2536e1d389', '2ca79bd', 'f02c33f'])
summary = {'merge': MERGE, 'review': git('rev-parse', MERGE + '^2'), 'includedAncestors': included,
           'net11Excluded': True, 'kernel': git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD'),
           'bundleSha256': sha(ROOT / 'docs/performance-acceptance/dependency-upgrade/kernel.bundle'),
           'kernelLocalOnly': True, 'stages': {}, 'auditedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat()}
ledger = []
def archive(source, relative):
    target = HERE / 'evidence' / (relative + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    raw = source.read_bytes()
    target.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(target.read_bytes()) == raw
    ledger.append({'path': relative, 'rawBytes': len(raw), 'rawSha256': hashlib.sha256(raw).hexdigest(), 'gzipSha256': sha(target)})

for stage in ['Debug', 'Release', 'Visor', 'RuntimeGate', 'Tests', 'DebugClean']:
    folder = BASE / stage
    manifest = json.loads((folder / 'files.json').read_text(encoding='utf-8'))
    expected = {r['path'] for r in manifest}
    actual = {str(p.relative_to(folder)) for p in folder.rglob('*') if p.is_file()} - {'files.json'}
    assert actual == expected, (stage, actual ^ expected)
    for row in manifest:
        p = folder / row['path']
        assert p.stat().st_size == row['bytes'] and sha(p) == row['sha256'], (stage, row['path'])
    provenance = json.loads((folder / 'provenance.json').read_text(encoding='utf-8'))
    assert provenance['exitCode'] == 0 and provenance['merge'] == MERGE
    record = {'directory': str(folder), 'files': len(manifest), 'manifestSha256': sha(folder / 'files.json'),
              'command': provenance['command'], 'runtimeconfigs': provenance['runtimeconfigs']}
    if stage in ['Debug', 'DebugClean', 'Release']:
        output, activation = folder / 'outputs/BiliCopilot.UI', folder / 'activation'
        assert not list(activation.rglob('*.appxrecipe'))
        original = {str(p.relative_to(output)): sha(p) for p in output.rglob('*') if p.is_file() and p.suffix != '.appxrecipe'}
        assert all(sha(activation / p) == h for p, h in original.items())
        preparation = json.loads((folder / 'activation-preparation.json').read_text(encoding='utf-8'))
        allowed = set(original) | {r['path'] for r in preparation['addedContent']}
        assert {str(p.relative_to(activation)) for p in activation.rglob('*') if p.is_file()} == allowed
        recipe = next(output.glob('*.build.appxrecipe'))
        packaged = set()
        for node in ET.parse(recipe).getroot().iter():
            if node.tag.split('}')[-1] == 'AppxPackagedFile':
                rel = next(n.text for n in node if n.tag.split('}')[-1] == 'PackagePath')
                assert (activation / rel).is_file(), rel
                packaged.add(rel.lower())
        dlls = {str(p.relative_to(activation)).lower() for p in activation.rglob('*.dll')}
        record['extraDllsOutsideRecipe'] = sorted(dlls - packaged)
        record['acceptedForHandoff'] = stage != 'Debug'
        if stage != 'Debug':
            assert not record['extraDllsOutsideRecipe'], (stage, record['extraDllsOutsideRecipe'])
        options = json.loads((output / 'BiliCopilot.UI.runtimeconfig.json').read_text())['runtimeOptions']
        assert options['tfm'] == 'net10.0' and options['includedFrameworks'][0]['version'] == '10.0.14'
        record['keyHashes'] = {name: sha(output / name) for name in ['BiliCopilot.UI.dll', 'BiliCopilot.UI.runtimeconfig.json', 'coreclr.dll', 'AppxManifest.xml']}
        record['activationFiles'] = len(allowed)
        record['recipeAddedFiles'] = len(preparation['addedContent'])
        record['identity'] = ET.parse(activation / 'AppxManifest.xml').getroot().find('{*}Identity').attrib
    for p in folder.rglob('*'):
        if p.is_file() and (not {'outputs', 'activation', 'build-output'}.intersection(p.relative_to(folder).parts) or p.name.endswith('.runtimeconfig.json')):
            archive(p, stage + '/' + p.relative_to(folder).as_posix())
    summary['stages'][stage] = record

trx = ET.parse(BASE / 'Tests/trx/integration-net10.trx')
counters = trx.find('.//{*}Counters').attrib
assert counters['total'] == counters['executed'] == counters['passed'] == '487'
assert counters['failed'] == counters['notExecuted'] == '0'
summary['freshTests'] = counters
graphs = []
for expected in json.loads((ROOT / 'docs/performance-acceptance/net10-adaptation/production-package-graphs.json').read_text(encoding='utf-8-sig')):
    path = expected['path']
    stage = 'Tests' if path.startswith('tests/') else ('Visor' if '/BiliCopilot.Visor/' in path else 'Release')
    frozen = BASE / stage / 'resolved-assets' / path
    actual = json.loads(frozen.read_text(encoding='utf-8-sig'))
    assert set(actual['libraries']) == set(expected['libraries']), path
    assert set(actual['targets']) == set(expected['targets']), path
    graphs.append({'path': path, 'stage': stage, 'sha256': sha(frozen), 'targets': list(actual['targets']), 'reviewedLibrarySetEqual': True})
summary['resolvedGraphs'] = graphs
sources = []
for prefix, args in [('', []), ('externals/bili-kernel/', ['-C', 'externals/bili-kernel'])]:
    for name in git(*args, 'ls-files').splitlines():
        path = ROOT / prefix / name
        if path.is_file() and path.suffix in {'.cs', '.csproj', '.props', '.targets', '.xaml', '.appxmanifest'} and (prefix or name.startswith(('src/', 'tests/'))):
            sources.append({'path': prefix + name, 'sha256': sha(path)})
(HERE / 'source-hashes.json.gz').write_bytes(gzip.compress(json.dumps(sources, indent=2).encode(), mtime=0))
summary['sourceHashesGzipSha256'] = sha(HERE / 'source-hashes.json.gz')
(HERE / 'archive-ledger.json').write_text(json.dumps(ledger, indent=2), encoding='utf-8')
(HERE / 'summary.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
if '--tracked' in sys.argv:
    for item in ledger:
        subprocess.run(['git', 'ls-files', '--error-unmatch', str((HERE / 'evidence' / (item['path'] + '.gz')).relative_to(ROOT))], cwd=ROOT, stdout=subprocess.DEVNULL, check=True)
print(json.dumps({'closure': 'PASS', 'frozenSets': 'PASS', 'freshTests': counters, 'archives': len(ledger), 'summarySha256': sha(HERE / 'summary.json')}))
