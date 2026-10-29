import struct,json,pathlib,hashlib,zipfile
import sys
p=pathlib.Path(sys.argv[1])
b=p.read_bytes()
def u(fmt,o): return struct.unpack_from('<'+fmt,b,o)
assert b[:4]==b'MDMP'
n,dr=u('II',8); streams={}
for i in range(n):
 t,sz,rva=u('III',dr+i*12); streams[t]=(sz,rva)
ranges=[]
if 5 in streams:
 r=streams[5][1]
 for i in range(u('I',r)[0]):
  va,sz,off=u('QII',r+4+i*16);ranges.append((va,sz,off))
if 9 in streams:
 r=streams[9][1]; count,off=u('QQ',r)
 for i in range(count):
  va,sz=u('QQ',r+16+i*16);ranges.append((va,sz,off));off+=sz
# Thread stack ranges supplement memory list.
if 3 in streams:
 r=streams[3][1]
 for i in range(u('I',r)[0]):
  va,sz,off=u('QII',r+4+i*48+24);ranges.append((va,sz,off))
def read(va,sz):
 for a,s,o in ranges:
  if a<=va and va+sz<=a+s:return b[o+va-a:o+va-a+sz]
 raise ValueError('unmapped '+hex(va))
def v(fmt,va):return struct.unpack('<'+fmt,read(va,struct.calcsize('<'+fmt)))
mods=[]
r=streams[4][1]
for i in range(u('I',r)[0]):
 o=r+4+i*108;base,size,checksum,stamp,name=u('QIIII',o)
 length=u('I',name)[0];path=b[name+4:name+4+length].decode('utf-16le')
 mods.append((base,size,path))
def addr(a):
 for base,size,path in mods:
  if base<=a<base+size:return pathlib.PureWindowsPath(path).name+'+'+hex(a-base)
 return hex(a)
r=streams[6][1];tid=u('I',r)[0];code,flags,rec,ea,np=u('IIQQI',r+8); params=u('15Q',r+40)[:np]
out={'dump':str(p),'sha256':hashlib.sha256(b).hexdigest(),'bytes':len(b),'streams':{str(k):list(val) for k,val in streams.items()},'exception':{'thread':tid,'code':hex(code),'address':addr(ea),'parameters':[hex(x) for x in params]},'stowed':[]}
if code==0xc000027b and len(params)>=2:
 for i in range(min(params[1],32)):
  try:
   ptr=v('Q',params[0]+i*8)[0];sz,sig,hr,bits=v('IIII',ptr)
   e={'address':hex(ptr),'size':sz,'signature':hex(sig),'hresult':hex(hr),'form':bits&3,'thread':bits&~3}
   if bits&3==1:
    ea,ws,cnt,st=v('QIIQ',ptr+16);e.update(exceptionAddress=addr(ea),stackWordSize=ws,stackCount=cnt)
    try:e['stack']=[addr(x) for x in v(('Q' if ws==8 else 'I')*min(cnt,128),st)]
    except ValueError as ex:e['stackError']=str(ex)
   elif bits&3==2:
    ep=v('Q',ptr+16)[0];data=bytearray()
    for j in range(4096):
     pair=read(ep+j*2,2)
     if pair==b'\0\0':break
     data.extend(pair)
    e['errorText']=data.decode('utf-16le')
   if sz>=56:
    nt=v('I',ptr+40)[0];nested=v('Q',ptr+48)[0];e.update(nestedType=hex(nt),nested=hex(nested))
   out['stowed'].append(e)
  except ValueError as ex:out['stowed'].append({'error':str(ex)})
out['engineModules']=[{'base':hex(base),'size':size,'path':path} for base,size,path in mods if 'Danmaku.' in path or path.endswith('Harness.exe')]
print(json.dumps(out,indent=2))
