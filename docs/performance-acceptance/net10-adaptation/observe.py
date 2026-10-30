"""Protocol v2: owned out-of-process Windows counters; no UIA during sampling."""
import ctypes as c
from ctypes import wintypes as w
import datetime, json, os, pathlib, sys, time

k = c.WinDLL('kernel32', use_last_error=True)
p = c.WinDLL('psapi', use_last_error=True)
class Memory(c.Structure):
    _fields_ = [('cb',w.DWORD),('PageFaultCount',w.DWORD)] + [(n,c.c_size_t) for n in ('PeakWorkingSetSize','WorkingSetSize','QuotaPeakPagedPoolUsage','QuotaPagedPoolUsage','QuotaPeakNonPagedPoolUsage','QuotaNonPagedPoolUsage','PagefileUsage','PeakPagefileUsage','PrivateUsage')]
class Thread(c.Structure):
    _fields_ = [(n,w.DWORD) for n in ('dwSize','cntUsage','th32ThreadID','th32OwnerProcessID')] + [('tpBasePri',w.LONG),('tpDeltaPri',w.LONG),('dwFlags',w.DWORD)]
k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD]; k.OpenProcess.restype=w.HANDLE
k.CloseHandle.argtypes=[w.HANDLE]
k.QueryFullProcessImageNameW.argtypes=[w.HANDLE,w.DWORD,w.LPWSTR,c.POINTER(w.DWORD)]
k.GetProcessTimes.argtypes=[w.HANDLE]+[c.POINTER(c.c_ulonglong)]*4
k.GetProcessHandleCount.argtypes=[w.HANDLE,c.POINTER(w.DWORD)]
k.CreateToolhelp32Snapshot.argtypes=[w.DWORD,w.DWORD]; k.CreateToolhelp32Snapshot.restype=w.HANDLE
k.Thread32First.argtypes=[w.HANDLE,c.POINTER(Thread)]; k.Thread32Next.argtypes=k.Thread32First.argtypes
p.GetProcessMemoryInfo.argtypes=[w.HANDLE,c.POINTER(Memory),w.DWORD]

def check(value):
    if not value: raise c.WinError(c.get_last_error())

def atomic(path, value):
    temp=path.with_suffix('.tmp')
    temp.write_text(json.dumps(value),encoding='utf-8'); temp.replace(path)

def wait(path, timeout=90):
    until=time.monotonic()+timeout
    while not path.exists():
        if time.monotonic()>until: raise TimeoutError(str(path))
        time.sleep(.05)
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sample(handle, pid):
    created, exited, kernel, user = (c.c_ulonglong() for _ in range(4))
    check(k.GetProcessTimes(handle,c.byref(created),c.byref(exited),c.byref(kernel),c.byref(user)))
    if exited.value: raise RuntimeError('Target exited')
    memory=Memory(); memory.cb=c.sizeof(memory)
    check(p.GetProcessMemoryInfo(handle,c.byref(memory),memory.cb))
    handles=w.DWORD(); check(k.GetProcessHandleCount(handle,c.byref(handles)))
    snapshot=k.CreateToolhelp32Snapshot(4,0)
    if snapshot == c.c_void_p(-1).value: raise c.WinError(c.get_last_error())
    count=0
    try:
        entry=Thread(); entry.dwSize=c.sizeof(entry)
        check(k.Thread32First(snapshot,c.byref(entry)))
        while True:
            count += entry.th32OwnerProcessID == pid
            if not k.Thread32Next(snapshot,c.byref(entry)):
                if c.get_last_error()!=18: raise c.WinError(c.get_last_error())
                break
    finally: k.CloseHandle(snapshot)
    return dict(cpuMilliseconds=(kernel.value+user.value)/10000,privateBytes=memory.PrivateUsage,
                workingSet=memory.WorkingSetSize,handles=handles.value,threads=count)

def main():
    pid=int(sys.argv[1]); expected=pathlib.Path(sys.argv[2]).resolve(); state=pathlib.Path(sys.argv[3]); out=pathlib.Path(sys.argv[4])
    if pid==9608 or pid<=0: raise ValueError('Protected/invalid PID')
    handle=k.OpenProcess(0x410,False,pid); check(handle)
    try:
        name=c.create_unicode_buffer(32768); length=w.DWORD(len(name))
        check(k.QueryFullProcessImageNameW(handle,0,name,c.byref(length)))
        if pathlib.Path(name.value).resolve()!=expected: raise ValueError('Target path mismatch')
        rows=[]; overhead=[]
        atomic(out/'observer-ready.json',dict(pid=pid,observerPid=os.getpid(),observerExe=sys.executable,protocol='external-os-v2'))
        for phase in ('visible','hidden','restored'):
            marker=wait(state/f'observe-{phase}.json',150)
            if marker['pid']!=pid or marker['scale']!=1.5: raise ValueError('Wrong marker')
            start=time.perf_counter()
            for i in range(61):
                time.sleep(max(0,start+i-time.perf_counter()))
                before=time.perf_counter(); counters=sample(handle,pid)
                rows.append(dict(**marker,sample=i,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),monotonicSeconds=time.perf_counter(),**counters))
                overhead.append(time.perf_counter()-before)
            atomic(state/f'observe-{phase}-done.json',dict(rows=61))
            atomic(out/f'samples-{phase}.json',dict(protocol='external-os-v2',rows=[r for r in rows if r['phase']==phase]))
        result=dict(protocol='external-os-v2',rows=rows,observerSampleSeconds=overhead,
                    counterApis=['GetProcessTimes','GetProcessMemoryInfo','GetProcessHandleCount','Thread32First/Next'])
        atomic(state/'idle-baseline.json',result); atomic(out/'idle-baseline.json',result)
    finally: k.CloseHandle(handle)

if __name__=='__main__': main()
