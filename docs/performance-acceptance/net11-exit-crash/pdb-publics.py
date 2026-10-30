import struct,pathlib,json,uuid,bisect
root=pathlib.Path('artifacts/exit-diagnosis');mods=json.loads((root/'parsed.json').read_text())['modules'];stack=json.loads((root/'stack.json').read_text());out=[]
def parse(path,m):
 data=path.read_bytes();u=lambda f,o:struct.unpack_from('<'+f,data,o)
 bs,_,blocks,ds,_,blockmap=u('IIIIII',32)
 assert len(data)==blocks*bs, 'Incomplete PDB, reject'
 n=(ds+bs-1)//bs;inds=u('I'*n,blockmap*bs);directory=b''.join(data[i*bs:(i+1)*bs] for i in inds)[:ds]
 ns=struct.unpack_from('<I',directory)[0];sizes=struct.unpack_from('<'+'I'*ns,directory,4);pos=4+4*ns;streams=[]
 for sz in sizes:
  n=0 if sz==0xffffffff else (sz+bs-1)//bs
  ids=struct.unpack_from('<'+'I'*n,directory,pos);pos+=4*n;streams.append(b''.join(data[i*bs:(i+1)*bs] for i in ids)[:sz])
 info=streams[1];infoage=struct.unpack_from('<I',info,8)[0];guid=uuid.UUID(bytes_le=info[12:28]).hex.upper();age=struct.unpack_from('<I',streams[3],8)[0];assert guid==m['guid'] and age==m['age'], f'PDB identity mismatch actual {guid}/{age} (info age {infoage}), expected {m["guid"]}/{m["age"]}'
 dbi=streams[3];symstream=struct.unpack_from('<H',dbi,20)[0]
 # DBI optional debug header points to PE section headers (slot 5).
 lengths=struct.unpack_from('<iiiii',dbi,24);ec=struct.unpack_from('<i',dbi,52)[0];opt=64+sum(lengths)+ec
 sh=struct.unpack_from('<H',dbi,opt+10)[0];sections=streams[sh];records=streams[symstream];publics=[];pos=0
 while pos+4<=len(records):
  sz,kind=struct.unpack_from('<HH',records,pos)
  if sz<2:break
  if kind==0x110e:
   flags,offset,seg=struct.unpack_from('<IIH',records,pos+4)
   if seg and seg*40<=len(sections):
    va=struct.unpack_from('<I',sections,(seg-1)*40+12)[0];name=records[pos+14:pos+2+sz].split(b'\0')[0].decode(errors='replace');publics.append((va+offset,name))
  pos+=sz+2
 publics.sort();rows=[]
 for f in stack['frames']:
  if f['module'].split('+')[0]!=m['name']:continue
  rva=int(f['pc'],16)-m['base'];idx=bisect.bisect_right(publics,(rva,chr(0x10ffff)))-1
  if idx>=0:
   off,name=publics[idx];rows.append(dict(module=f['module'],nearestPublic=name,displacement=hex(rva-off)))
 return dict(name=m['name'],pdb=path.name,guid=guid,age=age,infoAge=infoage,sha256=__import__('hashlib').sha256(data).hexdigest(),publicCount=len(publics),frames=rows)
for name in ['ntdll.dll','CoreMessagingXP.dll']:
 m=next(x for x in mods if x['name']==name)
 try:out.append(parse(root/'symbols'/m['pdb'],m))
 except Exception as e:out.append(dict(name=name,error=str(e)))
(root/'publics.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
