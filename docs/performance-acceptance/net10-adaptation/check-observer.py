"""Owned self-process probe. Wall overhead is diagnostic, not application CPU."""
import ctypes, importlib.util, json, os, pathlib, statistics, time
here=pathlib.Path(__file__).parent
modules=[]
for name in ('observe','observe-fast'):
    spec=importlib.util.spec_from_file_location(name,here/(name+'.py'))
    module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    modules.append((name,module))
rows=[]
for repetition in range(20):
    for name,module in modules:
        handle=module.k.OpenProcess(0x410,False,os.getpid()); module.check(handle)
        try:
            before=time.perf_counter(); counters=module.sample(handle,os.getpid())
            elapsed=time.perf_counter()-before
            assert counters['threads']>0 and counters['handles']>0 and counters['privateBytes']>0
            rows.append(dict(repetition=repetition,observer=name,wallMs=elapsed*1000,counters=counters))
        finally: module.k.CloseHandle(handle)
result=dict(processEntryBytes=ctypes.sizeof(modules[1][1].Process),rows=rows,
    medians={name:statistics.median(r['wallMs'] for r in rows if r['observer']==name) for name,_ in modules})
(here/'observer-probe.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result['medians']))
