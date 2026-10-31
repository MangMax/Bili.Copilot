"""Independent raw evidence audit. Does not change acceptance thresholds."""
from pathlib import Path
import json, hashlib, math, subprocess, xml.etree.ElementTree as E
r=Path(__file__).resolve().parents[2]; a=r/'artifacts/list-resize'; out=r/'artifacts/list-review'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
report={'candidate':'1797c94aa7c6fc54226bb4ea8f0efef90a49f5bd','baseline':'90e215f0','stages':[]}
source_index={}
for p in a.rglob('*'):
    if p.is_file() and p.suffix in ('.cs','.xaml','.csproj'):
        source_index.setdefault(digest(p),[]).append(p.relative_to(a).as_posix())
for stage in ('red-settled','red-extra','green-verified','green-extra-navigation'):
    d=a/stage; rows=read(d/'matrix-results.json'); checks=read(d/'matrix-checks.json')
    timings=[]
    for row in rows:
        for raw,maximum,p95 in [('beats','heartbeatMax','heartbeatP95'),('resize','resizeMax','resizeP95'),('scrollBeats','scrollHeartbeatMax','scrollHeartbeatP95')]:
            if raw not in row:
                assert raw=='scrollBeats' and stage.startswith('red'),(stage,raw)
                continue
            values=sorted(row[raw]); assert values
            assert math.isclose(max(values),row[maximum],abs_tol=1e-8)
            assert math.isclose(values[int((len(values)-1)*.95)],row[p95],abs_tol=1e-8)
        assert len(row['resize'])==120 and len(row['scrolling'])==27
        assert row['peakRealized']>=row['realized'] and all(0<=s['realized']<=row['count'] for s in row['scrolling'])
        timings.append({k:row.get(k) for k in ('family','count','realized','peakRealized','draws','scrollDraws','heartbeatMax','resizeP95','scrollHeartbeatMax')})
    source_count=0; recovered=[]; missing=[]
    for entry in read(d/'source-hashes.json'):
        name=entry['Path'].replace('\\','/').split('/')[-1]
        if (d/'sources'/name).exists():
            assert digest(d/'sources'/name)==entry['Hash'].lower(),(stage,name)
            source_count+=1
        elif entry['Hash'].lower() in source_index:
            recovered.append({'name':name,'matchingArchivedSources':source_index[entry['Hash'].lower()]})
        else: missing.append(name)
    samples=read(d/'process-samples.json')
    report['stages'].append({'stage':stage,'sourceHashesVerified':source_count,'sourceRecoveredByHash':recovered,'sourceMissing':missing,'failures':[s for s in checks if s.endswith('/FAIL')],
      'rows':timings,'identity':read(d/'identity.json'),'sampleUtcRange':[samples[0]['utc'],samples[-1]['utc']], 'exit':read(d/'exit.json'),'wer':read(d/'wer.json')})
ns='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';xs='{http://schemas.microsoft.com/winfx/2006/xaml}'
owners=[]; styles=[]
for p in (r/'src/Desktop/BiliCopilot.UI').rglob('*.xaml'):
    for e in E.parse(p).iter():
        if e.tag.endswith('}AppRepeater'): owners.append({'path':p.relative_to(r).as_posix(),'style':e.get('Style'),'inlineStyle':any(c.tag.endswith('.Style') for c in e)})
        if e.tag==ns+'Style' and 'AppRepeater' in e.get('TargetType',''): styles.append({'path':p.relative_to(r).as_posix(),'key':e.get(xs+'Key'),'basedOn':e.get('BasedOn')})
report['styleInventory']={'owners':owners,'styles':styles}
report['pixelChecks']=[]
from PIL import Image
for kind in ('False','True'):
    d=a/'green-extra-navigation'; first=Image.open(d/f'matrix-{kind}-first.png').convert('RGBA'); same=Image.open(d/f'matrix-{kind}-same.png').convert('RGBA'); replacement=Image.open(d/f'matrix-{kind}-replacement.png').convert('RGBA')
    assert first.tobytes()==same.tobytes() and first.tobytes()!=replacement.tobytes()
    report['pixelChecks'].append({'blur':kind=='True','size':first.size,'samePixels':True,'replacementChangesPixels':True,'uniqueColors':len(first.getcolors(first.width*first.height))})
