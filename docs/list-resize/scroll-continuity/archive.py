"""Archive measured scroll frames and receipts, not binaries or unrelated app data."""
from pathlib import Path
import hashlib, json, zipfile, subprocess
root=Path(__file__).resolve().parents[3]
base=root/'artifacts/recycled-cards'
files={}
def add(p,name):
 if p.is_file():files[name]=p
add(root/'docs/list-resize/scroll-continuity-results.md','REPORT.md')
for name in ('Controls/Components/ArticleCardControl/PartitionArticlePresenter.xaml','Styles/Repeater.xaml'):
 add(root/'src/Desktop/BiliCopilot.UI'/name,'final-production/'+name)
for stage in base.iterdir():
 if not stage.is_dir() or not (stage/'identity.json').exists():continue
 for p in stage.iterdir():
  if p.is_file() and p.suffix in ('.json','.txt','.png','.cs','.jsonl'):
   add(p,f'runs/{stage.name}/{p.name}')
 for p in (stage/'sources').glob('*'):add(p,f'runs/{stage.name}/sources/{p.name}')
 if (stage/'video-pixels.json').exists():
  rows=json.loads((stage/'video-pixels.json').read_text(encoding='utf-8-sig'))
  selected=set()
  for count in {r['count'] for r in rows}:
   v=[r for r in rows if r['count']==count];runs=[];start=None
   for i,r in enumerate(v):
    if r['background']>.9 and start is None:start=i
    if start is not None and (r['background']<=.9 or i==len(v)-1):
     runs.append((v[i]['ms']-v[start]['ms'],start,i));start=None
   if runs:_,a,b=max(runs)
   else:a=b=max(range(len(v)),key=lambda i:v[i]['background'])
   selected.update(r['file'] for r in v if v[a]['ms']-150<=r['ms']<=v[b]['ms']+150)
   selected.update(r['file'] for r in (v[0],v[-1],min(v,key=lambda r:r['background'])))
  for name in selected:add(stage/'continuous.frames'/name,f'runs/{stage.name}/continuous.frames/{name}')
  for p in (stage/'pixel-grids').glob('*.rgb'):add(p,f'runs/{stage.name}/pixel-grids/{p.name}')
  for name in ('frames.ndjson','manifest.json'):
   add(stage/'continuous.frames'/name,f'runs/{stage.name}/continuous.frames/{name}')
for p in (root/'artifacts/scroll-continuity').glob('*'):
 if p.is_file():add(p,'receipts/'+p.name)
for name in ('head.txt','status.txt'):
 add(root/'artifacts/scroll-continuity/kernel-initial'/name,'kernel-initial/'+name)
for p in Path(__file__).parent.glob('*'):
 if p.is_file():add(p,'replay/'+p.name)
# Archive every harness source; fixed assets are restored from the checked-in mount archive.
for p in (base/'host').iterdir():
 if p.is_file():add(p,'host/'+p.name)
manifest={name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for name,p in files.items()}
dest=root/'docs/list-resize/scroll-continuity-evidence.zip'
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED) as z:
 for name,p in sorted(files.items()):z.write(p,name)
 z.writestr('manifest.json',json.dumps(manifest,indent=2))
 z.writestr('ARCHIVE-SCOPE.txt','Full recordings and full JPEG frames remain under artifacts/recycled-cards. For EVERY sampled scrolling frame this archive preserves the exact row-major RGB pixel grid used by the classifier (stride 10, crop coordinates and original JPEG SHA256 in video-pixels.json), permitting independent recomputation of ALL blank durations without video decoding. JPEGs are a declared subset: each count\'s longest blank interval with 150ms margins, first/last frame and least-background frame. Full recording index/manifest preserves timing. Warmup, resize and idle-only frames and MP4 files are omitted. Assets come from docs/list-resize/mount-diagnosis-evidence.zip. Earlier half-viewport attempts and their raw RenderTargetBitmap frames are retained, not substituted for the external compositor recordings. No missing historical Temp evidence is claimed freshly verified.\n')
with zipfile.ZipFile(dest) as z:
 for name,m in manifest.items():
  data=z.read(name)
  assert len(data)==m['bytes'] and hashlib.sha256(data).hexdigest()==m['sha256'],name
print(dest, dest.stat().st_size,len(manifest),'verified payloads')
