"""Audit fresh headless/JIT integration outputs; no UI or native execution."""
from pathlib import Path
import hashlib, json, subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
base = root / 'artifacts/renderer-fault-integration'
def git(*args):
    return subprocess.check_output(['git', *args], cwd=root, text=True).strip()
def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

assert git('diff', '4ca81f88', 'HEAD', '--', 'src', 'tests', 'packages', 'externals') == ''
assert git('-C', 'externals/bili-kernel', 'rev-parse', 'HEAD') == '0ae5e4e000a41a07f6696746706df11c06e8835b'
assert git('-C', 'externals/bili-kernel', 'status', '--porcelain') == ''
results = {}
for stage in ('Full', 'Release', 'RuntimeGate'):
    receipt = json.loads((base / ('ArticleAvatar' + stage) / 'provenance.json').read_text())
    assert receipt['exitCode'] == 0
    for p, h in receipt['sourceHashes'].items():
        assert sha(root / p) == h, p
    for p, h in receipt['outputs'].items():
        assert sha(base / p) == h, p
    results[stage] = {k: receipt[k] for k in ('exitCode', 'startedUtc', 'completedUtc')}
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def names(tree):
    return sorted(x.attrib['testName'] for x in tree.findall('.//t:UnitTestResult', ns))
fresh = ET.parse(base / 'ArticleAvatarFull/trx/results.trx')
assert names(fresh) == names(ET.parse(base / 'Full/trx/results.trx'))
results['testCounters'] = fresh.find('.//t:Counters', ns).attrib
app = base / 'ArticleAvatarRelease/build-output/BiliCopilot.UI/bin'
results['runtimeFiles'] = {}
for rel in ('libmpv/x64/libmpv-2.dll', 'ffmpeg/x64/ffmpeg.exe', 'bbdown/x64/BBDown.exe'):
    assert sha(app / 'Assets' / rel) == sha(root / 'src/Desktop/BiliCopilot.UI/Assets' / rel)
    results['runtimeFiles'][rel] = sha(app / 'Assets' / rel)
assert (app / 'resources.pri').stat().st_size > 0
results['resourcesPriSha256'] = sha(app / 'resources.pri')
out = root / 'artifacts/article-avatar-integration/audit.json'
out.write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
