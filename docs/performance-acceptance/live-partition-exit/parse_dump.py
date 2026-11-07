"""Parse a WER minidump (MDMP) and report the 0xc000027b stowed-exception surface.

Usage:
    python parse_dump.py <dump.dmp> <out-dir> [tag]

Writes <out-dir>/<tag>-parse.json.  No dependency outside the standard library.
Stowed records live on the heap, so a WER LocalDumps minidump (MemoryListStream
only) normally does not contain them; the script reports that as an evidence gap
instead of failing.
"""
import pathlib, struct, hashlib, json, uuid, bisect, datetime, sys

DUMP = sys.argv[1]
OUT = pathlib.Path(sys.argv[2])
TAG = sys.argv[3] if len(sys.argv) > 3 else pathlib.Path(DUMP).stem
OUT.mkdir(parents=True, exist_ok=True)

d = pathlib.Path(DUMP).read_bytes()
sha = hashlib.sha256(d).hexdigest()
def u(fmt, o): return struct.unpack_from('<' + fmt, d, o)
assert d[:4] == b'MDMP', 'not a minidump'
n, dr = u('II', 8)
streams = {k: (s, r) for k, s, r in (u('III', dr + i * 12) for i in range(n))}

maps = []
if 5 in streams:                      # MemoryListStream
    r = streams[5][1]
    for i in range(u('I', r)[0]):
        a, s, o = u('QII', r + 4 + i * 16)
        maps.append((a, s, o))
if 9 in streams:                      # Memory64ListStream
    r = streams[9][1]
    cnt, base = u('QQ', r)
    o = base
    for i in range(cnt):
        a, s = u('QQ', r + 16 + i * 16)
        maps.append((a, s, o)); o += s
maps.sort()
bases = [x[0] for x in maps]

def mem(a, size):
    parts = []; left = size; cur = a
    start = max(0, bisect.bisect_right(bases, a) - 1)
    for b, z, o in maps[start:]:
        if b > cur: break
        if b <= cur < b + z:
            take = min(left, b + z - cur)
            parts.append(d[o + cur - b:o + cur - b + take]); cur += take; left -= take
            if left == 0: return b''.join(parts)
    raise ValueError('not captured %#x remaining %d of %d' % (cur, left, size))

def v(fmt, a): return struct.unpack('<' + fmt, mem(a, struct.calcsize('<' + fmt)))

mods = []
r = streams[4][1]
for i in range(u('I', r)[0]):
    o = r + 4 + i * 108
    b, s, c, t, na = u('QIIII', o)
    ln = u('I', na)[0]
    name = pathlib.PureWindowsPath(d[na + 4:na + 4 + ln].decode('utf-16le')).name
    ms, ls = u('II', o + 32)
    cvsz, cv = u('II', o + 76)
    m = dict(name=name, base=b, size=s, timestamp=hex(t),
             version='%d.%d.%d.%d' % (ms >> 16, ms & 65535, ls >> 16, ls & 65535))
    if cvsz >= 24 and d[cv:cv + 4] == b'RSDS':
        m.update(pdb=pathlib.PureWindowsPath(d[cv + 24:cv + cvsz].split(b'\0')[0].decode(errors='replace')).name,
                 guid=uuid.UUID(bytes_le=d[cv + 4:cv + 20]).hex.upper(), age=u('I', cv + 20)[0])
    mods.append(m)

def loc(a):
    for m in mods:
        if m['base'] <= a < m['base'] + m['size']:
            return m['name'] + '+%#x' % (a - m['base'])
    return 'unmapped'

r = streams[6][1]
tid = u('I', r)[0]
code, flags = u('II', r + 8)
addr = u('Q', r + 24)[0]
num = u('I', r + 32)[0]
params = u('Q' * min(num, 15), r + 40)
ctxsz, ctx = u('II', r + 160)

names = {}
if 24 in streams:
    r2 = streams[24][1]
    for i in range(u('I', r2)[0]):
        t, saddr = u('IQ', r2 + 4 + i * 12)
        sl = u('I', saddr)[0]
        names[t] = d[saddr + 4:saddr + 4 + sl].decode('utf-16le')

threads = []
r = streams[3][1]
for i in range(u('I', r)[0]):
    o = r + 4 + i * 48
    tid2, susp, pc, pri = u('IIII', o)
    teb = u('Q', o + 16)[0]
    st, sl, so = u('QII', o + 24)
    tsz, to = u('II', o + 40)
    threads.append(dict(tid=tid2, ctx_size=tsz, ctx_rva=to, stack_start=st, stack_size=sl, name=names.get(tid2)))

out = dict(
    dump=pathlib.Path(DUMP).name, sha256=sha, bytes=len(d),
    pid=u('I', streams[15][1] + 8)[0] if 15 in streams else None,
    timestampUtc=datetime.datetime.fromtimestamp(u('I', 20)[0], datetime.timezone.utc).isoformat(),
    flags=hex(u('Q', 24)[0]), streamIds=sorted(streams),
    exception=dict(thread=tid, code=hex(code), flags=hex(flags), address=hex(addr), location=loc(addr),
                   parameters=[hex(x) for x in params], count=num),
    threadsTotal=len(threads), threadsNamed=len(names),
    memoryRanges=len(maps), memorySpan=[hex(maps[0][0]), hex(maps[-1][0] + maps[-1][1])],
    moduleCount=len(mods), stowed=[])

try:
    arr = params[0]; cnt = params[1]
    entries = v('Q' * cnt, arr)
    for a in entries:
        rec = dict(address=hex(a))
        try:
            size, sig, hr, form = v('IIII', a)
            rec.update(size=size, signature=hex(sig), hresult=hex(hr), form=form & 3, thread=form & ~3)
            if size >= 56:
                typ, nest = v('I4xQ', a + 40)
                rec.update(nestedType=hex(typ), nestedAddress=hex(nest),
                           nestedCaptured=any(b <= nest < b + z for b, z, _ in maps))
            if form & 3 == 1:
                ex, ws, words, trace = v('QIIQ', a + 16)
                rec.update(exception=loc(ex), exceptionAddress=hex(ex), wordSize=ws, words=words)
                if ws == 8:
                    rec['stack'] = [dict(address=hex(q), location=loc(q)) for q in v('Q' * min(words, 128), trace)]
        except ValueError as e:
            rec['missing'] = str(e)
        out['stowed'].append(rec)
except ValueError as e:
    out['stowedError'] = 'stowed array not captured: ' + str(e)

out['modules'] = mods
(OUT / (TAG + '-parse.json')).write_text(json.dumps(out, indent=1))
print(json.dumps({k: x for k, x in out.items() if k != 'modules'}, indent=1)[:4000])
threads_snapshot = threads
