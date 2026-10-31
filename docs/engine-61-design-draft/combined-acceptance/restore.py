"""Verify and recover the sealed offline packages, symbols and source documents.
Does not register, launch, install or modify application dependencies.
"""
from pathlib import Path
import hashlib,json,zipfile
R=Path.cwd();D=Path(__file__).resolve().parent;dest=R/'artifacts/combined-restored-60cd'
ledger=json.loads((D/'offline-evidence-manifest.json').read_text())
blob=D/'offline-evidence.zip';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(blob.read_bytes())==ledger['sha256']
assert not dest.exists(),'Recovery requires an unused destination'
with zipfile.ZipFile(blob) as z:
 assert set(z.namelist())==set(ledger['files'])
 for n,h in ledger['files'].items():
  assert sha(z.read(n))==h,n
  assert (dest/n).resolve().is_relative_to(dest.resolve()),n
 dest.mkdir(parents=True)
 z.extractall(dest)
print('Verified offline evidence restored:',dest)
print('UI remains NOT RUN. Do not launch while the user desktop hold is active.')
