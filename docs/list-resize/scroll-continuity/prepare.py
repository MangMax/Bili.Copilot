"""Restore the pinned archived host, then add the continuous compositor regression.

Requires a fresh artifacts/recycled-cards directory; never overwrites evidence.
Uses the checkout's committed production source; cache arms share one binary.
"""
import argparse, hashlib, json, subprocess, shutil, io, zipfile, uuid, xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--kernel-repo', required=True)
a=p.parse_args()
root=Path(__file__).resolve().parents[3]
host=root/'artifacts/recycled-cards/host'
source=host.parent/'source'
if host.parent.exists():raise SystemExit('Fresh artifacts/recycled-cards required; existing evidence is never replaced')
host.mkdir(parents=True);source.mkdir()
with zipfile.ZipFile(root/'docs/list-resize/mount-diagnosis-evidence.zip') as z:
 for name in z.namelist():
  if name.startswith('replay/host/') and not name.endswith('/'):
   dest=host/name.removeprefix('replay/host/');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(z.read(name))
zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','archive','--format=zip','HEAD','src'],cwd=root))).extractall(source)
kernel=source/'externals/bili-kernel';kernel.mkdir(parents=True)
zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','-C',a.kernel_repo,'archive','--format=zip','0ae5e4e000a41a07f6696746706df11c06e8835b'],cwd=root))).extractall(kernel)
(host/'Directory.Build.props').write_text('<Project><PropertyGroup><SourceRoot>'+source.as_posix()+'</SourceRoot></PropertyGroup></Project>')
manifest=host/'Package.appxmanifest'
manifest.write_text(manifest.read_text().replace('BiliCopilot.RecycleD228Mount.Owned','BiliCopilot.Recycle'+uuid.uuid4().hex[:12]+'.Owned'))
shutil.copyfile(source/'src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml',host/'Repeater.xaml')
shutil.copyfile(source/'src/Desktop/BiliCopilot.UI/Controls/Components/ArticleCardControl/PartitionArticlePresenter.xaml',host/'PartitionArticlePresenter.xaml')
policy=next(e.attrib['Value'] for e in ET.parse(host/'Repeater.xaml').iter() if e.tag.endswith('Setter') and e.attrib.get('Property','').endswith('VerticalCacheLength'))
shutil.copyfile(Path(__file__).with_name('Continuity.cs'),host/'Continuity.cs')
app=host/'App.xaml.cs'
s=app.read_text(encoding='utf-8-sig')
old='Add("Focus",()=>_ = RunFastReview())'
assert s.count(old)==1
app.write_text(s.replace(old,'Add("Focus",()=>_ = RunContinuity())'),encoding='utf-8')
# Original PNG resize trajectory, with explicit policy override surviving remount.
matrix=host/'Matrix.cs';s=matrix.read_text(encoding='utf-8-sig')
s=s.replace('var cacheControl=File.ReadAllText(Path.Combine(output,"related-family.txt")).Trim()=="Cache20";', 'var requestedCache=double.Parse(File.ReadAllText(Path.Combine(output,"mount-cache.txt")),System.Globalization.CultureInfo.InvariantCulture);')
s=s.replace('if(cacheControl)RepeaterCachePolicy.SetVerticalCacheLength(owner,20);','')
s=s.replace('var commandCalls=0;','RepeaterCachePolicy.SetVerticalCacheLength(owner,requestedCache);\n                var commandCalls=0;')
s=s.replace('(green&&!cacheControl?0.5:20)','requestedCache')
matrix.write_text(s,encoding='utf-8')
resize=host/'ResizeChecks.cs';s=resize.read_text(encoding='utf-8-sig')
s=s.replace('caches.Any(x=>x!=0.5)','caches.Any(x=>x!='+policy+')')
resize.write_text(s,encoding='utf-8')
fast=host/'FastReview.cs';s=fast.read_text(encoding='utf-8-sig')
s=s.replace('double reviewCache=.5','double reviewCache='+policy)
s=s.replace('if(ir.VerticalCacheLength!=.5)','if(ir.VerticalCacheLength!='+policy+')')
fast.write_text(s,encoding='utf-8')
subprocess.run(['python',str(Path(__file__).with_name('realistic.py'))],check=True,cwd=root)
(host/'continuity-inputs.json').write_text(json.dumps({str(f.relative_to(host)):hashlib.sha256(f.read_bytes()).hexdigest() for f in host.iterdir() if f.is_file()},indent=2))
