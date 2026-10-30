"""Archive only explicitly allowed owned-process evidence; never archive layouts/auth/traces."""
import gzip
import hashlib
import json
from pathlib import Path

here=Path(__file__).resolve().parent
raw=here.parents[2]/'artifacts/dependency-cpu-profile'
runs=('r1-base-ref','r1b-base-ref','r2-up-profile','r3-base-ref','r4-up-ref','r5-base-profile')
allowed=('provenance.json','launch.json','hash-gate.json','modules.json','accepted.json','cleanup.json',
    'cleanup-corrected.json','yield.json','summary.json','idle-baseline.json','idle-partial.json',
    'main-results.json','main-ready.json','font-results.json','thread-origins.json','collector.stderr',
    'profile/before.json','profile/after.json','profile/started.json','profile/complete.json',
    'profile/cpu.nettrace.summary.json','player/results.json')
files=[raw/run/name for run in runs for name in allowed if (raw/run/name).is_file()]
files += [raw/name for name in ('tool-capabilities.json','profile-privileges.json','collector-hashes.json',
    'collector-runtime.json','idle-source-hashes.json','final-audit.json','verification.json','parser-hashes.json')]
ledger=[]
for path in files:
    data=path.read_bytes()
    relative=path.relative_to(raw)
    dest=here/'evidence'/str(relative).replace('\\','/')
    dest=dest.with_name(dest.name+'.gz')
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_bytes(gzip.compress(data,mtime=0))
    ledger.append({'original':relative.as_posix(),'archive':dest.relative_to(here).as_posix(),
        'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
(here/'evidence/ledger.json').write_text(json.dumps(ledger,indent=2),encoding='utf-8')
print(f'Archived {len(ledger)} byte-preserved allowlisted files')
