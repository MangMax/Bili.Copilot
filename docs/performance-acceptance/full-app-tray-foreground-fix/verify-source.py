import pathlib,json,hashlib,gzip,subprocess,xml.etree.ElementTree as ET
root=pathlib.Path.cwd();here=root/'docs/performance-acceptance/full-app-tray-foreground-fix';p=json.loads((here/'provenance.json').read_text());src=pathlib.Path(p['source'])
def sha(f):
 with f.open('rb') as s:return hashlib.file_digest(s,'sha256').hexdigest()
old=root/'docs/performance-acceptance/integration-tray-foreground-fix';sources=json.loads(gzip.decompress((old/'source-hashes.json.gz').read_bytes()))
for row in sources:assert sha(root/row['path'])==row['sha256'],row['path']
actual=set()
for prefix,args in [('',[]),('externals/bili-kernel/',['-C','externals/bili-kernel'])]:
 for name in subprocess.check_output(['git',*args,'ls-files'],text=True).splitlines():
  f=root/prefix/name
  if f.is_file() and f.suffix in {'.cs','.csproj','.props','.targets','.xaml','.appxmanifest'} and (prefix or name.startswith(('src/','tests/'))):actual.add(prefix+name)
assert actual=={r['path'] for r in sources}
graph=src/'resolved-assets/src/Desktop/BiliCopilot.UI/obj/project.assets.json';assert sha(graph)==p['graph']['sha256'];g=json.loads(graph.read_text(encoding='utf-8-sig'));packages={k:v['sha512'] for k,v in g['libraries'].items() if v['type']=='package'};assert len(packages)==134
activation=src/'activation';output=src/'outputs/BiliCopilot.UI';prep=json.loads((src/'activation-preparation.json').read_text());original={str(f.relative_to(output)):sha(f) for f in output.rglob('*') if f.is_file() and f.suffix!='.appxrecipe'}
assert set(p['files'])==set(original)|{r['path'] for r in prep['addedContent']}
for k,h in original.items():assert sha(activation/k)==h
recipe=next(output.glob('*.build.appxrecipe'));packaged=set()
for node in ET.parse(recipe).getroot().iter():
 if node.tag.split('}')[-1]=='AppxPackagedFile':
  rel=next(n.text for n in node if n.tag.split('}')[-1]=='PackagePath');assert (activation/rel).is_file();packaged.add(rel.lower())
assert not {str(f.relative_to(activation)).lower() for f in activation.rglob('*.dll')}-packaged
r=dict(sourceFiles=len(sources),sourceBidirectionalPass=True,recipeClosurePass=True,packages=packages,graphSha256=sha(graph),keyHashes={n:sha(pathlib.Path(p['layout'])/n) for n in ['BiliCopilot.UI.dll','coreclr.dll','BiliCopilot.UI.runtimeconfig.json','AppxManifest.xml']})
(here/'source-graph-verification.json').write_text(json.dumps(r,indent=2));print('Source files',len(sources),'and recipe/DLL closure and 134 package graph PASS')
