"""Read-only payload verification and exact-process exit gate using safe event fields."""
import datetime, hashlib, importlib.util, json, pathlib, sys
here=pathlib.Path(__file__).resolve().parent
root=here.parents[2]
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
provenance=read(here/'provenance.json')
layout=root/'artifacts/full-app-net11/layout-v2'
actual={str(p.relative_to(layout)):sha(p) for p in layout.rglob('*') if p.is_file()}
assert actual.keys()==provenance['files'].keys()
assert all(actual[k]==v for k,v in provenance['files'].items() if k!='AppxManifest.xml')
registration=read(here/'registration-after.json')
assert pathlib.Path(registration['InstallLocation'])==layout
assert registration['Publisher']=='CN=BiliCopilotDev'
assert registration['PackageFamilyName']=='Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg'
spec=importlib.util.spec_from_file_location('gate',here.parent/'net11-exit-reproduction/exit_gate.py')
gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
summary={'payloadFiles':len(actual),'payloadHashEqualExceptAcceptanceManifest':True,'deployedManifestSha256':actual['AppxManifest.xml'],'runs':{}}
for name in sys.argv[1:]:
    folder=here/name
    run=read(folder/'exit-observation.json')
    ready=read(folder/'ready.json')
    assert run['managedLogScan']=='complete' and run['managedUnhandled']==0
    assert run['pid']==ready['pid'] and run['creationFileTime']==ready['creationFileTime']
    assert pathlib.Path(run['exe'])==layout/'BiliCopilot.UI.exe'
    parse=lambda value: datetime.datetime.fromisoformat(value.replace('Z','+00:00'))
    assert (parse(run['collectionEndUtc'])-parse(run['terminationUtc'])).total_seconds()>=30
    result=gate.verdict(run,read(folder/'events-safe.json'))
    modules=read(folder/'modules-playing.json') if (folder/'modules-playing.json').exists() else read(folder/'modules.json')
    for row in modules:
        assert pathlib.Path(row['path']).parent==layout or layout in pathlib.Path(row['path']).parents
        assert sha(pathlib.Path(row['path']))==row['sha256'].lower()
    assert any(r['name'].lower()=='coreclr.dll' and r['sha256'].lower()=='46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2' for r in modules)
    result['loadedPackageModules']=len(modules)
    (folder/'exit-verdict.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    summary['runs'][name]=result
(here/'verification.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
assert all(r['status']=='PASS_BOUNDED' for r in summary['runs'].values())
