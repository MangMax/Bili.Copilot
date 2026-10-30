import gzip, hashlib, json, pathlib
root=pathlib.Path(__file__).resolve().parents[3]
data=root/'artifacts/net10-adaptation'; out=pathlib.Path(__file__).parent
records={}
for arm in ('player9common','player10common','player10final'):
    fixture=data/arm
    graph=json.loads((fixture/'obj/project.assets.json').read_text(encoding='utf-8-sig'))
    packages={key:dict(sha512=value.get('sha512'),type=value['type']) for key,value in graph['libraries'].items()}
    sources={}
    for file in fixture.rglob('*.cs'):
        rel=file.relative_to(fixture)
        if any(p in ('bin','obj','frozen-output') for p in rel.parts): continue
        text=file.read_bytes().replace(b'N854B',b'N854X').replace(b'N854C',b'N854X').replace(b'N854F',b'N854X').replace(b'\r\n',b'\n')
        sources[str(rel)]=hashlib.sha256(text).hexdigest()
    frozen=json.loads((fixture/'frozen-all-files.json').read_text(encoding='utf-8-sig'))
    records[arm]=dict(packages=packages,normalizedSources=sources,frozenFiles=frozen,
                      runtime=json.loads((fixture/'frozen-output/BiliCopilot.UI.runtimeconfig.json').read_text(encoding='utf-8-sig')))
    (out/f'{arm}-assets.json.gz').write_bytes(gzip.compress((fixture/'obj/project.assets.json').read_bytes(),mtime=0))
a=records['player9common']; b=records['player10common']; final=records['player10final']
comparison=dict(sourceDifferences=[k for k in sorted(a['normalizedSources'].keys()|b['normalizedSources'].keys()) if a['normalizedSources'].get(k)!=b['normalizedSources'].get(k)],
                finalSourceDifferences=[k for k in sorted(final['normalizedSources'].keys()|b['normalizedSources'].keys()) if final['normalizedSources'].get(k)!=b['normalizedSources'].get(k)],
                net9OnlyPackages=sorted(a['packages'].keys()-b['packages'].keys()),net10OnlyPackages=sorted(b['packages'].keys()-a['packages'].keys()),
                finalOnlyPackages=sorted(final['packages'].keys()-b['packages'].keys()),commonOnlyPackages=sorted(b['packages'].keys()-final['packages'].keys()))
(out/'closure-comparison.json').write_text(json.dumps(comparison,indent=2),encoding='utf-8')
(out/'closures.json.gz').write_bytes(gzip.compress(json.dumps(records,indent=2).encode(),mtime=0))
print(json.dumps(comparison,indent=2))
