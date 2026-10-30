"""Archive upstream package metadata without executing package content."""
import concurrent.futures, datetime, hashlib, json, pathlib, urllib.request, xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / 'docs/performance-acceptance/net10-adaptation'
CACHE = pathlib.Path.home() / '.nuget/packages'
rows = json.loads((ROOT / 'docs/performance-acceptance/dependency-upgrade/versions.json').read_text(encoding='utf-8-sig'))

def inspect(row):
    row = dict(row)
    if row['id'] == 'H.NotifyIcon.WinUI': row['selected'] = '2.4.1'
    name = row['id'].lower()
    versions = dict(old=row.get('oldResolvedApp') or row['old'], selected=row['selected'])
    row['capabilityMetadata'] = {}
    for label, version in versions.items():
        version = version.strip('[]').lower()
        folder = CACHE / name / version
        nuspec = folder / (name + '.nuspec')
        data = nuspec.read_bytes() if nuspec.exists() else urllib.request.urlopen(f'https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.nuspec',timeout=30).read()
        tree = ET.fromstring(data)
        fields = {}
        for node in tree.iter():
            tag = node.tag.rsplit('}',1)[-1]
            if tag in ('releaseNotes','repository','projectUrl','description'):
                fields[tag] = {'text':node.text,'attributes':node.attrib}
        row['capabilityMetadata'][label] = {'version':version,'nuspecSha256':hashlib.sha256(data).hexdigest(),'fields':fields,
            'assemblies':[str(p.relative_to(folder)) for p in folder.glob('lib/**/*') if p.suffix in ('.dll','.xml')]}
    with urllib.request.urlopen(row['source'],timeout=30) as response:
        index = json.load(response)
    row['selectedPresentOnline'] = row['selected'].lower() in [v.lower() for v in index['versions']]
    row['recheckedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    return row

def safe(row):
    try: return inspect(row)
    except Exception as exc: return {**row, 'auditError':str(exc)}

with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    result = list(pool.map(safe, [r for r in rows if r['scope']=='app-and-tests']))
(OUT/'package-metadata.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'rows':len(result),'errors':[{ 'id':r['id'],'error':r['auditError']} for r in result if 'auditError' in r]}))
