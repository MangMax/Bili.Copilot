"""Byte-preserving portable evidence; large source/build/layout trees stay ignored."""
import gzip, json, pathlib, shutil
from prepare import BASE, HERE, ROOT, digest, save

destination=HERE/'raw'
destination.mkdir(exist_ok=False)
items=[]
def add(source,relative):
    data=source.read_bytes()
    target=destination/(relative+'.gz');target.parent.mkdir(parents=True,exist_ok=True)
    target.write_bytes(gzip.compress(data,mtime=0))
    items.append({'path':relative,'bytes':len(data),'sha256':digest(source),'archive':target.relative_to(HERE).as_posix()})
for p in sorted(BASE.rglob('*')):
    if not p.is_file():continue
    parts=p.relative_to(BASE).parts
    if any(x in parts for x in ('source','fixture','input','layout','references')):continue
    if p.suffix=='.png':
        relative=p.relative_to(BASE).as_posix();target=HERE/'images'/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
        items.append({'path':relative,'bytes':p.stat().st_size,'sha256':digest(p),'image':target.relative_to(HERE).as_posix()})
    elif p.suffix in ('.json','.jsonl','.log','.txt','.md','.csv','.ps1','.py','.stderr','.stdout','.patch'):
        add(p,p.relative_to(BASE).as_posix())
for arm in ('net10','net11'):
    area=BASE/arm
    for p in sorted(area.rglob('project.assets.json')):
        add(p,(pathlib.Path(arm)/'resolved-assets'/p.relative_to(area)).as_posix())
    names=['App.xaml.cs','GlobalDependencies.cs','Fixture.cs','IdleBaseline.cs','MainRuntimeChecks.cs','ExtraRuntimeChecks.cs','FontRuntimeChecks.cs','BiliCopilot.UI.csproj','Directory.Build.props','Directory.Packages.props','global.json','run-ui.ps1','run-main.ps1','source-provenance.txt','frozen-all-files.json','frozen-content-provenance.json']
    for name in names:
        p=area/'fixture'/name
        if p.exists():add(p,f'{arm}/fixture-inputs/{name}')
for p in sorted(HERE.glob('*')):
    if p.is_file() and p.suffix in ('.py','.ps1'):add(p,'replay-source/'+p.name)
save(HERE/'ledger.json',items)
print('Archived',len(items),'files')
