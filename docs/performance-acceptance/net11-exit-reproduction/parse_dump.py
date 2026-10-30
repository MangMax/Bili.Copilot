import pathlib,struct,hashlib,json,uuid,sys,bisect,datetime
import mmap
root=pathlib.Path('artifacts/exit-reproduction/original1')
P=root/'crash-full.dmp'
f=P.open('rb'); d=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ)

def u(fmt,o): return struct.unpack_from('<'+fmt,d,o)
assert d[:4]==b'MDMP'
n,dr=u('II',8); streams={k:(s,r) for k,s,r in (u('III',dr+i*12) for i in range(n))}
maps=[]
if 5 in streams:
 r=streams[5][1]
 for i in range(u('I',r)[0]):
  a,s,o=u('QII',r+4+i*16);maps.append((a,s,o))
if 9 in streams:
 r=streams[9][1];n,o=u('QQ',r)
 for i in range(n):
  a,s=u('QQ',r+16+i*16);maps.append((a,s,o));o+=s
if 3 in streams:
 r=streams[3][1]
 for i in range(u('I',r)[0]):
  a,s,o=u('QII',r+4+i*48+24)
  if s and (a,s,o) not in maps:maps.append((a,s,o))
maps.sort()
bases=[x[0] for x in maps]
def mem(a,s):
 parts=[];left=s;cur=a
 start=max(0,bisect.bisect_right(bases,a)-1)
 for b,z,o in maps[start:]:
  if b>cur:break
  if b<=cur<b+z:
   take=min(left,b+z-cur);parts.append(d[o+cur-b:o+cur-b+take]);cur+=take;left-=take
   if left==0:return b''.join(parts)
 raise ValueError('not captured '+hex(cur)+' remaining '+str(left)+' of '+str(s))
def v(fmt,a):return struct.unpack('<'+fmt,mem(a,struct.calcsize('<'+fmt)))
mods=[];r=streams[4][1]
for i in range(u('I',r)[0]):
 o=r+4+i*108;b,s,c,t,na=u('QIIII',o);ln=u('I',na)[0];name=pathlib.PureWindowsPath(d[na+4:na+4+ln].decode('utf-16le')).name
 ms,ls=u('II',o+32);cvsz,cv=u('II',o+76)
 m=dict(name=name,base=b,size=s,timestamp=hex(t),version=f'{ms>>16}.{ms&65535}.{ls>>16}.{ls&65535}')
 if cvsz>=24 and d[cv:cv+4]==b'RSDS':
  m.update(pdb=pathlib.PureWindowsPath(d[cv+24:cv+cvsz].split(b'\0')[0].decode(errors='replace')).name,guid=uuid.UUID(bytes_le=d[cv+4:cv+20]).hex.upper(),age=u('I',cv+20)[0])
 mods.append(m)
def loc(a):
 for m in mods:
  if m['base']<=a<m['base']+m['size']:return m['name']+'+'+hex(a-m['base'])
 return 'unmapped-code-address'
r=streams[6][1];tid=u('I',r)[0];code=u('I',r+8)[0];addr=u('Q',r+24)[0];num=u('I',r+32)[0];params=u('Q'*num,r+40)
out=dict(sha256=hashlib.sha256(d).hexdigest(),bytes=len(d),streams=streams,flags=hex(u('Q',24)[0]),thread=tid,code=hex(code),exceptionAt=loc(addr),parameters=[hex(x) for x in params],memoryRanges=len(maps),stowed=[])
out['pid']=u('I',streams[15][1]+8)[0]
out['timestampUtc']=datetime.datetime.fromtimestamp(u('I',20)[0],datetime.timezone.utc).isoformat()
assert out['pid']==38208 and u('H',streams[7][1])[0]==9
assert tid==39912 and code==0xc000027b
for i in range(min(params[1],16)):
 try:
  a=v('Q',params[0]+8*i)[0];size,sig,hr,form=v('IIII',a)
  x=dict(size=size,signature=hex(sig),hresult=hex(hr),form=form&3,thread=form&~3)
  if size>=56:
   typ,nest=v('I4xQ',a+40);x.update(nestedType=hex(typ),nestedCaptured=any(b<=nest<b+z for b,z,_ in maps),nestedAddress=hex(nest))
  if form&3==1:
   ex,ws,words,trace=v('QIIQ',a+16);x.update(exception=loc(ex),wordSize=ws,words=words)
   x['stack']=[dict(address=hex(q),location=loc(q)) for q in v('Q'*min(words,256),trace)] if ws==8 else []
  out['stowed'].append(x)
 except ValueError as e:
  x['missing']=str(e);out['stowed'].append(x)
ctxsz,ctx=u('II',r+160);out['context']=dict(size=ctxsz,rip=hex(u('Q',ctx+248)[0]),rsp=hex(u('Q',ctx+152)[0]))
out['modules']=mods
(root/'parsed-dump.json').write_text(json.dumps(out,indent=2))
print(json.dumps({k:val for k,val in out.items() if k!='modules'},indent=2))
