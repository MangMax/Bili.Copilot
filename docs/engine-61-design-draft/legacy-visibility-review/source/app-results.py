from pathlib import Path
import json, hashlib
from png_pixels import glyph

O=Path(__file__).resolve().parent
E=O/'full/runtime'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
checks=[]
def check(name, passed, evidence): checks.append(dict(name=name,passed=bool(passed),evidence=evidence))
pixels={x['file']:x for x in read(O/'pixels/pixels.json')['records']}
ref=glyph((E/'screen-remount1-stable.png').read_bytes())
check('BlankNegativeRejected',ref!=bytes(len(ref)) and sum(c>20 for c in ref)==5577,'pixels/pixels.json')
decoded=True
for folder, report in [(E,O/'pixels/pixels.json'),(O/'prior/full/runtime',O/'prior-pixels-v2/pixels.json')]:
    for v in read(report)['records']:
        data=glyph((folder/v['file']).read_bytes())
        decoded &= hashlib.sha256(data).hexdigest().upper()==v['glyphSha256']
check('IndependentDecodersAgree',decoded,'30 PNGs: Python zlib/filter decoder vs System.Drawing')
old=O/'prior/full/runtime'
oldresults=read(old/'results.json')
check('OriginalVerdictContradictedByPixels',
      oldresults['failed']==2 and (old/'screen-remount1-stable.png').read_bytes()==(old/'screen-remount2-stable.png').read_bytes()
      and all(glyph((old/n).read_bytes())==ref for n in ['screen-remount3-stable.png','static-0.png','static-1.png','static-2.png']),
      'Original 11 PASS / 2 FAIL preserved; identical PNG cannot be both visible and absent')
masters=[]
foreground=True
owned_pid=read(E/'identity.json')['pid']
for i in range(1,4):
    names=[f'screen-remount{i}{suffix}' for suffix in ['', '-stable','-static1','-static2']]
    states=[read(E/(n+'.json')) for n in names]
    masters.append(states[0]['visual']['master'])
    check(f'PausedRemountStateRound{i}',all(s['state']['state']=='Paused' and s['state']['position']==20
          and s['visual']['master']==masters[-1] and s['visual']['paused'] and not s['visual']['stopped']
          and s['visual']['time']==20000 and s['visual']['rows'] for s in states),'; '.join(n+'.json' for n in names))
    check(f'PausedRemountVisibleRound{i}',all(glyph((E/(n+'.png')).read_bytes())==ref for n in names),
          '4 fixed samples: first + stable + static1 + static2; exact TOP20 RGB pixels, no retry selection')
    empty=f'screen-unloaded{i}'
    check(f'UnloadNegativeRound{i}',glyph((E/(empty+'.png')).read_bytes())==bytes(len(ref))
          and not read(E/(empty+'.json'))['visual'].get('renderer',False),empty+'.png/json')
    for s in states:
        foreground &= any(w['title']=='Offline full player 224' and w['isForeground'] and w['processId']==owned_pid for w in s['windowsAfter'])
check('DistinctMasters',len(set(masters))==3,str(masters))
check('CapturePlayerForeground',foreground,'snapshot windowsAfter; exact owned PID/HWND')
for name,pos in [('forward1',21),('backward1',20)]:
    s=read(E/f'screen-button-{name}.json')
    img=glyph((E/f'screen-button-{name}.png').read_bytes())
    expected=glyph((old/f'screen-button-{name}.png').read_bytes())
    check('ActualButton'+name,s['state']['position']==pos and s['state']['state']=='Paused' and img==expected
          and (img==ref)==(pos==20),f'screen-button-{name}.json/png')
events=[json.loads(x) for x in (O/'full/events.jsonl').read_text(encoding='utf-8').splitlines()]
closed=next(x for x in events if x['data']['kind']=='closed')
check('CloseFullDisposes',closed['data']['disposed'] and closed['data']['remaining']==0,'events.jsonl')
check('NoPublicationAfterClose',not any(x['at']>closed['at'] and x['data']['kind'] in ['seek-request','redraw-request'] for x in events),'events.jsonl')
check('NoHarnessExceptions',not any(x['data']['kind'].endswith('-error') or x['data']['kind']=='unhandled' for x in events),'events.jsonl; startup CLI empty-window race separately preserved')
ex=read(E/'retainedexit.json');wer=read(E/'wer.json')
check('NormalExit',ex['retained'] and not ex['forced'] and ex['exitCode']==0,'retainedexit.json')
check('WERQuietAfter31Seconds',wer['applicationLogReadable'] and wer['secondsAfterExit']>=31 and not wer['matches'],'wer.json')
check('OwnedCleanupAndYield',read(E/'cleanup.json')==dict(packagesRemaining=0,processRemaining=False) and read(E/'yield.json')['exit']==0,'cleanup.json/yield.json')
result=dict(passed=sum(x['passed'] for x in checks),failed=sum(not x['passed'] for x in checks),skipped=0,checks=checks)
(E/'results.json').write_text(json.dumps(result,indent=2))
print(f"Passed: {result['passed']}, Failed: {result['failed']}, Skipped: 0")
assert not result['failed'],[x for x in checks if not x['passed']]