for stage in ('baseline-two-pane','candidate-two-pane'):
    d=out/stage
    if (d/'exit.json').exists():
        exit=read(d/'exit.json'); assert exit['absent'] and exit['observedSeconds']>=31
        report[stage]={'checks':read(d/'matrix-focus.json'),'trace':(d/'trace.jsonl').read_text(encoding='utf-8-sig'),'exit':exit,'wer':read(d/'wer.json')}
        binaries=[]
        for name in ('Harness.dll','Richasy.WinUIKernel.Share.dll','Microsoft.WinUI.dll'):
            p=d/'layout'/name
            if p.exists(): binaries.append({'file':name,'sha256':digest(p)})
        report[stage]['binaries']=binaries
if (out/'baseline-two-pane/sources').exists() and (out/'candidate-two-pane/sources').exists():
    b=out/'baseline-two-pane/sources';c=out/'candidate-two-pane/sources'
    report['pairSourceDifferences']=[p.name for p in b.iterdir() if p.is_file() and digest(p)!=digest(c/p.name)]
image_stage=out/'candidate-image-checks'
if (image_stage/'exit.json').exists():
    checks=read(image_stage/'matrix-cover-checks.json'); assert len(checks)==4 and all(c['pass'] for c in checks)
    exit=read(image_stage/'exit.json'); assert exit['absent'] and exit['observedSeconds']>=31
    assert read(image_stage/'wer.json')==[]
    report['freshImageChecks']={'checks':checks,'exit':exit,'wer':[]}
package=Path('C:/Users/hp/.nuget/packages/richasy.winuikernel.share/0.0.12-preview4')
dll=package/'lib/net9.0-windows10.0.22621/Richasy.WinUIKernel.Share.dll'
report['dependency']={'package':'Richasy.WinUIKernel.Share/0.0.12-preview4','dllSha256':digest(dll),'nuspecSha256':digest(package/'richasy.winuikernel.share.nuspec'),'upstreamCommit':'3f1086f6cfa8c1e0c3a64b930a8577dc5496c616'}
for stage in ('baseline-two-pane','candidate-two-pane','candidate-image-checks','baseline-user50','candidate-user50'):
    p=out/stage/'layout/Richasy.WinUIKernel.Share.dll'
    if p.exists(): assert digest(p)==digest(dll)
report['focusedUser50']=[]
for stage in ('baseline-user50','candidate-user50'):
    d=out/stage
    if not (d/'exit.json').exists():continue
    rows=read(d/'matrix-results.json');assert len(rows)==1 and rows[0]['family']=='User' and rows[0]['count']==50
    row=rows[0];exit=read(d/'exit.json');assert exit['absent'] and exit['observedSeconds']>=31 and read(d/'wer.json')==[]
    for raw,maximum,p95 in [('beats','heartbeatMax','heartbeatP95'),('resize','resizeMax','resizeP95'),('scrollBeats','scrollHeartbeatMax','scrollHeartbeatP95')]:
        v=sorted(row[raw]); assert max(v)==row[maximum] and v[int((len(v)-1)*.95)]==row[p95]
    report['focusedUser50'].append({'stage':stage,'row':row,'failures':[x for x in read(d/'matrix-checks.json') if x.endswith('/FAIL')],'exit':exit})
    if stage=='candidate-user50':assert not report['focusedUser50'][-1]['failures']
if len(report['focusedUser50'])==2:
    b=out/'baseline-user50/sources';c=out/'candidate-user50/sources'
    report['user50SourceDifferences']=[p.name for p in b.iterdir() if p.is_file() and digest(p)!=digest(c/p.name)]
    assert sorted(report['user50SourceDifferences'])==['BasicCoverImage.cs','BlurCoverImage.cs','FocusMode.cs']
(out/'audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Raw available heartbeat/resize/scroll arrays and retained source hashes verified; 40 AppRepeater sites inventoried; decoded PNG pixel equality verified.')
print('Sources:',[(s['stage'],s['sourceHashesVerified'],len(s['sourceRecoveredByHash']),s['sourceMissing']) for s in report['stages']])
print('Residual >=200ms:',[(v['family'],v['count'],v['heartbeatMax']) for s in report['stages'][2:] for v in s['rows'] if v['heartbeatMax']>=200])
print('Pair differences:',report.get('pairSourceDifferences'))
