"""Recompute closure, source exclusions, behavior and strict bounded exit evidence."""
import pathlib, json, hashlib, gzip, sys, importlib.util, subprocess
root=pathlib.Path.cwd();base=root/'artifacts/refresh-undo';dest=pathlib.Path(__file__).parent
run=sys.argv[1]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.file_digest(p.open('rb'),'sha256').hexdigest()
def compact(name,value):
    with gzip.open(dest/name,'wt',encoding='utf-8') as f:json.dump(value,f,ensure_ascii=False)
fixture=base/'fixture'; frozen=fixture/'frozen-output'
ledger=read(fixture/'frozen-all-files.json')
assert {x['path'] for x in ledger}=={str(p.relative_to(frozen)) for p in frozen.rglob('*') if p.is_file()}
for x in ledger:assert sha(frozen/x['path'])==x['sha256'] and (frozen/x['path']).stat().st_size==x['size']
differences=[];source=[]
for name in subprocess.check_output(['git','ls-files','src/Desktop/BiliCopilot.UI'],text=True).splitlines():
    p=root/name;rel=p.relative_to(root/'src/Desktop/BiliCopilot.UI');other=fixture/rel
    source.append(dict(path=name,sha256=sha(p),fixture=sha(other)))
    if sha(p)!=sha(other):differences.append(str(rel))
assert set(differences)=={'App.xaml.cs','GlobalDependencies.cs','BiliCopilot.UI.csproj','Package.appxmanifest','Package.Dev.appxmanifest'}, differences
assert sha(fixture/'ControlledRefresh.cs')==sha(dest/'ControlledRefresh.cs')
spec=importlib.util.spec_from_file_location('exit_gate',root/'docs/performance-acceptance/net11-exit-reproduction/exit_gate.py');gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
summary={}
for name in [f'run{i}' for i in range(1,int(run[3:])+1)]:
    folder=base/name;obs=read(folder/'exit-observation.json');events=[gate.event(x) for x in read(folder/'event-xml.json')]
    verdict=gate.verdict(obs,events)
    final=obs['queries'][-1]
    strict=final['startedSecondsAfterExit']>=30 and gate.timestamp(obs['lastQueryUtc'])>=gate.timestamp(obs['terminationUtc'])+30
    summary[name]=dict(pid=obs['pid'],exitCode=obs['exitCode'],verdict=verdict,strictLastQuery=strict,lastQueryUtc=obs['lastQueryUtc'],collectionEndUtc=obs['collectionEndUtc'],finalQueryStartSeconds=final['startedSecondsAfterExit'])
    if name==run:
        assert verdict['status']=='PASS_BOUNDED' and strict
        results=read(folder/'controlled-results.json');assert len(results)==12 and all(x['status']=='PASS' for x in results),results
        summary[name]['results']=results
    # Only safe, task-owned external event fields are exported; never raw XML/dumps.
    safe=[]
    for e in events:
        safe.append({**e,'data':{k:v for k,v in e['data'].items() if k in ('AppPath','ProcessId','ProcessCreationTime','IntegratorReportId','ReportId','P1','ExceptionCode','ModuleName')}})
    target=dest/name;target.mkdir(exist_ok=True)
    (target/'events-safe.json').write_text(json.dumps(safe,indent=2),encoding='utf-8')
    import shutil
    for file in ('controlled-results.json','exit-observation.json','exit-verdict.json','cleanup.json','cleanup-followup.json','failure.json','yield.json','launch.json','actions.jsonl','loaded-modules.json'):
        if (folder/file).exists():shutil.copyfile(folder/file,target/file)
    for file in folder.glob('*.png'):shutil.copyfile(file,target/file.name)
    if (folder/'controlled.jsonl').exists():
        with gzip.open(target/'controlled.jsonl.gz','wt',encoding='utf-8') as f:f.write((folder/'controlled.jsonl').read_text(encoding='utf-8-sig'))
    history=base/('history-'+name)
    if history.exists():
        compact(name+'/frozen-files.json.gz',read(history/'frozen-all-files.json'))
        compact(name+'/fixture-input-files.json.gz',read(history/'fixture-input-files.json'))
        with gzip.open(target/'ControlledRefresh.cs.gz','wb') as f:f.write((history/'ControlledRefresh.cs').read_bytes())
for module in read(base/run/'loaded-modules.json'):
    assert sha(pathlib.Path(module['path'])).upper()==module['sha256']
assets=read(fixture/'obj/project.assets.json')
packages={k:v.get('sha512') for k,v in assets['libraries'].items() if v['type']=='package'}
compact('frozen-files.json.gz',ledger);compact('source-hashes.json.gz',source);compact('fixture-input-files.json.gz',read(base/'fixture-input-files.json'))
(dest/'package-closure.json').write_text(json.dumps(packages,indent=2))
for x in ledger:
    if x['path']=='AppxManifest.xml':continue
    assert sha(base/run/'layout'/x['path'])==x['sha256']
assert {x['path'] for x in ledger}=={str(p.relative_to(base/run/'layout')) for p in (base/run/'layout').rglob('*') if p.is_file()}
import xml.etree.ElementTree as ET
manifestA=ET.parse(frozen/'AppxManifest.xml').getroot();manifestB=ET.parse(base/run/'layout/AppxManifest.xml').getroot()
for node in (manifestA,manifestB):node.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity').set('Name','OWNED-IDENTITY')
assert ET.tostring(manifestA)==ET.tostring(manifestB),'Manifest changed beyond owned identity Name'
summary.update(sourceHead=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),sourceDifferences=differences,
               frozenFiles=len(ledger),packages=len(packages),uiDll=sha(frozen/'BiliCopilot.UI.dll'),runtime=read(frozen/'BiliCopilot.UI.runtimeconfig.json'),
               kernel=subprocess.check_output(['git','-C','externals/bili-kernel','rev-parse','HEAD'],text=True).strip())
for log in base.glob('build*.log'):
    with gzip.open(dest/(log.name+'.gz'),'wb') as f:f.write(log.read_bytes())
(dest/'verification.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
