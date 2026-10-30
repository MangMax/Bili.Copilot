"""Record exact resolved package libraries and dependency edges, not manifest guesses."""
import json, pathlib, sys, hashlib
fixture=pathlib.Path(sys.argv[1])
assets=fixture/'obj/project.assets.json'
data=json.loads(assets.read_text(encoding='utf-8-sig'))
out={'assetsSha256':hashlib.sha256(assets.read_bytes()).hexdigest(),
     'libraries':{k:v for k,v in data['libraries'].items() if v['type']=='package'},
     'targets':{t:{k:v.get('dependencies',{}) for k,v in d.items()} for t,d in data['targets'].items()},
     'logs':data.get('logs',[])}
pathlib.Path(sys.argv[2]).write_text(json.dumps(out,indent=2))
if any(x['level']=='Error' for x in out['logs']): raise SystemExit('Restore errors in graph')
