"""Check fixed integration scope and archive fresh evidence without running UI."""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
base = root/'artifacts/home-refresh-integration'
def git(*args):
    return subprocess.check_output(['git', *args], cwd=root, text=True, encoding='utf-8').strip()
candidate = git('rev-parse', 'cd915f2')
review = git('rev-parse', 'b5b1b55')
for tip in (candidate, review):
    subprocess.run(['git', 'merge-base', '--is-ancestor', tip, 'HEAD'], cwd=root, check=True)
assert not git('diff', candidate, 'HEAD', '--', 'src', 'tests', 'externals')
assert not git('diff', '8c797bfd', 'HEAD', '--', 'externals', 'src/Directory.Packages.props', 'src/Directory.Build.props', 'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj', 'src/Desktop/BiliCopilot.UI/Danmaku', 'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel', 'src/Desktop/BiliCopilot.UI/Controls/RecommendationCoverImage.cs')
assert not git('-C', 'externals/bili-kernel', 'status', '--porcelain=v1')
diff = git('diff', '8c797bfd', 'HEAD', '--', 'src')
binding_lines = [s for s in diff.splitlines() if s.startswith('+') and '="{Binding}"' in s]
assert len(binding_lines) == 0
stages = {}
for stage in ('Tests', 'Release', 'RuntimeGate'):
    record = json.loads((base/stage/'provenance.json').read_text())
    assert record['exitCode'] == 0
    assert record['sourceTree'] == git('rev-parse', 'HEAD:src')
    for path, sha in record['sourceHashes'].items():
        assert hashlib.sha256((root/path).read_bytes()).hexdigest() == sha, path
    for path, sha in record['outputs'].items():
        assert hashlib.sha256((root/path).read_bytes()).hexdigest() == sha, path
    stages[stage] = {'exitCode': record['exitCode'], 'head': record['head']}
    if stage != 'RuntimeGate':
        pdb = json.loads((base/stage/'pdb.json').read_text())
        present = [d for d in pdb['documents'] if d['exists']]
        assert all(d['matches'] for d in present)
        stages[stage]['pdbDocuments'] = len(pdb['documents'])
        stages[stage]['matchingAvailableDocuments'] = len(present)
    if stage in ('Debug', 'Release'):
        output = base/stage/'build-output/BiliCopilot.UI/bin'
        documents = {str(Path(d['path']).resolve()).lower(): d for d in pdb['documents']}
        absent = []
        changed = git('diff', '8c797bfd', 'HEAD', '--name-only', '--', 'src').splitlines()
        for path in changed:
            doc = documents.get(str((root/path).resolve()).lower())
            if doc is None:
                absent.append(path)
            else:
                assert doc['matches'], path
        assert absent == []
        stages[stage]['changedFilesMatchedInPdb'] = len(changed) - len(absent)
        stages[stage]['resourceDictionaryNotInPdb'] = absent
        asset_hashes = {}
        for relative in ('libmpv/x64/libmpv-2.dll', 'ffmpeg/x64/ffmpeg.exe', 'bbdown/x64/BBDown.exe'):
            source = root/'src/Desktop/BiliCopilot.UI/Assets'/relative
            target = output/'Assets'/relative
            source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            assert hashlib.sha256(target.read_bytes()).hexdigest() == source_hash
            asset_hashes[relative] = source_hash
        assert (output/'resources.pri').stat().st_size > 0
        stages[stage]['outputRuntimeAssetHashes'] = asset_hashes
assert json.loads((base/'Tests/provenance.json').read_text())['counters']['passed'] == '577'
fresh = ET.parse(base/'Tests/trx/all.trx').getroot()
with zipfile.ZipFile(root/'docs/performance-acceptance/home-refresh-navigation/evidence.zip') as archive:
    historical = ET.fromstring(archive.read('test-results/all-green.trx'))
def names(tree):
    return sorted(r.attrib['testName'] for r in tree.findall('.//{*}UnitTestResult'))
assert len(names(fresh)) == 577 and names(fresh) == names(historical)
assert fresh.find('.//{*}Counters').attrib['notExecuted'] == '0'
subprocess.run(['git', 'diff', '--check', '8c797bfd', 'HEAD'], cwd=root, check=True)
summary = dict(candidate=candidate, review=review, merge=git('rev-parse', 'HEAD'), sourceTree=git('rev-parse', 'HEAD:src'), kernel=git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD'), bindings=0, stages=stages)
(base/'audit.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
files = [p for p in base.rglob('*') if p.is_file() and 'build-output' not in p.parts and p.suffix in ('.json', '.log', '.trx')]
files += [root/'artifacts/searchbox-properties-integration/Metadata/Program.cs']
manifest = {}
with zipfile.ZipFile(root/'docs/performance-acceptance/home-refresh-integration/evidence.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for p in files:
        data = p.read_bytes()
        name = str(p.relative_to(root)).replace('\\', '/')
        archive.writestr(name, data)
        manifest[name] = {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
    archive.writestr('manifest.json', json.dumps(manifest, indent=2))
with zipfile.ZipFile(root/'docs/performance-acceptance/home-refresh-integration/evidence.zip') as archive:
    for name, expected in manifest.items():
        data = archive.read(name)
        assert len(data) == expected['bytes'] and hashlib.sha256(data).hexdigest() == expected['sha256']
print(json.dumps(summary, indent=2))
