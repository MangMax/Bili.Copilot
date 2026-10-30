"""Archive small raw results and hashes; retain large logs/TRX/assets locally."""
import hashlib,json,pathlib,shutil,subprocess
root=pathlib.Path(__file__).resolve().parents[3]
source=root/'artifacts/dependency-upgrade'
dest=pathlib.Path(__file__).parent/'evidence'
dest.mkdir(exist_ok=True)
hashes=[]
for path in sorted(source.rglob('*')):
    if not path.is_file() or 'sdk11' in path.parts or path.name.endswith('.zip'):continue
    rel=path.relative_to(source)
    if path.suffix in ('.log','.trx','.json','.txt','.patch'):
        hashes.append(dict(path=str(rel),bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    if path.suffix=='.json' and path.stat().st_size<90000 and path.name not in ('inventory.json',) and 'graphs' not in str(rel):
        target=dest/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
graphs={}
for path in [root/'src/Desktop/BiliCopilot.UI/obj/project.assets.json',root/'tests/BiliCopilot.UI.Tests/obj/project.assets.json']:
    if path.exists():
        data=json.loads(path.read_text(encoding='utf-8-sig'))
        graphs[str(path.relative_to(root))]=dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),libraries={key:value.get('sha512') for key,value in data['libraries'].items()},frameworks=data['project']['frameworks'])
(dest/'package-graphs.json').write_text(json.dumps(graphs,indent=2),encoding='utf-8')
(dest/'artifact-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
