"""Verify the committed feed against the frozen archive manifest; no restore/build."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

feed = Path(__file__).resolve().parent
root = feed.parent.parent
evidence = root / 'docs/engine-61-design-draft/combined-acceptance'
ledger = json.loads((evidence / 'offline-evidence-manifest.json').read_text(encoding='utf-8'))
sha = lambda data: hashlib.sha256(data).hexdigest()
blob = evidence / 'offline-evidence.zip'
assert sha(blob.read_bytes()) == ledger['sha256'], 'Frozen archive SHA256 mismatch'
versions = {
    'Danmaku.Core': '0.0.1-pxdpi.9dc8c6c9.clean',
    'Danmaku.Legacy': '0.0.1-reconstruct0b4f.final',
    'Danmaku.Share': '0.0.1-reconstruct0b4f.final',
}
central = ET.parse(root / 'src/Directory.Packages.props')
with zipfile.ZipFile(blob) as archive:
    for name, version in versions.items():
        filename = f'{name}.{version}.nupkg'
        data = (feed / filename).read_bytes()
        assert sha(data) == ledger['files']['feed/' + filename], filename
        assert data == archive.read('feed/' + filename), filename
        # Historical package bytes stay frozen; active pins are checked below.
        print(filename, sha(data))
candidate = root / 'docs/seek-responsive-fix'
version = '0.0.1-seekd1fc.20261101.2'
for row in json.loads((candidate / 'packages.json').read_text(encoding='utf-8')):
    original = (candidate / row['path']).read_bytes()
    assert sha(original) == row['sha256']
    if not row['path'].endswith('.nupkg'):
        continue
    filename = Path(row['path']).name
    data = (feed / filename).read_bytes()
    assert data == original, filename
    with zipfile.ZipFile(feed / filename) as archive:
        spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        name = spec.find('.//{*}id').text
        assert spec.find('.//{*}version').text == version
        assert central.find(f'.//PackageVersion[@Include="{name}"]').attrib['Version'] == version
        for dep in spec.findall('.//{*}dependency'):
            if dep.attrib['id'] == 'Danmaku.Share':
                assert dep.attrib['version'] == version
    print(filename, sha(data))
config = ET.parse(root / 'nuget.config')
assert config.find('.//packageSources/add[@key="danmaku-frozen"]').attrib['value'] == 'packages/danmaku'
mapped = config.findall('.//packageSourceMapping/packageSource[@key="danmaku-frozen"]/package')
assert {p.attrib['pattern'] for p in mapped} == set(versions)
print('PASS: original package bytes, fixed central versions, relative source and exact ID mapping')
