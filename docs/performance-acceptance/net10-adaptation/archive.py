"""Preserve small raw evidence byte-for-byte; hashes are of raw bytes, not text IO."""
import gzip, hashlib, json, pathlib
root=pathlib.Path(__file__).resolve().parents[3]
data=root/'artifacts/net10-adaptation'; dest=pathlib.Path(__file__).parent/'evidence'
dest.mkdir(exist_ok=True)
ledger=[]
files=[f for f in data.glob('*.log') if f.name!='evidence-driver.log']+list((data/'tests').glob('*.trx'))+list((data/'tests-final').glob('*.trx'))
files.extend(data.glob('final-*.json'))
for arm in ('player9common','player10common','player10final','controls10'):
    folder=data/arm
    for pattern in ('*.csproj','Directory.*.props','source-provenance.txt','source-hashes.json','frozen-*.json','obj/project.assets.json'):
        files.extend(folder.glob(pattern))
for run in ('r1-net9','r2-net10','r3-net9','r4-net10','r5-net9','final-net10','controls-native','v3-r1-net9','v3-r2-net10','v3-r3-net9','v3-r4-net10'):
    folder=data/run
    for f in folder.rglob('*'):
        rel=f.relative_to(folder)
        if f.is_file() and not any(p in ('layout','input') for p in rel.parts) and f.suffix in ('.json','.jsonl','.log','.stdout','.stderr','.ps1','.py','.png','.wer'):
            files.append(f)
for file in files:
    relative=file.relative_to(data); raw=file.read_bytes()
    entry=dict(path=str(relative),bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())
    if file.suffix!='.png':
        target=dest/(str(relative)+'.gz'); target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(gzip.compress(raw,mtime=0)); entry['archive']=str(target.relative_to(dest))
    ledger.append(entry)
(dest/'ledger.json').write_text(json.dumps(ledger,indent=2),encoding='utf-8')
for entry in ledger:
    if 'archive' in entry:
        assert hashlib.sha256(gzip.decompress((dest/entry['archive']).read_bytes())).hexdigest()==entry['sha256']
print(json.dumps({'files':len(ledger),'archived':sum('archive' in x for x in ledger),'rawByteHashVerification':'PASS'}))
