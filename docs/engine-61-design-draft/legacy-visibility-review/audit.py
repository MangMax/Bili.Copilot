"""Read-only audit of original PNG pixels, fresh run, packages and preserved red evidence."""
from pathlib import Path
import hashlib,json,sys,zipfile
D=Path(__file__).resolve().parent
sys.path.insert(0,str(D/'source'))
from png_pixels import glyph
sha=lambda b:hashlib.sha256(b).hexdigest()
def read(z,n):return json.loads(z.read(n).decode('utf-8-sig'))
def manifest(z,d):
    m=json.loads((d/'manifest.json').read_text())
    for n,v in m.items():
        b=z.read(n);assert len(b)==v['bytes'] and sha(b)==v['sha256'],n
    return len(m)
P=D.parent/'legacy-early-close/independent-review'
with zipfile.ZipFile(D/'evidence.zip') as z,zipfile.ZipFile(P/'evidence.zip') as old:
    count=manifest(z,D);prior_count=manifest(old,P)
    e='full/runtime/'
    assert old.read(e+'screen-remount1-stable.png')==old.read(e+'screen-remount2-stable.png')
    oldr=read(old,e+'results.json')
    assert oldr['passed']==11 and oldr['failed']==2 and not oldr['acceptancePassed']
    ref=glyph(old.read(e+'screen-remount1-stable.png'))
    assert sha(ref)=='497428bf56e905ceab6926eadd25e517fb40c26f3eddd69f53fd2a9c31ca6306'
    assert sum(v>20 for v in ref)==5577 and ref!=bytes(len(ref))
    # Independently recompute all raw pixels, rather than trusting reported visual verdicts.
    for source,report in [(old,'prior-pixels-v2/pixels.json'),(z,'pixels/pixels.json')]:
        for rec in read(z,report)['records']:
            b=source.read(e+rec['file']);g=glyph(b)
            assert sha(b).upper()==rec['fileSha256']
            assert sha(g).upper()==rec['glyphSha256'] and sum(v>20 for v in g)==rec['nonzero']
            assert (g==ref)==rec['matchesTop20']
    for n in ['screen-remount2-stable.png','screen-remount3-stable.png','static-0.png','static-1.png','static-2.png']:
        assert glyph(old.read(e+n))==ref
    identity=read(z,e+'identity.json');masters=[]
    for i in range(1,4):
        round_masters=[]
        for suffix in ['','-stable','-static1','-static2']:
            n=e+f'screen-remount{i}{suffix}'
            assert glyph(z.read(n+'.png'))==ref
            s=read(z,n+'.json')
            for moment in [s['before'],s]:
                st=moment['state'];v=moment['visual']
                assert st['state']=='Paused' and st['position']==20
                assert v['paused'] and not v['stopped'] and v['time']==20000
                assert any(row['text']=='TOP 20' and row['drawable'] for row in v['rows'])
                round_masters.append(v['master'])
            assert s['utc']>s['before']['utc']
            assert any(w['processId']==identity['pid'] and w['isForeground'] and w['title']=='Offline full player 224' for w in s['windowsAfter'])
        assert len(set(round_masters))==1;masters.append(round_masters[0])
        assert glyph(z.read(e+f'screen-unloaded{i}.png'))==bytes(len(ref))
        assert not read(z,e+f'screen-unloaded{i}.json')['visual'].get('renderer',False)
    assert len(set(masters))==3
    for n,pos in [('forward1',21),('backward1',20)]:
        stem=e+'screen-button-'+n
        st=read(z,stem+'.json')['state'];assert st['position']==pos and st['state']=='Paused'
        g=glyph(z.read(stem+'.png'));assert g==glyph(old.read(stem+'.png')) and (g==ref)==(pos==20)
    cmds=read(z,e+'commands-screen-matrix.json');assert all(c['exit']==0 for c in cmds)
    invokes=[c['cmd'][3] for c in cmds if c['cmd'][2]=='invoke']
    assert invokes.count('Seek20')==1 and invokes.count('UnloadDanmaku')==3 and invokes.count('LoadDanmaku')==3
    assert not any(n in invokes for n in ['Redraw','PauseResume','Repeated'])
    assert sum(c['cmd'][2]=='screenshot' for c in cmds)==18
    events=[json.loads(line) for line in z.read('full/events.jsonl').decode('utf-8-sig').splitlines()]
    closed=next(x for x in events if x['data']['kind']=='closed')
    assert closed['data']['disposed'] and closed['data']['remaining']==0
    assert not any(x['at']>closed['at'] and x['data']['kind'] in ['seek-request','redraw-request'] for x in events)
    assert not any(x['data']['kind'].endswith('-error') or x['data']['kind']=='unhandled' for x in events)
    ex=read(z,e+'retainedexit.json');w=read(z,e+'wer.json')
    assert ex['pid']==identity['pid'] and ex['path']==identity['path'] and ex['retained'] and not ex['forced'] and ex['exitCode']==0
    assert w['applicationLogReadable'] and w['secondsAfterExit']>=31 and not w['matches']
    assert read(z,e+'cleanup.json')==dict(packagesRemaining=0,processRemaining=False) and read(z,e+'yield.json')['exit']==0
    r=read(z,e+'results.json');assert (r['passed'],r['failed'],r['skipped'])==(22,0,0) and all(c['passed'] for c in r['checks'])
    v=read(z,'validation.json');assert v['unchangedProductionFiles']==901 and v['kernelPreserved']
    closure=read(z,'source-closure.json');assert len(closure)==903
    assert sum(x['archiveNormalizedSha256']==x['actualNormalizedSha256'] for x in closure.values())==901
    for name,p in v['packages'].items():
        b=z.read('clean/feed/'+p['package']);assert sha(b)==p['sha256']
        import io
        with zipfile.ZipFile(io.BytesIO(b)) as pkg:
            dll=pkg.read(next(n for n in pkg.namelist() if n.endswith('/Danmaku.'+name+'.dll')))
            assert sha(dll)==p['dllSha256']
    norm=lambda b:b.removeprefix(b'\xef\xbb\xbf').replace(b'\r\n',b'\n')
    for n,folder in [('DanmakuRender.cs','legacy-early-close'),('DanmakuFrostMaster.cs','legacy-ready')]:
        assert norm(z.read('clean/source/src/Danmaku.Legacy/'+n))==norm((D.parent/folder/'source'/n).read_bytes())
print(f'PASS: {count} new + {prior_count} original files; 30 decoded PNGs; 22/0 new checks; original 11/2 retained and visual contradiction proven.')
