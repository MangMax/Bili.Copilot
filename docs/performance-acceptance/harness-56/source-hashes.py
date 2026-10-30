import hashlib
import json
import pathlib
import sys
import xml.etree.ElementTree as ET
root=pathlib.Path(sys.argv[1]).resolve()
destination=pathlib.Path(sys.argv[2]).resolve()
h=root/'docs/performance-acceptance/harness-56'
files=set()
for item in ET.parse(h/'Harness.csproj').findall('.//Compile'):
    for pattern in item.attrib['Include'].split(';'):
        if pattern.startswith('$(SourceRoot)/'):
            files.update(root.glob(pattern.removeprefix('$(SourceRoot)/')))
for f in (root/'externals/bili-kernel/src/BiliKernel.Abstractions').rglob('*.cs'):
    if not {'bin','obj'}.intersection(f.parts):
        files.add(f)
rows=[{'path':f.relative_to(root).as_posix(),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()} for f in sorted(files)]
destination.write_text(json.dumps(rows,indent=2),encoding='utf-8')
