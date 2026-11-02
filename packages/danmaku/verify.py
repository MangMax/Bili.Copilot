"""Verify the committed Danmaku feed against the active central pins."""
from pathlib import Path
import hashlib
import xml.etree.ElementTree as ET
import zipfile

feed = Path(__file__).resolve().parent
root = feed.parent.parent
sha = lambda data: hashlib.sha256(data).hexdigest()

active = {
    'Danmaku.Core': (
        '0.0.1-seekd1fc.20261101.2',
        'a8869b0a71dd15a77cc0e78bda8f868bbdb8fe1492b8cbb552464b8ccdaf0983',
    ),
    'Danmaku.Legacy': (
        '0.0.1-win2dlayout.20261102.2',
        'a8be93f2798d816eeb157df12a0bd7e1ddc953e654704db5bfd2f71e677a01a4',
    ),
    'Danmaku.Share': (
        '0.0.1-win2dlayout.20261102.2',
        '5fcdd7f35736c30733d6ca206ad0892946c3b00d4c01403067c6c16b092a6313',
    ),
}

central = ET.parse(root / 'src/Directory.Packages.props')
config = ET.parse(root / 'nuget.config')
source = next(
    s for s in config.findall('.//packageSources/add')
    if s.attrib['value'] == 'packages/danmaku'
)
key = source.attrib['key']
mapped = {
    p.attrib['pattern']
    for p in config.findall(f'.//packageSourceMapping/packageSource[@key="{key}"]/package')
}
assert mapped == set(active), mapped

for name, (version, digest) in active.items():
    path = feed / f'{name}.{version}.nupkg'
    assert path.exists(), path
    data = path.read_bytes()
    assert sha(data) == digest, path
    assert central.find(f'.//PackageVersion[@Include="{name}"]').attrib['Version'] == version, name
    with zipfile.ZipFile(path) as archive:
        spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        assert spec.find('.//{*}id').text == name, path
        assert spec.find('.//{*}version').text == version, path
        for dep in spec.findall('.//{*}dependency'):
            if dep.attrib['id'] == 'Danmaku.Share':
                # Core's older minimum is satisfied by the centrally pinned Share.
                assert dep.attrib['version'] == version, (name, dep.attrib['version'])
    print('active', path.name, digest)

print('PASS: active feed, central pins and source mapping')
