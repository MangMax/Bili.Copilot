"""Bounded native debugger for ONLY this checkout's synthetic layout.

Full dumps stay ignored. All exceptions except the attach breakpoint are passed
to the application. Attaching changes timing and native reporting behavior.
"""
import ctypes as c
from ctypes import wintypes as w
import datetime
import json
import pathlib
import sys
import time

k = c.WinDLL('kernel32', use_last_error=True)
dbg = c.WinDLL('dbghelp', use_last_error=True)
class Record(c.Structure):
    _fields_ = [('code', w.DWORD), ('flags', w.DWORD), ('record', c.c_void_p),
                ('address', c.c_void_p), ('count', w.DWORD), ('info', c.c_ulonglong * 15)]
class ExceptionEvent(c.Structure):
    _fields_ = [('record', Record), ('first', w.DWORD)]
class Union(c.Union):
    _fields_ = [('exception', ExceptionEvent), ('raw', c.c_byte * 160)]
class Event(c.Structure):
    _fields_ = [('code', w.DWORD), ('pid', w.DWORD), ('tid', w.DWORD), ('u', Union)]
class Pointers(c.Structure):
    _fields_ = [('record', c.c_void_p), ('context', c.c_void_p)]
class DumpException(c.Structure):
    _pack_ = 4
    _fields_ = [('tid', w.DWORD), ('pointers', c.POINTER(Pointers)), ('client', w.BOOL)]
k.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]; k.OpenProcess.restype = w.HANDLE
k.OpenThread.argtypes = [w.DWORD, w.BOOL, w.DWORD]; k.OpenThread.restype = w.HANDLE
k.QueryFullProcessImageNameW.argtypes = [w.HANDLE, w.DWORD, w.LPWSTR, c.POINTER(w.DWORD)]
k.CloseHandle.argtypes = [w.HANDLE]
k.GetThreadContext.argtypes = [w.HANDLE, c.c_void_p]
k.WaitForDebugEvent.argtypes = [c.POINTER(Event), w.DWORD]
k.ContinueDebugEvent.argtypes = [w.DWORD, w.DWORD, w.DWORD]
k.CreateFileW.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, c.c_void_p, w.DWORD, w.DWORD, w.HANDLE]
k.CreateFileW.restype = w.HANDLE
dbg.MiniDumpWriteDump.argtypes = [w.HANDLE, w.DWORD, w.HANDLE, w.DWORD, c.c_void_p, c.c_void_p, c.c_void_p]

def check(value):
    if not value:
        raise c.WinError(c.get_last_error())

pid = int(sys.argv[1]); output = pathlib.Path(sys.argv[2]).resolve()
allowed = pathlib.Path.cwd() / 'artifacts/exit-reproduction'
assert pid != 9608 and output.is_relative_to(allowed)
handle = k.OpenProcess(0x1FFFFF, False, pid); check(handle)
buffer = c.create_unicode_buffer(32768); length = w.DWORD(len(buffer))
check(k.QueryFullProcessImageNameW(handle, 0, buffer, c.byref(length)))
assert pathlib.Path(buffer.value).resolve() == output / 'layout/BiliCopilot.UI.exe'
attached = False
try:
    check(k.DebugActiveProcess(pid)); attached = True
    check(k.DebugSetProcessKillOnExit(False))
    (output / 'collector-ready.json').write_text(json.dumps(dict(pid=pid, collectorPid=__import__('os').getpid(), exe=buffer.value)))
    deadline = time.monotonic() + 600
    first_break = True
    dumped = False
    while time.monotonic() < deadline:
        ev = Event()
        if not k.WaitForDebugEvent(c.byref(ev), 500):
            if c.get_last_error() == 121:
                continue
            raise c.WinError(c.get_last_error())
        continuation = 0x10002
        try:
            if ev.code == 1:
                rec = ev.u.exception.record
                continuation = 0x80010001
                if rec.code == 0x80000003 and first_break:
                    first_break = False; continuation = 0x10002
                if rec.code == 0xC000027B and not dumped:
                    thread = k.OpenThread(0x0008, False, ev.tid); check(thread)
                    storage = c.create_string_buffer(1248)
                    address = (c.addressof(storage) + 15) & ~15
                    c.c_uint32.from_address(address + 48).value = 0x10001F
                    check(k.GetThreadContext(thread, address))
                    pointers = Pointers(c.addressof(rec), address)
                    info = DumpException(ev.tid, c.pointer(pointers), False)
                    file = k.CreateFileW(str(output / 'crash-full.dmp'), 0x40000000, 0, None, 1, 0x80, None)
                    check(file != c.c_void_p(-1).value)
                    try:
                        check(dbg.MiniDumpWriteDump(handle, pid, file, 0x1826, c.byref(info), None, None))
                    finally:
                        k.CloseHandle(file); k.CloseHandle(thread)
                    dumped = True
                    (output / 'collector-capture.json').write_text(json.dumps(dict(pid=pid, tid=ev.tid,
                        code=hex(rec.code), address=rec.address, parameters=list(rec.info)[:rec.count],
                        firstChance=bool(ev.u.exception.first), utc=datetime.datetime.now(datetime.timezone.utc).isoformat())))
            elif ev.code == 5:
                break
            # Close debugger-delivered image/thread handles; process ownership is separate.
            elif ev.code == 3:
                for offset in (0, 8, 16):
                    h = c.c_void_p.from_address(c.addressof(ev.u) + offset).value
                    if h: k.CloseHandle(h)
            elif ev.code in (2, 6):
                h = c.c_void_p.from_address(c.addressof(ev.u)).value
                if h: k.CloseHandle(h)
        finally:
            check(k.ContinueDebugEvent(ev.pid, ev.tid, continuation))
finally:
    if attached: k.DebugActiveProcessStop(pid)
    k.CloseHandle(handle)
    (output / 'collector-stopped.json').write_text(json.dumps(dict(pid=pid, utc=datetime.datetime.now(datetime.timezone.utc).isoformat())))
