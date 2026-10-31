"""Check test provenance and, after archiving, every evidence entry's hash."""
from pathlib import Path
import json,hashlib,xml.etree.ElementTree as E,zipfile
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-resize'
def tests(name):
    root=E.parse(a/'tests'/name)
    results=root.findall('.//{*}UnitTestResult')
    assert results and all(x.get('outcome')=='Passed' for x in results)
    return {x.get('testName') for x in results}
targeted=tests('targeted.trx');full=tests('full.trx')
assert len(targeted)==98 and len(full)==572 and targeted<=full
record={'targeted':len(targeted),'full':len(full),'targetedSubsetOfFull':True}
(a/'test-audit.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
archive=r/'docs/list-resize/evidence.zip'
if archive.exists():
    with zipfile.ZipFile(archive) as z:
        manifest=json.loads(z.read('evidence-manifest.json'))
        for entry in manifest:
            data=z.read(entry['path'])
            assert len(data)==entry['bytes'] and hashlib.sha256(data).hexdigest()==entry['sha256'],entry['path']
        print('Archive verified:',len(manifest),'entries')
print('Test provenance:',json.dumps(record))
