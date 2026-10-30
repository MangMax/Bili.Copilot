"""Render captured metadata and actual selected manifest versions, without querying again."""
import datetime,json,pathlib,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[3]
data=json.loads((root/'artifacts/dependency-upgrade/inventory.json').read_text(encoding='utf-8-sig'))
out=pathlib.Path(__file__).parent
result=[]
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
    result.append({**row,'references':current_refs[row['area']].get(row['id'],[]),'selected':selected,'latestStable':meta.get('latestStable'),'latestAvailable':meta.get('latestAvailable'),'source':meta['source'],'checkedUtc':checked,'metadataError':meta.get('error')})
(out/'versions.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
lines=['# Exact dependency inventory','', 'Latest means stable if available; preview-only packages retain their channel. Unreferenced central declarations are inventoried separately and are not runtime upgrades. See dependency-upgrade.md for attempted-version blockers.','']
for used in (True,False):
    lines+=['## '+('Referenced declarations' if used else 'Central declarations with no direct reference'),'','| Area / package | Old | Latest stable / available | Selected | Checked UTC |','|---|---|---|---|---|']
    for r in result:
        if bool(r['references'])!=used:continue
        latest=str(r['latestStable'] or r['latestAvailable'] or 'unresolved')
        lines.append(f"| {r['area']} / [{r['id']}]({r['source']}) | {r['old']} | {latest} | {r['selected']} | {r['checkedUtc']} |")
(out/'versions.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
