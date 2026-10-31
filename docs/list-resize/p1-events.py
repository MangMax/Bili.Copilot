"""Correlate, rather than sum overlapping native decode awaits."""
from pathlib import Path
import json, sys
p = Path(sys.argv[1])
events = json.loads(p.read_text(encoding='utf-8-sig'))
beats = [e['ms'] for e in events if e['kind']=='heartbeat']
if len(beats)>1:
    start,end = max(zip(beats,beats[1:]),key=lambda a:a[1]-a[0])
    print('max heartbeat interval',start,end,'gap',end-start)
    print('events in interval',[(e['kind'],round(e['ms'],3),e['index']) for e in events if start<=e['ms']<=end])
for kind in ('decode','surface','surface-remount','draw'):
    starts = {e['index']:e['ms'] for e in events if e['kind']==kind+'-start'}
    intervals = [(starts[e['index']],e['ms']) for e in events if e['kind']==kind+'-end' and e['index'] in starts]
    print(kind,'count',len(intervals),'max',max((b-a for a,b in intervals),default=0))
submissions=[]
last=None
for e in events:
    if e['kind']=='decode-start':last=e['ms']
    elif e['kind']=='decode-submitted' and last is not None:submissions.append(e['ms']-last)
print('decode synchronous submission max',max(submissions,default=0))
