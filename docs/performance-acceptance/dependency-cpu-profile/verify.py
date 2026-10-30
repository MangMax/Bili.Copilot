"""Verify fresh local deployments/results, or portable archive with --archive."""
import gzip
import hashlib
import json
import sys
from pathlib import Path

here=Path(__file__).resolve().parent
raw=here.parents[2]/'artifacts/dependency-cpu-profile'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.file_digest(p.open('rb'),'sha256').hexdigest().upper()
if '--archive' in sys.argv:
    ledger=read(here/'evidence/ledger.json')
    for entry in ledger:
        data=gzip.decompress((here/entry['archive']).read_bytes())
        assert len(data)==entry['bytes']
        assert hashlib.sha256(data).hexdigest()==entry['sha256'],entry['original']
    print(f'PASS {len(ledger)} archive byte hashes')
    sys.exit(0)
checked=[]
for run in ('r2-up-profile','r3-base-ref','r4-up-ref','r5-base-profile'):
    folder=raw/run
    provenance=read(folder/'provenance.json')
    expected={r['path'].replace('\\','/').lower():r['sha256'].upper() for r in provenance['originalDlls']}
    actual={p.relative_to(folder/'layout').as_posix().lower():sha(p) for p in (folder/'layout').rglob('*.dll')}
    assert actual==expected and len(actual)==357,run
    assert sha(folder/'layout/BiliCopilot.UI.runtimeconfig.json')==provenance['runtimeHash'],run
    accepted=read(folder/'accepted.json')
    assert accepted['explicitExit'] and accepted['rows']==183
    cleanup=read(folder/'cleanup.json')
    assert cleanup['processAbsent'] and cleanup['registrationAbsent']
    player=read(folder/'player/results.json')
    assert len(player)==15 and all(r['status']=='PASS' for r in player)
    main=read(folder/'main-results.json')
    assert len(main)==4 and all(r['status']=='PASS' for r in main)
    baseline=read(folder/'idle-baseline.json')
    for phase in ('visible','hidden','restored'):
        rows=[r for r in baseline['rows'] if r['phase']==phase]
        assert [r['sample'] for r in rows]==list(range(61)) and all(r['scale']==1.5 for r in rows)
    summary=read(folder/'summary.json')
    if provenance['mode']=='profile':
        assert sha(folder/'profile/cpu.nettrace').lower()==summary['trace']['sha256']
        assert summary['eventSummary']['pid']==accepted['pid']
        assert summary['eventSummary']['EventsLost']==0
        assert summary['samples']['count']>0
    checked.append({'run':run,'pid':accepted['pid'],'dlls':len(actual),'rows':183,'player':15,'main':4})
result={'pass':True,'acceptedRuns':checked,'totalDlls':sum(r['dlls'] for r in checked),
    'note':'Fresh post-run two-way DLL/runtime hash checks, structured results, cleanup records and raw trace hashes. Not a production build/test run.'}
(raw/'verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
