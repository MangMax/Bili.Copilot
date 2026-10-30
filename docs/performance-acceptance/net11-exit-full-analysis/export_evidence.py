"""Export only explicitly selected code/identity evidence, never dump data blobs."""
import hashlib
import json
import pathlib
import shutil
import zipfile
import xml.etree.ElementTree as ET

root = pathlib.Path('artifacts/exit-full-analysis')
dest = pathlib.Path(__file__).parent
for name in ['identities.json', 'metadata.txt', 'native-symbols.json']:
    text = (root/name).read_text(encoding='utf-8-sig')
    (dest/name).write_text('\n'.join(line.rstrip() for line in text.splitlines()).rstrip()+'\n')
lines = (root/'managed.txt').read_text(encoding='utf-8-sig').splitlines()
(dest/'managed-stack.txt').write_text('\n'.join(line for line in lines if not line.startswith('ILMAP'))+'\n')
dis = (root/'timer-disassembly.txt').read_text().splitlines()
selected = [line for line in dis if 0x7ffe93889beb <= int(line[:16],16) <= 0x7ffe93889c48
            or 0x7ffe9388a03a <= int(line[:16],16) <= 0x7ffe9388a042]
(dest/'throw-disassembly.txt').write_text('\n'.join(line.rstrip() for line in selected)+'\n')
dump = json.loads((root/'parsed-dump.json').read_text())
(dest/'stowed-stack.json').write_text(json.dumps({k:dump[k] for k in ['sha256','bytes','pid','thread','code','exceptionAt','stowed']},indent=2)+'\n')
packages = []
for path in sorted((root/'packages').rglob('*.nupkg')):
    with zipfile.ZipFile(path) as z:
        nuspec = ET.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))))
        fields = {x.tag.split('}')[-1]: x.text for x in nuspec.iter()}
    packages.append(dict(id=fields['id'], version=fields['version'], sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                         source='https://api.nuget.org/v3/index.json'))
(dest/'tool-packages.json').write_text(json.dumps(packages,indent=2)+'\n')
sources = json.loads((dest/'source-downloads.json').read_text())
for source in sources:
    source['sha256'] = hashlib.sha256((root/source['file']).read_bytes()).hexdigest()
(dest/'source-downloads.json').write_text(json.dumps(sources,indent=2)+'\n')
print('Exported allowlisted code stacks, module/tool hashes, IL and bounded disassembly.')
