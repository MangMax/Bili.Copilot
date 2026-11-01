from pathlib import Path
import hashlib,io,json,subprocess,zipfile
root=Path.cwd();out=root/'artifacts/fault-isolation';sha=lambda b:hashlib.sha256(b).hexdigest()
kernelref=subprocess.check_output(['git','rev-parse','ad4d17d3:externals/bili-kernel'],text=True).strip()
z=zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','-C',str(root.parent/'t3-c10467bb/artifacts/aot-dev-install/kernel.git'),'archive','--format=zip',kernelref])))
count=0
for e in z.infolist():
 if e.is_dir():continue
 assert (root/'artifacts/home-aot/source/externals/bili-kernel'/e.filename).read_bytes()==z.read(e),e.filename
 count+=1
rsp=root/'artifacts/home-aot/fixture/obj/x64/Release/net11.0-windows10.0.22621.0/win-x64/native/BiliCopilot.UI.ilc.rsp'
rows=[]
for line in rsp.read_text(encoding='utf-8-sig').splitlines():
 if not line.startswith('-r:'):continue
 dll=Path(line[3:].strip('"'))
 if dll.name not in ['Danmaku.Core.dll','Danmaku.Legacy.dll','Danmaku.Share.dll','Richasy.MpvKernel.WinUI.dll']:continue
 cache=dll.parents[2]; nupkg=next(cache.glob('*.nupkg'))
 feed=(root/'packages/danmaku') if dll.name.startswith('Danmaku.') else root/'artifacts/aot-payload-verification/local-feed'
 package=next(p for p in feed.glob('*.nupkg') if p.name.lower()==nupkg.name.lower())
 assert sha(package.read_bytes())==sha(nupkg.read_bytes())
 pkg=zipfile.ZipFile(package); member=next(p for p in pkg.namelist() if p.endswith('/'+dll.name))
 assert sha(pkg.read(member))==sha(dll.read_bytes())
 rows.append({'dll':str(dll),'dllSha256':sha(dll.read_bytes()),'package':str(package),'packageSha256':sha(package.read_bytes())})
assert len(rows)==4
(out/'dependency-audit.json').write_text(json.dumps({'kernelRef':kernelref,'kernelFilesMatched':count,'ilcPackages':rows},indent=2))
print('Kernel and four ILC package byte chains matched')
