"""Verify fresh assets, restore origin, deps and both JIT outputs against sealed packages."""
from pathlib import Path
import hashlib
import base64
import json
import zipfile

root = Path(__file__).resolve().parents[3]
base = root/'artifacts/seek-responsive-integration'
sha = lambda b: hashlib.sha256(b).hexdigest()
app = root/'src/Desktop/BiliCopilot.UI'
data = (app/'obj/project.assets.json').read_bytes()
(base/'app-project.assets.json').write_bytes(data)
assets = json.loads(data)
assert list(assets['packageFolders']) == [str(base/'packages')], assets['packageFolders']
version = '0.0.1-seekd1fc.20261101.2'
rows = []
for name in ('Danmaku.Core', 'Danmaku.Legacy', 'Danmaku.Share'):
    key = name+'/'+version
    assert [k for k in assets['libraries'] if k.startswith(name+'/')] == [key]
    folder = base/'packages'/name.lower()/version
    meta = json.loads((folder/'.nupkg.metadata').read_text())
    assert Path(meta['source']).resolve() == (root/'packages/danmaku').resolve(), meta
    filename = name+'.'+version+'.nupkg'
    original = (root/'packages/danmaku'/filename).read_bytes()
    assert (folder/filename.lower()).read_bytes() == original
    sha512 = base64.b64encode(hashlib.sha512(original).digest()).decode()
    assert assets['libraries'][key]['sha512'] == sha512
    assert meta['contentHash'] == sha512
    assert (folder/(filename.lower()+'.sha512')).read_text().strip() == sha512
    for target in assets['targets'].values():
        if key in target:
            share = target[key].get('dependencies', {}).get('Danmaku.Share')
            if share is not None:
                assert share == version
    with zipfile.ZipFile(root/'packages/danmaku'/filename) as z:
        dll = next(n for n in z.namelist() if n.endswith('/'+name+'.dll'))
        dllhash = sha(z.read(dll))
        assert sha((folder/dll).read_bytes()) == dllhash
    outputs = {}
    for config in ('Debug', 'Release'):
        output = base/config/'build-output/BiliCopilot.UI/bin'
        depsbytes = (output/'BiliCopilot.UI.deps.json').read_bytes()
        (base/config/'BiliCopilot.UI.deps.json').write_bytes(depsbytes)
        deps = json.loads(depsbytes)
        assert [k for k in deps['libraries'] if k.startswith(name+'/')] == [key]
        assert sha((output/(name+'.dll')).read_bytes()) == dllhash
        outputs[config] = dllhash
    rows.append(dict(name=name, version=version, nupkgSha256=sha(original), dllSha256=dllhash, restoreSource=meta['source'], outputs=outputs))
record = dict(assetsSha256=sha(data), packageFolders=list(assets['packageFolders']), packages=rows)
(base/'package-verification.json').write_text(json.dumps(record, indent=2))
print(json.dumps(record, indent=2))
