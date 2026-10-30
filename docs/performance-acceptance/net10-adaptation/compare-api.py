"""Compare shipped XML documentation, not inferred semantic version changes."""
import json, pathlib, xml.etree.ElementTree as ET
here=pathlib.Path(__file__).parent
rows=json.loads((here/'package-metadata.json').read_text(encoding='utf-8'))
cache=pathlib.Path.home()/'.nuget/packages'
def members(name,version):
    root=cache/name.lower()/version.strip('[]').lower()
    files=list(root.glob('lib/**/*.xml'))
    result=set()
    for file in files:
        try: result.update(n.attrib['name'] for n in ET.parse(file).findall('.//member') if 'name' in n.attrib)
        except ET.ParseError: pass
    return files,result
result=[]
for row in rows:
    old=row.get('oldResolvedApp') or row['old']
    a,ma=members(row['id'],old); b,mb=members(row['id'],row['selected'])
    result.append(dict(id=row['id'],old=old,selected=row['selected'],oldXml=len(a),newXml=len(b),
                       added=sorted(mb-ma) if a and b else None,removed=sorted(ma-mb) if a and b else None))
(here/'api-diff.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
for r in result:
    if r['added']: print(r['id'],len(r['added']),r['added'][:12])
