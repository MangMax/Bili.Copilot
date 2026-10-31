from pathlib import Path
import sys
p=Path('docs/engine-61-design-draft/combined-desktop-39df/run-ui.py')
s=p.read_text();prefix=s.split('\ntry:')[0]
out=Path('artifacts/combined-60cd/full/runtime2/supplement');out.mkdir();(out/'identity.json').write_bytes((out.parent/'identity.json').read_bytes())
sys.argv=['supplement','runtime2/supplement'];exec(compile(prefix,str(p),'exec'))
from assertions import lifetime
try:
 for engine in ['Core','Legacy']:
  try:lifetime(globals(),engine)
  except Exception as ex:
   write(engine+'-failure.json',dict(error=repr(ex),state=latest('state'),visual=latest('visual')))
 # Small bounded repeat verifies the final media target after a 2s settling period.
 repetitions=[]
 for engine in ['Core','Legacy']:
  invoke(engine);time.sleep(1);pause()
  for i in range(3):
   seek20();invoke('FastLatest');time.sleep(2)
   repetitions.append(dict(engine=engine,attempt=i,expected=20,state=latest('state'),visual=latest('visual')))
   snap(engine.lower()+'-latest-'+str(i))
 write('latest-reproductions.json',repetitions)
 invoke('ExitOwned')
finally:
 p=subprocess.run(['winapp','ui','yield','--on','local','--json'],capture_output=True,text=True,encoding='utf8');write('yield.json',dict(exit=p.returncode,stdout=p.stdout,stderr=p.stderr))
