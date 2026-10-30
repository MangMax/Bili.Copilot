"""Read-only replay of evidence assertions. --live additionally hashes retained actual files."""
import pathlib,json,gzip,hashlib,datetime as dt,importlib.util,sys,xml.etree.ElementTree as ET
here=pathlib.Path(__file__).parent;root=here.parents[2];e=here/'evidence';base=root/'artifacts/tray-foreground'
def read(rel):
    p=e/rel
    if not p.exists():p=p.with_name(p.name+'.gz')
    data=gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes()
    return json.loads(data.decode('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def stamp(s):return dt.datetime.fromisoformat(s.replace('Z','+00:00'))
def lines(rel):return [json.loads(x) for x in gzip.decompress((e/(rel+'.gz')).read_bytes()).decode('utf-8-sig').splitlines()]
spec=importlib.util.spec_from_file_location('gate',here.parent/'net11-exit-reproduction/exit_gate.py');gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
summary={'exits':{},'cases':{},'live': '--live' in sys.argv}
assert read('run1-exit-incomplete.json')['status']=='BLOCKED'
for run in ('run2','logged1','green1','physical-red','physical-green'):
    obs=read(run+'/exit-observation.json');ready=read(run+'/ready.json')
    assert all(obs[k]==ready[k] for k in ('pid','exe','creationFileTime'))
    assert obs['queries'] and all(q['ok'] for q in obs['queries'])
    last=(stamp(obs['lastQueryUtc'])-stamp(obs['terminationUtc'])).total_seconds()
    end=(stamp(obs['collectionEndUtc'])-stamp(obs['terminationUtc'])).total_seconds()
    assert last>=30 and obs['queries'][-1]['startedSecondsAfterExit']>=30 and end>=last
    if run=='logged1':
        logs=read(run+'/log-summary.json');assert logs['managedUnhandled']==0
        obs=dict(obs,managedUnhandled=logs['managedUnhandled'])
    else:assert obs['managedUnhandled']==0 and not obs.get('copyError')
    result=gate.verdict(obs,read(run+'/events-safe.json'));assert result['status']=='PASS_BOUNDED'
    summary['exits'][run]=dict(**result,pid=obs['pid'],lastQueryAfterExit=last,collectionEndAfterExit=end)
    assert read(run+'/competitor-exit.json')['terminated']
for run,cases in [('logged1',('Mouse','Invoke','Aumid')),('run2',('Mouse','Menu')),('green1',('Mouse','Invoke','Aumid','Menu')),('physical-red',('Mouse',)),('physical-green',('Mouse',))]:
    for name in cases:
        c=read(run+'/case-'+name+'.json');assert c['before']['visible']==False
        assert c['before']['foreground']==read(run+'/competitor-ready.json')['hwnd']
        assert c['visiblePass']
        expected=(run=='green1' or run.startswith('physical-') or name=='Menu')
        assert c['foregroundPass']==expected and c['pass']==expected
        if name!='Mouse' or run!='logged1':assert c['keyboardPass']
        if run in ('green1','physical-red','physical-green'):
            events=[x for x in lines(run+'/tray-probe.jsonl') if stamp(c['startUtc'])<=stamp(x['utc'])<=stamp(c['endUtc'])]
            keys=[x for x in events if x['kind']=='key' and x['data']['foreground']==c['restored']['mainHwnd']]
            assert len(keys)>=len('tray'+name.lower())
            assert any(x['kind']=='text' and x['data']['text']=='tray'+name.lower() and x['data']['foreground']==c['restored']['mainHwnd'] for x in events)
        summary['cases'][run+'/'+name]=dict(visible=c['visiblePass'],foreground=c['foregroundPass'],keyboard=c['keyboardPass'])
# The reused freeze script includes its just-written output manifests in this list.
# Those are derived evidence, not source inputs; compare their payloads separately above/below.
red={x['path']:x['sha256'] for x in read('red/fixture-input-files.json') if not x['path'].startswith('frozen-')}
green={x['path']:x['sha256'] for x in read('green/fixture-input-files.json') if not x['path'].startswith('frozen-')}
assert red.keys()==green.keys()
delta=[k for k in red if red[k]!=green[k]];assert delta==['App.xaml.cs'],delta
assert read('red/packages.json')==read('green/packages.json')
summary['fixtureSourceDelta']=delta;summary['packages']=len(read('green/packages.json'))
redPayload={x['path']:x['sha256'] for x in read('red/frozen-all-files.json')}
greenPayload={x['path']:x['sha256'] for x in read('green/frozen-all-files.json')}
assert redPayload.keys()==greenPayload.keys()
payloadDelta=[k for k in redPayload if redPayload[k]!=greenPayload[k]]
assert set(payloadDelta)=={'BiliCopilot.UI.dll','BiliCopilot.UI.pdb'}
summary['payloadDelta']=payloadDelta
mods=lambda run,file:{pathlib.Path(r['path']).name.lower():r for r in read(run+'/'+file)}
a=mods('run2','activation-modules.json');b=mods('green1','loaded-modules.json')
for n in ('h.notifyicon.dll','h.notifyicon.winui.dll','winuiex.dll','microsoft.ui.xaml.dll','microsoft.windowsappruntime.dll','coreclr.dll'):
    assert a[n]['sha256']==b[n]['sha256'],n
assert a['bilicopilot.ui.dll']['sha256']!=b['bilicopilot.ui.dll']['sha256']
summary['sameCriticalRuntimeModules']=6
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for name,count in [('tray',25),('suite',487)]:
    p=e/'tests'/(name+'.trx.gz');t=ET.fromstring(gzip.decompress(p.read_bytes()));c=t.find('t:ResultSummary/t:Counters',ns).attrib
    assert int(c['total'])==int(c['passed'])==count and int(c['failed'])==0
    summary[name+'Tests']=dict(passed=count,failed=0,skipped=0)
for s in read('screenshots-reviewed.json'):assert s['viewed'] and sha(e/s['path'])==s['sha256']
state=read('final-state-after-physical.json');assert not state['owned'] and state['temporaryPackages']==0
assert state['oldLayoutExists'] and state['newLayoutExists'] and state['protectedVersion']=='2.2610.0.2'
summary['yield']=read('yield-after-physical.json');summary['run1Exit']='BLOCKED: missing persisted observation'
if summary['live']:
    for run,arm in [('run1','red'),('run2','red'),('green1','green'),('physical-red','red'),('physical-green','green')]:
        layout=base/run/'layout';rows=read(arm+'/frozen-all-files.json')
        expected={r['path'] for r in rows};actual={str(p.relative_to(layout)) for p in layout.rglob('*') if p.is_file()}
        assert actual==expected
        for row in rows:assert sha(layout/row['path'])==row['sha256'],row['path']
    for rows in (read('run2/activation-modules.json'),read('logged1/activation-modules.json'),read('green1/loaded-modules.json')):
        for row in rows:assert sha(pathlib.Path(row['path']))==row['sha256'].lower()
    for row in read('production-source.json'):assert sha(root/row['path'])==row['sha256']
    for row in read('production-build-files.json'):assert sha(base/'production-build'/row['path'])==row['sha256']
    provenance=json.loads((here.parent/'full-app-net11/provenance.json').read_text(encoding='utf-8-sig'))
    layout=pathlib.Path(state['protectedRegistration']);actual={str(p.relative_to(layout)):sha(p) for p in layout.rglob('*') if p.is_file()}
    assert actual.keys()==provenance['files'].keys()
    assert all(actual[k]==v for k,v in provenance['files'].items() if k!='AppxManifest.xml')
    assert actual['AppxManifest.xml']=='ead860442444bbe3c1e34897640991cdd466787778eddacadc95741f24ad37ce'
    summary['protectedPayloadFiles']=len(actual)
print(json.dumps(summary,indent=2))
