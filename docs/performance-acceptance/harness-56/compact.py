"""Keep collection reason/timing evidence; preserve full parse beside ignored trace."""
import hashlib
import json
import pathlib
import sys
source=pathlib.Path(sys.argv[1]).resolve()
destination=pathlib.Path(sys.argv[2]).resolve()
raw=json.loads(source.read_text(encoding='utf-8-sig'))
kept={'GC/Start','GC/Stop'}
out={'lost':raw['lost'],'subset':'GC/Start and GC/Stop only; full parse and original nettrace retained',
     'events':[e for e in raw['events'] if e['name'] in kept]}
destination.write_text(json.dumps(out,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
(destination.parent/'raw-events-hash.json').write_text(json.dumps({'path':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'fullEventCount':len(raw['events']),'kept':len(out['events'])},indent=2),encoding='utf-8')
