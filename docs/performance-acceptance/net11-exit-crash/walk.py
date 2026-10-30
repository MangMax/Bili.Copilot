import contextlib,io,ctypes as c,ctypes.wintypes as w
with contextlib.redirect_stdout(io.StringIO()):import parse as p
H=c.c_void_p;Q=c.c_ulonglong;D=w.DWORD
dbg=c.WinDLL('C:/Windows/System32/dbghelp.dll',use_last_error=True);handle=H(0x12345);symbol=p.loc
import json,pathlib
rawmem=p.mem;images=[];imageLedger=[]
root=p.P.parent.parent/'player-freeze'/'activation'
for m in p.mods:
 if m['name'].lower() not in {'kernelbase.dll','ntdll.dll','coremessagingxp.dll','combase.dll','microsoft.ui.xaml.dll','coreclr.dll','user32.dll','kernel32.dll'}:continue
 for path in (root/m['name'],pathlib.Path('C:/Windows/System32')/m['name'],pathlib.Path('C:/Program Files/WindowsApps/Microsoft.WindowsAppRuntime.2_2.6.4.0_x64__8wekyb3d8bbwe')/m['name']):
  if not path.exists():continue
  data=path.read_bytes();up=lambda f,o:p.struct.unpack_from('<'+f,data,o)
  pe=up('I',60)[0];stamp=up('I',pe+8)[0];size=up('I',pe+24+56)[0]
  if stamp!=int(m['timestamp'],16) or size!=m['size']:continue
  ns=up('H',pe+6)[0];opt=up('H',pe+20)[0];sections=[]
  for i in range(ns):
   off=pe+24+opt+i*40;vs,va,rs,ro=up('IIII',off+8);sections.append((va,rs,ro))
  images.append((m,data,sections,up('I',pe+24+60)[0]))
  imageLedger.append(dict(name=m['name'],sha256=p.hashlib.sha256(data).hexdigest(),timestamp=m['timestamp'],size=size))
  break
def memory(a,n):
 try:return rawmem(a,n)
 except ValueError:
  for m,data,sections,hs in images:
   r=a-m['base']
   if 0<=r and r+n<=hs:return data[r:r+n]
   for va,sz,off in sections:
    if va<=r and r+n<=va+sz:return data[off+r-va:off+r-va+n]
  raise
p.mem=memory
class A(c.Structure):_fields_=[('Offset',Q),('Segment',w.WORD),('Mode',D)]
class K(c.Structure):_fields_=[('Thread',Q),('d',D*4),('q',Q*6),('build',D),('count',D),('table',Q),('off',D),('sz',D),('reserved',Q*2)]
class F(c.Structure):_fields_=[('pc',A),('ret',A),('frame',A),('stack',A),('store',A),('ft',H),('params',Q*4),('far',w.BOOL),('virtual',w.BOOL),('reserved',Q*3),('kd',K)]
readtype=c.WINFUNCTYPE(w.BOOL,H,Q,H,D,c.POINTER(D));fttype=c.WINFUNCTYPE(H,H,Q);basetype=c.WINFUNCTYPE(Q,H,Q)
missing=[];buffers={}
@readtype
def read(h,a,b,n,done):
 try:raw=p.mem(a,n);c.memmove(b,raw,n);done[0]=n;return True
 except ValueError:missing.append(dict(address=hex(a),size=n));done[0]=0;return False
@basetype
def base(h,a):
 return next((m['base'] for m in p.mods if m['base']<=a<m['base']+m['size']),0)
@fttype
def table(h,a):
 b=base(h,a)
 if not b:return None
 try:
  pe=p.v('I',b+60)[0];rva,size=p.v('II',b+pe+24+112+24)
  # Binary search PE exception directory, no stack scanning.
  lo=0;hi=size//12
  while lo<hi:
   mid=(lo+hi)//2;start,end,unwind=p.v('III',b+rva+mid*12)
   if a-b<start:hi=mid
   elif a-b>=end:lo=mid+1
   else:
    key=b+rva+mid*12
    if key not in buffers:buffers[key]=c.create_string_buffer(p.mem(key,12))
    return c.addressof(buffers[key])
 except ValueError:pass
 return None
dbg.StackWalk64.argtypes=[D,H,H,c.POINTER(F),H,readtype,fttype,basetype,H];dbg.StackWalk64.restype=w.BOOL
ctx=c.create_string_buffer(p.d[p.ctx:p.ctx+p.ctxsz]);f=F();f.pc.Offset=p.u('Q',p.ctx+248)[0];f.stack.Offset=p.u('Q',p.ctx+152)[0];f.frame.Offset=p.u('Q',p.ctx+160)[0]
for a in (f.pc,f.stack,f.frame):a.Mode=3
frames=[]
for i in range(64):
 ok=dbg.StackWalk64(0x8664,handle,H(p.tid),c.byref(f),ctx,read,table,base,None)
 if not ok or not f.pc.Offset:break
 if p.loc(f.pc.Offset)=='unmapped-code-address':break
 frames.append(dict(pc=hex(f.pc.Offset),module=p.loc(f.pc.Offset),symbol=symbol(f.pc.Offset)))
 if len(frames)>1 and frames[-1]['pc']==frames[-2]['pc']:break
leo=p.out['stowed'][0];obj=int(leo['nestedAddress'],16)
try:
 vt=p.v('Q',obj)[0];leoType=dict(vtable=p.loc(vt),methods=[symbol(q) for q in p.v('QQQ',vt)])
except ValueError as e:leoType=dict(missing=str(e))
out=dict(method='DbgHelp StackWalk64; dump memory plus timestamp/size-matched PE fallback; no stack scan; stops before unmapped PC',frames=frames,missingReads=missing[:12],languageObject=leoType,images=imageLedger,structSizes=[c.sizeof(A),c.sizeof(K),c.sizeof(F)])
pathlib.Path('artifacts/exit-diagnosis/stack.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
