from pathlib import Path
import hashlib,json,zipfile
from png_pixels import glyph
D=Path(__file__).resolve().parent
sha=lambda b:hashlib.sha256(b).hexdigest()
manifest=json.loads((D/'evidence-manifest.json').read_text())
with zipfile.ZipFile(D/'evidence.zip') as z:
    assert set(z.namelist())==set(manifest)
    for n,h in manifest.items():assert sha(z.read(n))==h,n
    read=lambda n:json.loads(z.read(n).decode('utf-8-sig'))
    for arm,passed,failed in [('red',8,10),('clean',18,0),('extended',17,1),('final',18,0),('extendedfinal',18,0),('lifetimes',21,0),('regression',12,0),('earlyclose',5,0)]:
        r=read(f'reconstruct-{arm}/evidence/native/results.json')
        assert (r['passed'],r['failed'],r['skipped'])==(passed,failed,0),arm
        assert r['executionFailed']==(failed>0),arm
        assert len(set(x['name'] for x in r['cases']))==passed+failed
    assert z.read('reconstruct-red/evidence/native/Program.cs')==z.read('reconstruct-final/evidence/native/Program.cs')
    for arm in ['red','clean','final']:
        for p in read(f'legacy-reconstruct/pixels-{arm}/pixels.json'):
            assert sha(glyph(z.read(f'reconstruct-{arm}/evidence/native/'+p['file']),rect=p['rect'])).upper()==p['sha256']
    for p in read('legacy-reconstruct/app-pixels-final/pixels.json')['records']:
        assert sha(glyph(z.read('reconstruct-app/full/runtime2/'+p['file']),geometry=(1680,1110),rect=(710,18,260,40))).upper()==p['glyphSha256']
    for arm in ['red','clean','extended','final','extendedfinal','lifetimes','regression','earlyclose']:
        root=f'reconstruct-{arm}/evidence/native/'
        assert read(root+'retainedexit.json')['exitCode']==0
        w=read(root+'wer.json');assert w['secondsAfterExit']>=31 and not w['matches']
        c=read(root+'cleanup.json');assert c['packagesRemaining']==0 and not c['processRemaining']
    assert read('legacy-reconstruct/validation.json')['failed']==0
    assert all(x['passed'] for x in read('reconstruct-final/compat-results/results.json'))
with zipfile.ZipFile(D/'candidate-source.zip') as z:
    for n,h in json.loads((D/'source.json').read_text())['files'].items():assert sha(z.read(n))==h,n
print(f'Audit PASS: {len(manifest)} sealed files; preserved red results, source ZIP, native/package/application evidence and pixels verified')
