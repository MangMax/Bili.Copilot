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
if blob.exists():
    assert sha(blob.read_bytes()) == ledger['sha256'], 'Frozen archive SHA256 mismatch'
versions = {
    'Danmaku.Core': '0.0.1-pxdpi.9dc8c6c9.clean',
    'Danmaku.Legacy': '0.0.1-reconstruct0b4f.final',
    'Danmaku.Share': '0.0.1-reconstruct0b4f.final',
}
central = ET.parse(root / 'src/Directory.Packages.props')
from contextlib import nullcontext
with (zipfile.ZipFile(blob) if blob.exists() else nullcontext(None)) as archive:
    for name, version in versions.items():
        filename = f'{name}.{version}.nupkg'
        data = (feed / filename).read_bytes()
        assert sha(data) == ledger['files']['feed/' + filename], filename
        if archive is not None:
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
        # Historical seek packages remain byte-identical; active pins checked below.
        for dep in spec.findall('.//{*}dependency'):
            if dep.attrib['id'] == 'Danmaku.Share':
                assert dep.attrib['version'] == version
    print(filename, sha(data))
config = ET.parse(root / 'nuget.config')
assert config.find('.//packageSources/add[@key="danmaku-frozen"]').attrib['value'] == 'packages/danmaku'
mapped = config.findall('.//packageSourceMapping/packageSource[@key="danmaku-frozen"]/package')
assert {p.attrib['pattern'] for p in mapped} == set(versions)
print('PASS: historical package hashes, relative source and exact ID mapping; archive cross-check', blob.exists())

active = {'Danmaku.Core': ('0.0.1-seekd1fc.20261101.2', 'a8869b0a71dd15a77cc0e78bda8f868bbdb8fe1492b8cbb552464b8ccdaf0983'),
          'Danmaku.Legacy': ('0.0.1-win2dlayout.20261102.2', 'a8be93f2798d816eeb157df12a0bd7e1ddc953e654704db5bfd2f71e677a01a4'),
          'Danmaku.Share': ('0.0.1-win2dlayout.20261102.2', '5fcdd7f35736c30733d6ca206ad0892946c3b00d4c01403067c6c16b092a6313')}
for name, (version, digest) in active.items():
    path = feed / f'{name}.{version}.nupkg'
    assert sha(path.read_bytes()) == digest, path
    assert central.find(f'.//PackageVersion[@Include="{name}"]').attrib['Version'] == version
    with zipfile.ZipFile(path) as archive:
        spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        assert spec.find('.//{*}id').text == name
        assert spec.find('.//{*}version').text == version
        for dep in spec.findall('.//{*}dependency'):
            if dep.attrib['id'] == 'Danmaku.Share':
                # Core's older minimum is satisfied by the centrally pinned Share.
                assert dep.attrib['version'] == version
    print('active', path.name, digest)
print('PASS: mixed-version active pins and immutable package hashes')
