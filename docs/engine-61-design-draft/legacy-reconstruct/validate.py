from pathlib import Path
import hashlib,json,zipfile,subprocess
from png_pixels import glyph
R=Path.cwd(); O=R/'artifacts/legacy-reconstruct'; D=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
checks=[]
def check(name,ok):
    checks.append(dict(name=name,passed=bool(ok)))
    assert ok,name

for arm in ['red','clean','final']:
    source=R/'artifacts'/('reconstruct-'+arm)/'evidence/native'
    pixels=read(O/('pixels-'+arm)/'pixels.json')
    check('NativeIndependentPixels_'+arm,all(hashlib.sha256(glyph((source/x['file']).read_bytes(),rect=x['rect'])).hexdigest().upper()==x['sha256'] for x in pixels))
    by={x['file']:x for x in pixels}
    if arm=='red':check('OriginalPreTargetPixelsBlank',all(x['nonzero']==0 for x in pixels))
    else:
        check('NativeFourModesVisible_'+arm,all(by[x+'.png']['nonzero']>1000 for x in ['TOP','BOTTOM','ROLL','REVERSE']))
        check('NativeStableAndNegatives_'+arm,by['TOP.png']['sha256']==by['TOPSTABLE.png']['sha256'] and by['BLANK.png']['nonzero']==0 and by['OTHER.png']['sha256']!=by['TOP.png']['sha256'])

app=R/'artifacts/reconstruct-app/full'; run=app/'runtime2'
pixels=read(O/'app-pixels-final/pixels.json')['records']
by={x['file']:x for x in pixels}
check('AppIndependentPixelDecoder',all(hashlib.sha256(glyph((run/x['file']).read_bytes(),geometry=(1680,1110),rect=(710,18,260,40))).hexdigest().upper()==x['glyphSha256'] for x in pixels))
masters=[]
for i in range(1,4):
    names=[f'screen-remount{i}{suffix}' for suffix in ['', '-stable','-static1','-static2']]
    states=[read(run/(n+'.json')) for n in names]
    masters.append(states[0]['visual']['master'])
    check('AppPausedRemountStateRound'+str(i),all(x['state']['state']=='Paused' and x['state']['position']==20 and x['visual']['paused'] and x['visual']['time']==20000 and [r['text'] for r in x['visual']['rows']]==['PRIOR19'] for x in states))
    check('AppPausedRemountVisibleRound'+str(i),all(by[n+'.png']['matchesPrior19'] and by[n+'.png']['nonzero']>1000 for n in names))
    check('AppUnloadBlankRound'+str(i),by[f'screen-unloaded{i}.png']['nonzero']==0)
check('AppDistinctMasters',len(set(masters))==3)
for label,target,texts in [('forward1',21,['PRIOR19','FUTURE21']),('backward1',20,['PRIOR19'])]:
    s=read(run/('screen-button-'+label+'.json'))
    check('AppActualButton_'+label,s['state']['state']=='Paused' and s['state']['position']==target and [x['text'] for x in s['visual']['rows']]==texts)
# The next row must appear only after forward: fixed region, same video background.
baseline=glyph((run/'screen-baseline20.png').read_bytes(),geometry=(1680,1110),rect=(710,58,260,25))
forward=glyph((run/'screen-button-forward1.png').read_bytes(),geometry=(1680,1110),rect=(710,58,260,25))
backward=glyph((run/'screen-button-backward1.png').read_bytes(),geometry=(1680,1110),rect=(710,58,260,25))
check('AppFutureGlyphOnlyAtBirth',baseline==backward and forward!=baseline)
events=[json.loads(x)['data'] for x in (app/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
check('AppCloseDisposes',any(x['kind']=='closed' and x['disposed'] and x['remaining']==0 for x in events))
check('AppNoHarnessErrors',not any(x['kind'] in ['unhandled','startup-error','action-error','observe-error'] for x in events))
for name,root in [('red',R/'artifacts/reconstruct-red/evidence/native'),('final',R/'artifacts/reconstruct-final/evidence/native'),('extendedfinal',R/'artifacts/reconstruct-extendedfinal/evidence/native'),('lifetimes',R/'artifacts/reconstruct-lifetimes/evidence/native'),('earlyclose',R/'artifacts/reconstruct-earlyclose/evidence/native'),('regression',R/'artifacts/reconstruct-regression/evidence/native'),('app',run)]:
    exit=read(root/'retainedexit.json');wer=read(root/'wer.json');clean=read(root/'cleanup.json')
    check('ExitAndWER_'+name,exit['exitCode']==0 and not exit['forced'] and wer['secondsAfterExit']>=31 and not wer['matches'] and clean['packagesRemaining']==0 and not clean['processRemaining'])

version='0.0.1-reconstruct0b4f.final';closure={}
for n in ['Legacy','Share']:
    dll='Danmaku.'+n+'.dll'
    package=R/'artifacts/reconstruct-final/feed'/('Danmaku.'+n+'.'+version+'.nupkg')
    with zipfile.ZipFile(package) as z: raw=z.read(next(x for x in z.namelist() if x.endswith('/'+dll)))
    expected=hashlib.sha256(raw).hexdigest()
    files=[R/'artifacts/reconstruct-final/layout'/dll,app/'layout2'/dll,app/'src/_build/x64/Release/BiliCopilot.UI/bin'/dll,Path.home()/'.nuget/packages'/('danmaku.'+n.lower())/version/'lib/net8.0-windows10.0.19041'/dll]
    # Use the actual restored TFM folder, not an assumed spelling.
    files[-1]=next((Path.home()/'.nuget/packages'/('danmaku.'+n.lower())/version/'lib').rglob(dll))
    check('PackageClosure_'+n,all(sha(p)==expected for p in files))
    loaded=read(run/'loaded-danmaku.json');check('LoadedApp_'+n,any(x['name']==dll and x['sha256'].lower()==expected for x in loaded))
    closure[n]=dict(package=str(package),packageSha256=sha(package),dllSha256=expected,files={str(p):sha(p) for p in files})
(O/'closure.json').write_text(json.dumps(closure,indent=2))
(O/'validation.json').write_text(json.dumps(dict(passed=len(checks),failed=0,skipped=0,cases=checks),indent=2))
print(f'Passed: {len(checks)}, Failed: 0, Skipped: 0')
