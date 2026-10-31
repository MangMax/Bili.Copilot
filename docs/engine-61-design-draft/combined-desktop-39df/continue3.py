from pathlib import Path
import sys
source=Path('docs/engine-61-design-draft/combined-desktop-39df/run-ui.py').read_text()
p=Path('artifacts/combined-60cd/full/runtime3/continuation');p.mkdir();(p/'identity.json').write_bytes((p.parent/'identity.json').read_bytes())
sys.argv=['continue','runtime3/continuation'];exec(compile(source.split('\ntry:')[0],'run-ui-prefix','exec'))
original_snap=snap
def snap(name):
 for attempt in range(3):
  try:return original_snap(name)
  except AssertionError as ex:
   if 'foreground_not_target' not in str(ex):raise
   write(name+'-capture-attempt'+str(attempt)+'.json',dict(error=str(ex),usable=False));time.sleep(.7)
 write(name+'-not-captured.json',dict(usable=False,reason='foreground guard; no foreign capture used'))
from assertions import lifetime
try:
 lifetime(globals(),'Legacy')
 tail=source[source.index(" invoke('Core');time.sleep(1);seek20()\n invoke('HideDanmaku')"):source.index('\nfinally:')]
 exec(compile('if True:\n'+tail,'remaining-matrix','exec'))
finally:
 p=subprocess.run(['winapp','ui','yield','--on','local','--json'],capture_output=True,text=True,encoding='utf8');write('yield.json',dict(exit=p.returncode,stdout=p.stdout,stderr=p.stderr))
