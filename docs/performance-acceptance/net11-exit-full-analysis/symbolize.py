"""Verify complete MSF/RSDS identity before resolving stowed native PCs."""
import bisect
import hashlib
import json
import pathlib
import struct
import uuid

root = pathlib.Path('artifacts/exit-full-analysis')
dump = json.loads((root / 'parsed-dump.json').read_text())
stack = {'frames': [{'module': f['location'], 'pc': f['address']}
                    for f in dump['stowed'][0]['stack']]}
source = pathlib.Path('docs/performance-acceptance/net11-exit-crash/pdb-publics.py').read_text()
exec(source[source.index('def parse('):source.index("for name in ['ntdll.dll'")])
result = []
for name in ['CoreMessagingXP.dll', 'combase.dll', 'Microsoft.UI.Xaml.dll']:
    m = next(m for m in dump['modules'] if m['name'] == name)
    try:
        with (root / 'symbols' / m['pdb']).open('rb') as f:
            assert f.read(32) == b'Microsoft C/C++ MSF 7.00\r\n\x1aDS\0\0\0', 'Not an MSF 7 PDB'
        result.append(parse(root / 'symbols' / m['pdb'], m))
    except Exception as error:
        result.append({'name': name, 'status': 'UNAVAILABLE', 'reason': str(error)})
(root / 'native-symbols.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
