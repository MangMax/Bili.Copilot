"""Read direct/central versions and official NuGet indexes; never mutate manifests."""
import concurrent.futures, datetime, json, pathlib, urllib.request, xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / 'artifacts/dependency-upgrade'
OUT.mkdir(parents=True, exist_ok=True)
rows, ids = [], set()
for area in ['src', 'tests', 'externals/bili-kernel/src']:
    files = [p for p in (ROOT/area).rglob('*') if p.suffix in ('.csproj','.props') and not {'obj','bin','_build'}.intersection(p.parts)]
    refs = {}
    for p in files:
        for n in ET.parse(p).iter('PackageReference'):
            refs.setdefault(n.get('Include'), []).append(str(p.relative_to(ROOT)))
    for p in files:
        for n in ET.parse(p).iter():
            if n.tag not in ('PackageVersion','PackageReference') or not n.get('Version'): continue
            name = n.get('Include'); ids.add(name)
            rows.append(dict(area=area, file=str(p.relative_to(ROOT)), id=name, old=n.get('Version'), kind=n.tag, references=refs.get(name,[])))
ids.update(['Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer','Richasy.WinUIKernel.Share'])
feeds=[]
for node in ET.parse(ROOT/'nuget.config').findall('./packageSources/add'):
    url=node.get('value')
    if url.endswith('/v3/index.json'):
        try:
            with urllib.request.urlopen(url,timeout=40) as response: index=json.load(response)
            base=next(r['@id'] for r in index['resources'] if r['@type'].startswith('PackageBaseAddress'))
            feeds.append(dict(source=url,base=base))
        except Exception as error: feeds.append(dict(source=url,error=str(error)))
(OUT/'feeds.json').write_text(json.dumps(feeds,indent=2),encoding='utf-8')
def fetch(name):
    url='https://api.nuget.org/v3-flatcontainer/'+name.lower()+'/index.json'
    now=datetime.datetime.now(datetime.timezone.utc).isoformat()
    try:
        with urllib.request.urlopen(url, timeout=40) as response: data=json.load(response)
        versions=data['versions']; stable=[v for v in versions if '-' not in v]
        return name, dict(source=url, checkedUtc=now, latestStable=stable[-1] if stable else None, latestAvailable=versions[-1], versions=versions)
    except Exception as e:
        errors=[str(e)]
        candidates=[url]
        if name.startswith('CommunityToolkit.Labs.'):
            candidates.extend(f['base']+name.lower()+'/index.json' for f in feeds if 'base' in f and 'CommunityToolkit' in f['source'])
        for candidate in candidates:
            try:
                with urllib.request.urlopen(candidate,timeout=40) as response: data=json.load(response)
                versions=data['versions']
                # Azure feeds return newest first, nuget.org oldest first.
                if candidate != url: versions=list(reversed(versions))
                stable=[v for v in versions if '-' not in v]
                return name,dict(source=candidate,checkedUtc=now,latestStable=stable[-1] if stable else None,latestAvailable=versions[-1],versions=versions,initialErrors=errors)
            except Exception as error: errors.append(str(error))
        return name,dict(source=url, checkedUtc=now,error=errors)
with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool: metadata=dict(pool.map(fetch,sorted(ids)))
(OUT/'inventory.json').write_text(json.dumps(dict(rows=rows,metadata=metadata),indent=2),encoding='utf-8')
for name,data in metadata.items(): print(name,data.get('latestStable'),data.get('latestAvailable'),data.get('error',''))
