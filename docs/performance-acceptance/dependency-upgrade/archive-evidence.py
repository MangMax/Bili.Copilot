"""Archive raw results, compressed logs/TRX/metadata and hashes; binaries stay local."""
import gzip,hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).resolve().parents[3]
source=root/'artifacts/dependency-upgrade'
dest=pathlib.Path(__file__).parent/'evidence'
dest.mkdir(exist_ok=True)
hashes=[]
for path in sorted(source.rglob('*')):
    if not path.is_file() or 'sdk11' in path.parts or path.name.endswith('.zip'):continue
    rel=path.relative_to(source)
    if path.suffix in ('.log','.trx','.json','.jsonl','.txt','.patch','.png','.xml'):
        hashes.append(dict(path=str(rel),bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    if path.suffix in ('.log','.trx','.jsonl') or path.name.endswith('-closure.json') or path.name in ('inventory.json','dotnet11-releases.json','stage1-tools-candidate.patch','net11-candidate.patch','base2-recipe.xml'):
        target=dest/'compressed'/str(rel.with_suffix(rel.suffix+'.gz'))
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
    if path.suffix=='.json' and path.stat().st_size<90000 and path.name not in ('inventory.json',) and 'graphs' not in str(rel):
        target=dest/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
graphs={}
for path in [root/'src/Desktop/BiliCopilot.UI/obj/project.assets.json',root/'tests/BiliCopilot.UI.Tests/obj/project.assets.json']:
    if path.exists():
        data=json.loads(path.read_text(encoding='utf-8-sig'))
        graphs[str(path.relative_to(root))]=dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),libraries={key:value.get('sha512') for key,value in data['libraries'].items()},frameworks=data['project']['frameworks'])
(dest/'package-graphs.json').write_text(json.dumps(graphs,indent=2),encoding='utf-8')
(dest/'artifact-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
