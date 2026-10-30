"""Strict allowlist: frozen code modules, matching local DAC, and tool packages."""
import gzip
import hashlib
import json
import pathlib
import struct
import subprocess
import uuid

root = pathlib.Path('artifacts/exit-full-analysis')
frozen = pathlib.Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction/fixture/frozen-output')
manifest = {x['path']: x for x in json.load(gzip.open('docs/performance-acceptance/net11-exit-reproduction/frozen-files.json.gz'))}
mods = {m['name']: m for m in json.loads((root/'parsed-dump.json').read_text())['modules']}

def identity(path):
    data = path.read_bytes()
    u = lambda f, o: struct.unpack_from('<'+f, data, o)
    pe = u('I', 60)[0]
    machine, count, timestamp = u('HHI', pe+4)
    opt = u('H', pe+20)[0]
    sections = [u('IIII', pe+24+opt+i*40+8) for i in range(count)]
    def offset(rva):
        return next(ro+rva-va for vs, va, rs, ro in sections if va <= rva < va+rs)
    dd = pe+24+(112 if u('H', pe+24)[0] == 0x20b else 96)
    rva, length = u('II', dd+6*8)
    result = dict(name=path.name, sha256=hashlib.sha256(data).hexdigest(), machine=hex(machine),
                  timestamp=hex(timestamp), sizeOfImage=u('I', pe+24+56)[0], codeView=[])
    for i in range(length//28):
        pos = offset(rva)+28*i
        if u('I', pos+12)[0] == 2:
            size, _, raw = u('III', pos+16)
            if data[raw:raw+4] == b'RSDS':
                result['codeView'].append(dict(guid=uuid.UUID(bytes_le=data[raw+4:raw+20]).hex.upper(), age=u('I', raw+20)[0]))
    return result

results = []
for name in ['coreclr.dll', 'mscordaccore.dll', 'System.Private.CoreLib.dll', 'BiliCopilot.UI.dll',
             'WinRT.Runtime.dll', 'Richasy.WinUIKernel.Share.dll', 'Microsoft.WinUI.dll',
             'Microsoft.InteractiveExperiences.Projection.dll']:
    rec = identity(frozen/name)
    assert rec['sha256'] == manifest[name]['sha256'], name
    rec['frozenManifestMatch'] = True
    if name in mods:
        m = mods[name]
        assert any(cv['guid']==m['guid'] and cv['age']==m['age'] for cv in rec['codeView']), name
        assert rec['timestamp']==m['timestamp'], name
        assert rec['sizeOfImage'] == m['size'], name
        rec['dumpModuleMatch'] = True
        rec['dumpVersion'] = m['version']
    results.append(rec)
local = identity(pathlib.Path('C:/Program Files/dotnet/shared/Microsoft.NETCore.App/11.0.0-rc.3.26522.148/mscordaccore.dll'))
assert local['sha256'] == next(r['sha256'] for r in results if r['name']=='mscordaccore.dll')
assert local['machine'] == '0x8664'
local['sameAsFrozenDAC'] = True
results.append(local)
name = 'src/Desktop/BiliCopilot.UI/Controls/Core/PlayerInteractivePanel.xaml.cs'
source = subprocess.check_output(['git','show',f'f6ed3ec063df8701bb57115abb40c4f7bcffc386:{name}'])
parent = subprocess.check_output(['git','show',f'f9271158:{name}'])
assert source == parent
results.append(dict(source=name, rcAndNet10ParentEqual=True, sha256=hashlib.sha256(source).hexdigest()))
(root/'identities.json').write_text(json.dumps(results, indent=2))
print(json.dumps(results, indent=2))
