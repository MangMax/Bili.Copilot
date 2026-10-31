from pathlib import Path
import hashlib, json, zipfile, xml.etree.ElementTree as ET
D=Path(__file__).resolve().parent
sha=lambda b: hashlib.sha256(b).hexdigest()
m=json.loads((D/'evidence-manifest.json').read_text())
assert sha((D/'evidence.zip').read_bytes())==m['sha256']
with zipfile.ZipFile(D/'evidence.zip') as z:
    assert set(z.namelist())==set(m['files'])
    for n,h in m['files'].items(): assert sha(z.read(n))==h,n
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    for n,expected in [('full.trx',(572,572,0)),('review-red.trx',(1,0,1)),('review.trx',(3,3,0))]:
        c=ET.fromstring(z.read('tests/'+n)).find('t:ResultSummary/t:Counters',ns).attrib
        actual=tuple(int(c[k]) for k in ['total','passed','failed'])
        assert actual==expected,(n,actual)
        assert int(c['notExecuted'])==0
        print(n,actual)
    red=z.read('review-red.log').decode()
    assert 'Expected: 20' in red and 'Actual:   19' in red
    assert json.loads(z.read('red-command.json'))['exit']==1
    assert json.loads(z.read('green-command.json'))['exit']==0
with zipfile.ZipFile(D.parent/'evidence.zip') as z:
    names=['runtime1/legacy-latest20.png','runtime3/legacy-first-activation.png','runtime3/legacy-latest20-stable.png']
    assert all(z.read(n)==(D.parent/'legacy-latest20.png').read_bytes() for n in names)
print('PASS: review hashes, fresh full suite, controlled final19 red, three boundary greens, identical Legacy frame evidence')
