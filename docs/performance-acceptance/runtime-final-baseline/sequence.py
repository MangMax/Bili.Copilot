"""Fixed ABBA once; stop on any failed run. No replacements or retries."""
import datetime, json, subprocess
from prepare import ROOT, BASE, HERE, save
from audit import main as audit

audit()
for name,arm in [('a1-net10','net10'),('b1-net11','net11'),('b2-net11','net11'),('a2-net10','net10')]:
    log=BASE/(name+'.log')
    assert not log.exists(),name
    command=['pwsh','-NoProfile','-File',str(HERE/'replay.ps1'),'-Fixture',str(BASE/arm/'fixture'),'-Run',name,'-Protocol','v3']
    start=datetime.datetime.now(datetime.timezone.utc).isoformat()
    print('START',name,start,flush=True)
    with log.open('wb') as f:
        result=subprocess.run(command,cwd=ROOT,stdout=f,stderr=subprocess.STDOUT)
    save(BASE/(name+'-execution.json'),dict(command=command,startedUtc=start,endedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),exitCode=result.returncode))
    print('END',name,result.returncode,flush=True)
    if result.returncode or not (BASE/name/'accepted.json').exists():
        raise SystemExit('Sequence stopped; no replacement runs')
