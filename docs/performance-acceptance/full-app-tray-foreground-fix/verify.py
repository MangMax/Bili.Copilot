import pathlib,json,hashlib,importlib.util,datetime,sys
here=pathlib.Path(__file__).resolve().parent;root=here.parents[2]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
p=read(here/'provenance.json');layout=pathlib.Path(p['layout']);actual={str(f.relative_to(layout)):sha(f) for f in layout.rglob('*') if f.is_file()};assert actual==p['files']
old=root.parent/'t3-4d615e72/artifacts/full-app-net11/layout-v2';assert {str(f.relative_to(old)):sha(f) for f in old.rglob('*') if f.is_file()}==p['oldFiles']
spec=importlib.util.spec_from_file_location('gate',here.parent/'net11-exit-reproduction/exit_gate.py');gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
summary={'newFiles':len(actual),'oldFiles':len(p['oldFiles']),'oldAndNewHashesPass':True,'runs':{}}
for name in sys.argv[1:]:
 folder=here/name;r=read(folder/'exit-observation.json');ready=read(folder/'ready.json');assert r['pid']==ready['pid'] and r['creationFileTime']==ready['creationFileTime'] and r['exe']==ready['exe'];assert r['managedLogScan']=='complete' and r['managedUnhandled']==0
 parse=lambda s:datetime.datetime.fromisoformat(s.replace('Z','+00:00'))
 assert (parse(r['lastQueryUtc'])-parse(r['terminationUtc'])).total_seconds()>=30 and r['queries'][-1]['startedSecondsAfterExit']>=30
 result=gate.verdict(r,read(folder/'events-safe.json'));modules=read(folder/('modules-playing.json' if (folder/'modules-playing.json').exists() else 'modules.json'))
 for m in modules:
  f=pathlib.Path(m['path']);assert layout in f.parents and sha(f)==m['sha256'].lower();assert sha(f)==actual[str(f.relative_to(layout))]
 assert any(m['name'].lower()=='coreclr.dll' and m['sha256'].lower()=='46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2' for m in modules)
 result.update(loadedModules=len(modules),lastQuerySeconds=(parse(r['lastQueryUtc'])-parse(r['terminationUtc'])).total_seconds(),pid=r['pid'],exitCode=r['exitCode'])
 (folder/'exit-verdict.json').write_text(json.dumps(result,indent=2));summary['runs'][name]=result
(here/'verification.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2));assert all(r['status']=='PASS_BOUNDED' for r in summary['runs'].values())
