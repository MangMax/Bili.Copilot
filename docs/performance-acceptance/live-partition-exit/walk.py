"""Walk the crash thread of a minidump with DbgHelp, without a live process.

Usage:
    python walk.py <dump.dmp> <out-dir> [tag] [--roots "dir1;dir2"]

Image files are matched by TimeDateStamp + SizeOfImage so stack walking works even
where the dump did not capture code pages.  Symbols come from DbgHelp plus a public
symbol server cache (set SYMBOL_CACHE to reuse a download directory).
Writes <out-dir>/<tag>-stacks.json.
"""
import contextlib, io, ctypes as c, ctypes.wintypes as w, json, pathlib, struct, sys, os

DUMP, OUT, TAG = sys.argv[1], pathlib.Path(sys.argv[2]), (sys.argv[3] if len(sys.argv) > 3 else 'dump')
roots = []
if '--roots' in sys.argv:
    roots = [pathlib.Path(p) for p in sys.argv[sys.argv.index('--roots') + 1].split(';') if p]
roots.append(pathlib.Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32')

sys.argv = [sys.argv[0], DUMP, str(OUT), TAG]
with contextlib.redirect_stdout(io.StringIO()):
    import parse_dump as p

H = c.c_void_p; Q = c.c_ulonglong; D = w.DWORD
OUT.mkdir(parents=True, exist_ok=True)

def find_image(m):
    for root in roots:
        f = root / m['name']
        if not f.exists():
            continue
        data = f.read_bytes()
        pe = struct.unpack_from('<I', data, 60)[0]
        if struct.unpack_from('<I', data, pe + 8)[0] != int(m['timestamp'], 16):
            continue
        if struct.unpack_from('<I', data, pe + 24 + 56)[0] != m['size']:
            continue
        return f, data
    return None, None

rawmem = p.mem
images = []
for m in p.mods:
    f, data = find_image(m)
    if not f:
        continue
    pe = struct.unpack_from('<I', data, 60)[0]
    ns = struct.unpack_from('<H', data, pe + 6)[0]
    opt = struct.unpack_from('<H', data, pe + 20)[0]
    sections = []
    for i in range(ns):
        off = pe + 24 + opt + i * 40
        vs, va, rs, ro = struct.unpack_from('<IIII', data, off + 8)
        sections.append((va, rs, ro))
    images.append((m, data, sections, struct.unpack_from('<I', data, pe + 24 + 60)[0], f))

def memory(a, n):
    try:
        return rawmem(a, n)
    except ValueError:
        for m, data, sections, hs, f in images:
            r = a - m['base']
            if 0 <= r and r + n <= hs:
                return data[r:r + n]
            for va, sz, off in sections:
                if va <= r and r + n <= va + sz:
                    return data[off + r - va:off + r - va + n]
        raise

p.mem = memory

class A(c.Structure): _fields_ = [('Offset', Q), ('Segment', w.WORD), ('Mode', D)]
class K(c.Structure):
    _fields_ = [('Thread', Q), ('d', D * 4), ('q', Q * 6), ('build', D), ('count', D), ('table', Q),
                ('off', D), ('sz', D), ('reserved', Q * 2)]
class F(c.Structure):
    _fields_ = [('pc', A), ('ret', A), ('frame', A), ('stack', A), ('store', A), ('ft', H), ('params', Q * 4),
                ('far', w.BOOL), ('virtual', w.BOOL), ('reserved', Q * 3), ('kd', K)]

readtype = c.WINFUNCTYPE(w.BOOL, H, Q, H, D, c.POINTER(D))
fttype = c.WINFUNCTYPE(H, H, Q)
basetype = c.WINFUNCTYPE(Q, H, Q)
missing = []
_bufs = {}

@readtype
def read(h, a, b, n, done):
    try:
        raw = p.mem(a, n); c.memmove(b, raw, n); done[0] = n; return True
    except ValueError:
        missing.append(dict(address=hex(a), size=n)); done[0] = 0; return False

@basetype
def base(h, a):
    return next((m['base'] for m in p.mods if m['base'] <= a < m['base'] + m['size']), 0)

@fttype
def table(h, a):
    b = base(h, a)
    if not b:
        return None
    try:
        pe = p.v('I', b + 60)[0]
        rva, size = p.v('II', b + pe + 24 + 112 + 24)
        lo, hi = 0, size // 12
        while lo < hi:
            mid = (lo + hi) // 2
            start, end, unwind = p.v('III', b + rva + mid * 12)
            if a - b < start: hi = mid
            elif a - b >= end: lo = mid + 1
            else:
                key = b + rva + mid * 12
                buf = c.create_string_buffer(p.mem(key, 12))
                _bufs[key] = buf
                return c.addressof(buf)
    except (ValueError, struct.error):
        pass
    return None

dbg = c.WinDLL('dbghelp.dll', use_last_error=True)
dbg.StackWalk64.argtypes = [D, H, H, c.POINTER(F), H, readtype, fttype, basetype, H]
dbg.StackWalk64.restype = w.BOOL

def walk(tid, ctxsz, ctxrva, maxframes=96):
    ctxbuf = c.create_string_buffer(p.d[ctxrva:ctxrva + ctxsz])
    f = F()
    f.pc.Offset = p.u('Q', ctxrva + 248)[0]
    f.stack.Offset = p.u('Q', ctxrva + 152)[0]
    f.frame.Offset = p.u('Q', ctxrva + 160)[0]
    for a in (f.pc, f.stack, f.frame):
        a.Mode = 3
    frames = []
    for _ in range(maxframes):
        if not dbg.StackWalk64(0x8664, H(0x12345), H(tid), c.byref(f), ctxbuf, read, table, base, None):
            break
        if not f.pc.Offset or p.loc(f.pc.Offset) == 'unmapped':
            break
        frames.append(dict(pc=hex(f.pc.Offset), location=p.loc(f.pc.Offset)))
        if len(frames) > 1 and frames[-1]['pc'] == frames[-2]['pc']:
            break
    return frames

result = dict(dump=pathlib.Path(DUMP).name, thread=p.tid, frames=walk(p.tid, p.ctxsz, p.ctx),
              missingReads=missing[:10], images=[str(i[4]) for i in images])

# --- symbols (public server; exports are enough for OS modules when no PDB exists)
SYM = c.WinDLL('dbghelp.dll', use_last_error=True)
SYM.SymSetOptions.argtypes = [D]; SYM.SymSetOptions.restype = D
SYM.SymInitializeW.argtypes = [H, w.LPCWSTR, w.BOOL]; SYM.SymInitializeW.restype = w.BOOL
SYM.SymLoadModuleExW.argtypes = [H, H, w.LPCWSTR, w.LPCWSTR, Q, D, H, D]; SYM.SymLoadModuleExW.restype = Q
SYM.SymFromAddrW.argtypes = [H, Q, c.POINTER(Q), c.c_void_p]; SYM.SymFromAddrW.restype = w.BOOL
SYM.SymSetOptions(0x2 | 0x4 | 0x10)
cache = pathlib.Path(os.environ.get('SYMBOL_CACHE', str(OUT / 'symbols')))
cache.mkdir(parents=True, exist_ok=True)
hproc = H(0x12345)
SYM.SymInitializeW(hproc, 'srv*%s*https://msdl.microsoft.com/download/symbols' % cache, False)

MAX_SYM_NAME = 2000
loaded = {}
def load_module(name):
    if name in loaded:
        return loaded[name]
    img = next((i for i in images if i[0]['name'] == name), None)
    if img is None:
        loaded[name] = False
        return False
    m, data, sections, hs, f = img
    loaded[name] = bool(SYM.SymLoadModuleExW(hproc, None, str(f), None, m['base'], m['size'], None, 0))
    return loaded[name]

def symbolize(pc):
    m = next((m for m in p.mods if m['base'] <= pc < m['base'] + m['size']), None)
    if m is None:
        return dict(module=None, symbol=None, offset=None)
    load_module(m['name'])
    buf = c.create_string_buffer(88 + MAX_SYM_NAME)
    struct.pack_into('<I', buf, 0, 88)
    struct.pack_into('<I', buf, 80, MAX_SYM_NAME)
    disp = Q(0)
    if SYM.SymFromAddrW(hproc, pc, c.byref(disp), buf):
        nlen = struct.unpack_from('<I', buf, 76)[0]
        return dict(module=m['name'], symbol=bytes(buf[84:84 + nlen * 2]).decode('utf-16le', 'replace'),
                    displacement=disp.value, offset=pc - m['base'])
    return dict(module=m['name'], symbol=None, offset=pc - m['base'])

for fr in result['frames']:
    fr.update(symbolize(int(fr['pc'], 16)))
result['symbolsLoaded'] = sum(1 for v in loaded.values() if v)
(OUT / (TAG + '-stacks.json')).write_text(json.dumps(result, indent=1))
for fr in result['frames']:
    print('%-28s %-58s +%#x' % (fr['module'], (fr['symbol'] or '?'), fr.get('displacement', 0)))
