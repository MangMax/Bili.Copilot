"""Byte-preserving small evidence archive. Run after all sampling/cleanup."""
import gzip, hashlib, json, pathlib
root=pathlib.Path(__file__).resolve().parents[3]
out=pathlib.Path(__file__).parent/'evidence'
out.mkdir(exist_ok=True)
diag=root/'artifacts/dependency-performance-diagnosis'
native=root/'artifacts/dependency-upgrade'
files=set(p for p in diag.iterdir() if p.is_file() and p.suffix in ('.json','.log','.props','.patch'))
files.discard(diag/'initial-kernel.patch')
files={p for p in files if 'partial' not in p.name}
files.update((diag/'lcs-results').glob('*.json'))
files.update((diag/'lcs-tiered-off-results').glob('*.json'))
files.add(diag/'lcs/source-ledger.json')
for fixture in ('player-a','player-b','player-c','controls-b','controls-c'):
    for name in ('frozen-all-files.json','frozen-content-provenance.json','Directory.Packages.props','BiliCopilot.UI.csproj','Harness.csproj','source-provenance.txt'):
        p=diag/fixture/name
        if p.exists(): files.add(p)
for p in native.glob('*d34*'):
    if p.is_file(): files.add(p)
    elif p.is_dir(): files.update(x for x in p.rglob('*') if x.is_file())
for p in diag.glob('controls-*-results'):
    files.update(x for x in p.rglob('*') if x.is_file())
ledger=[]
for p in sorted(files):
    raw=p.read_bytes(); rel=p.relative_to(root).as_posix()
    record=dict(path=rel,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())
    if p.suffix!='.png' and len(raw)<8_000_000:
        target=out/(hashlib.sha256(rel.encode()).hexdigest()[:12]+'-'+p.name+'.gz')
        target.write_bytes(gzip.compress(raw,mtime=0))
        record['archive']=target.name
        assert gzip.decompress(target.read_bytes())==raw
    ledger.append(record)
(out/'ledger.json').write_text(json.dumps(ledger,indent=2),encoding='utf-8')
print(f'{len(ledger)} originals, {sum("archive" in r for r in ledger)} byte-preserved archives')
