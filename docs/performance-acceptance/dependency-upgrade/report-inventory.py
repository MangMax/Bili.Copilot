"""Render captured metadata and actual selected manifest versions, without querying again."""
import datetime,gzip,json,pathlib,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[3]
inventory=root/'artifacts/dependency-upgrade/inventory.json'
raw=inventory.read_bytes() if inventory.exists() else gzip.decompress((pathlib.Path(__file__).parent/'evidence/compressed/inventory.json.gz').read_bytes())
data=json.loads(raw.decode('utf-8-sig'))
out=pathlib.Path(__file__).parent
result=[]
baseline=root/'artifacts/dependency-upgrade/baseline-graphs/app-assets.json'
baseline_versions={}
if baseline.exists():
    baseline_versions={k.split('/')[0]:k.split('/')[1] for k in json.loads(baseline.read_text(encoding='utf-8-sig'))['libraries']}
elif (out/'evidence/baseline-app-versions.json').exists():
    baseline_versions=json.loads((out/'evidence/baseline-app-versions.json').read_text())
current_refs={}
for area in ['src','tests','externals/bili-kernel/src']:
    refs={}
    for p in (root/area).rglob('*'):
        if p.suffix not in ('.csproj','.props') or {'bin','obj','_build'}.intersection(p.parts):continue
        for node in ET.parse(p).iter('PackageReference'):
            refs.setdefault(node.get('Include'),[]).append(str(p.relative_to(root)))
    current_refs[area]=refs
for row in data['rows']:
    meta=data['metadata'][row['id']]
    nodes=ET.parse(root/row['file']).iter(row['kind'])
    selected=next(n.get('Version') for n in nodes if n.get('Include')==row['id'])
    checked=datetime.datetime.fromisoformat(meta['checkedUtc'].replace('Z','+00:00')).astimezone(datetime.timezone.utc).isoformat()
    refs=current_refs[row['area']].get(row['id'],[])
    scope='unused-central' if not refs else 'kernel-sample-only' if all('/Samples/' in p.replace('\\','/') for p in refs) else 'app-and-tests'
    result.append({**row,'references':refs,'scope':scope,'oldResolvedApp':baseline_versions.get(row['id']),'selected':selected,'latestStable':meta.get('latestStable'),'latestAvailable':meta.get('latestAvailable'),'source':meta['source'],'checkedUtc':checked,'metadataError':meta.get('error')})
(out/'versions.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
lines=['# Exact dependency inventory','', 'Latest means stable if available; preview-only packages retain their channel. Unreferenced central declarations are inventoried separately and are not runtime upgrades. See dependency-upgrade.md for attempted-version blockers.','']
for scope,title in [('app-and-tests','App and test referenced declarations'),('kernel-sample-only','Kernel sample only (outside app closure)'),('unused-central','Central declarations with no direct reference')]:
    lines+=['## '+title,'','| Area / package | Old declaration / app resolved | Latest stable / available | Selected | Checked UTC |','|---|---|---|---|---|']
    for r in result:
        if r['scope']!=scope:continue
        latest=str(r['latestStable'] or r['latestAvailable'] or 'unresolved')
        lines.append(f"| {r['area']} / [{r['id']}]({r['source']}) | {r['old']} / {r['oldResolvedApp'] or 'not in app graph'} | {latest} | {r['selected']} | {r['checkedUtc']} |")
(out/'versions.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
