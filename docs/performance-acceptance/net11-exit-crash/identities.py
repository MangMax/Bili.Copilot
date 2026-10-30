import pathlib,struct,json,hashlib,uuid
root=pathlib.Path('artifacts/exit-diagnosis');mods=json.loads((root/'parsed.json').read_text())['modules'];stack=json.loads((root/'stack.json').read_text());results=[]
base=pathlib.Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-66927834/artifacts/net11-primary/f6ed3ec063df8701bb57115abb40c4f7bcffc386/player-freeze/activation')
for rec in stack['images']+[dict(name='BiliCopilot.UI.dll')]:
 m=next(x for x in mods if x['name']==rec['name'])
 for parent in [base,pathlib.Path('C:/Windows/System32'),pathlib.Path('C:/Program Files/WindowsApps/Microsoft.WindowsAppRuntime.2_2.6.4.0_x64__8wekyb3d8bbwe')]:
  path=parent/m['name']
  if not path.exists():continue
  d=path.read_bytes();u=lambda f,o:struct.unpack_from('<'+f,d,o);pe=u('I',60)[0];ns=u('H',pe+6)[0];opt=u('H',pe+20)[0]
  sections=[u('IIII',pe+24+opt+i*40+8) for i in range(ns)]
  def off(r):
   for vs,va,rs,ro in sections:
    if va<=r<va+rs:return ro+r-va
   raise ValueError('RVA missing')
  magic=u('H',pe+24)[0];dd=pe+24+(112 if magic==0x20b else 96);r,z=u('II',dd+6*8);cv=None
  for i in range(z//28):
   at=off(r)+i*28
   if u('I',at+12)[0]==2:
    sz,_,raw=u('III',at+16)
    if d[raw:raw+4]==b'RSDS':cv=dict(guid=uuid.UUID(bytes_le=d[raw+4:raw+20]).hex.upper(),age=u('I',raw+20)[0])
  if cv and cv['guid']==m.get('guid') and cv['age']==m.get('age'):
   out=dict(name=m['name'],version=m['version'],sha256=hashlib.sha256(d).hexdigest(),pdb=m['pdb'],**cv,match=True)
   if m['name']=='BiliCopilot.UI.dll':
    pdb=path.with_suffix('.pdb');pd=pdb.read_bytes();ver=struct.unpack_from('<I',pd,12)[0];at=(16+ver+3)&~3;n=struct.unpack_from('<H',pd,at+2)[0];at+=4
    for i in range(n):
     so,sz=struct.unpack_from('<II',pd,at);end=pd.index(b'\0',at+8);name=pd[at+8:end];at=(end+4)&~3
     if name==b'#Pdb':out.update(portablePdbSha256=hashlib.sha256(pd).hexdigest(),portablePdbGuid=uuid.UUID(bytes_le=pd[so:so+16]).hex.upper())
   results.append(out);break
 else:results.append(dict(name=m['name'],match=False))
(root/'identities.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
