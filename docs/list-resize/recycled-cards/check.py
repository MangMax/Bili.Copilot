"""Validate every visible expected card, including missing ViewModel/Source/command."""
from pathlib import Path
import json,sys
stage=Path(sys.argv[1]); J=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
checks=[]
def check(name,ok,detail=None): checks.append(dict(name=name,pass_=bool(ok),detail=detail))
if (stage/'matrix-fast.json').exists():
 for run in J(stage/'matrix-fast.json'):
  samples=run['samples'];check('RapidTrajectoryComplete',len(samples)==62)
  bad=[];pending=[]
  for sample in samples:
   cards=sample['cards']
   if not cards or len(cards)!=sample['visible']:bad.append((sample['phase'],sample['step'],'missing card'))
   for c in cards:
    keys=('vmCorrect','outerCorrect','contentCorrect','contextCorrect','textCorrect','commandCorrect')
    ok=all(c.get(k,False) for k in keys) and c.get('actualId')==c.get('expectedId') and len(c['avatars'])==1 and all(im['correct'] and im['source'] is not None for im in c['avatars'])
    if not ok:bad.append((sample['phase'],sample['step'],c['index']))
    if any(not im['ready'] for im in c['avatars']):pending.append((sample['phase'],sample['step'],c['index']))
  check('EveryVisibleUserCardMatchesCurrentItem',not bad,bad)
  check('SettledVisibleAvatarsReady',not any(p[0] in ('after100','after1000') for p in pending),pending)
if (stage/'matrix-related.json').exists():
 for run in J(stage/'matrix-related.json'):
  bad=[]
  for s in run['samples']:
   if not s['cards']:bad.append((s['phase'],'empty'))
   for c in s['cards']:
    keys=['vmCorrect','contextCorrect','contentCorrect','commandCorrect']
    if 'textCorrect' in c:keys+=['textCorrect','imageSourceCorrect']
    if not all(c[k] for k in keys):bad.append((s['phase'],s['step'],c['index']))
  check('EveryVisibleRelatedCardMatchesCurrentItem_'+run['family'],not bad,bad)
  if 'imagesReady' in run['samples'][-1]['cards'][0]: check('SettledRelatedImagesReady_'+run['family'],all(c['imagesReady'] for c in run['samples'][-1]['cards']))
if (stage/'matrix-results.json').exists():
 for r in J(stage/'matrix-results.json'):
  if r.get('family')=='User':
   check('UserResizeOriginal200msGate_'+str(r['count']),len(r['resize'])==120 and max(r['beats'])<=200,dict(max=max(r['beats']),cache=r['cache'],count=r['count']))
if (stage/'matrix-checks.json').exists():
 for value in J(stage/'matrix-checks.json'): check(value.rsplit('/',1)[0],value.endswith('/PASS'))
for pattern in ('matrix-cover-checks.json','matrix-resize-checks.json'):
 if (stage/pattern).exists():
  for value in J(stage/pattern):
   if isinstance(value,str): check(value,not (stage/'matrix-resize-error.txt').exists())
   else: check(value['name'],value['pass'])
check('OwnedExitReceiptsPresent',all((stage/name).exists() for name in ('exit.json','identity.json','wer.json','remaining-owned-packages.txt','yield.json')))
if (stage/'exit.json').exists():
 ex=J(stage/'exit.json');identity=J(stage/'identity.json')
 check('OwnedRetainedExitAndWER',ex['absent'] and ex['observedSeconds']>=31 and ex['pid']==identity['pid'] and ex['path']==identity['path'] and ex.get('retainedHasExited') and ex.get('retainedExitCode')==0 and not J(stage/'wer.json'),ex)
 check('OwnedCleanupAndYield',(stage/'remaining-owned-packages.txt').read_text(encoding='utf-8-sig').strip()=='0' and bool(J(stage/'yield.json')))
if stage.name.startswith('covers'): check('CoverChecksDiscovered',(stage/'matrix-cover-checks.json').exists() and len(J(stage/'matrix-cover-checks.json'))>0)
if stage.name.startswith('undo'): check('UndoChecksDiscovered',(stage/'matrix-resize-checks.json').exists() and len(J(stage/'matrix-resize-checks.json'))>0 and (stage/'matrix-resize-results.json').exists())
check('FunctionalResultsPresent',any((stage/name).exists() and bool(J(stage/name)) for name in ('matrix-fast.json','matrix-related.json','matrix-results.json','matrix-cover-checks.json','matrix-resize-checks.json')))
check('NativeRunCompleted',(stage/'matrix-finished.txt').exists() and not (stage/'matrix-error.txt').exists() and not (stage/'trace.jsonl').exists() and not (stage/'matrix-resize-error.txt').exists())
result=dict(stage=stage.name,passed=sum(c['pass_'] for c in checks),failed=sum(not c['pass_'] for c in checks),checks=checks)
(stage/'checks.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='checks'}));sys.exit(2 if result['failed'] else 0)
