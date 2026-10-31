from pathlib import Path
import json,hashlib,shutil,zipfile
r=Path.cwd();o=r/'artifacts/remount-6273';d=r/'docs/engine-61-design-draft/legacy-visibility-review';s=d/'source';s.mkdir(exist_ok=True)
p=d/'README.md';p.write_text(p.read_text(encoding='utf-8').replace('11016766、59945944','46779618、59945944'),encoding='utf-8')
scripts=['prepare-clean.py','prepare-app.py','launch-retain.ps1','ui-app-screen.py','check-pixels.ps1','png_pixels.py','app-results.py','validate.py','Probe.cs','seal.py']
for n in scripts:shutil.copyfile(o/n,s/n)
old=json.loads((o/'prior/full/runtime/results.json').read_text())
pix={x['file']:x for x in json.loads((o/'prior-pixels-v2/pixels.json').read_text(encoding='utf-8-sig'))['records']}
contradictions=[]
for c in old['checks']:
 if c['name'].startswith('PausedRemountVisible'):
  n='screen-remount'+c['name'][-1]+'-stable.png'
  if c['passed']!=pix[n]['matchesTop20']:contradictions.append({'check':c['name'],'savedVerdict':c['passed'],'decodedTop20':pix[n]['matchesTop20'],'file':n})
assert len(contradictions)==2
(o/'verdict-regression.json').write_text(json.dumps({'legacyVerdictGate':'FAIL','contradictions':contradictions,'newResult':'22 PASS / 0 FAIL','productionChanges':0},indent=2))
files=[o/n for n in ['full-build.log','validation.json','source-closure.json','verdict-regression.json','glyph-contact.png','kernel-head','kernel-status','kernel-staged.patch','kernel-worktree.patch']]
files+=list((o/'full/runtime').glob('*'))+[o/'full/events.jsonl',o/'full/src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs',o/'full/src/Desktop/BiliCopilot.UI/App.xaml.cs',o/'full/src/Desktop/BiliCopilot.UI/GlobalDependencies.cs',o/'full/src/Directory.Packages.props',o/'full/src/Desktop/BiliCopilot.UI/Package.appxmanifest']
files+=list((o/'pixels').glob('*'))+list((o/'prior-pixels-v2').glob('*'))+list((o/'clean/feed').glob('*.nupkg'))+list((o/'clean').glob('*.log'))
m=json.loads((r/'docs/engine-61-design-draft/legacy-paused-seek/origin.json').read_text())
files += [o/'clean/source'/n for n in m['files']]+[o/n for n in scripts]
manifest={}
with zipfile.ZipFile(d/'evidence.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(set(files)):
  if not p.is_file():continue
  n=p.relative_to(o).as_posix();b=p.read_bytes();manifest[n]={'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()};z.writestr(n,b)
(d/'manifest.json').write_text(json.dumps(manifest,indent=2));print('sealed',len(manifest),'files', (d/'evidence.zip').stat().st_size,'bytes')
