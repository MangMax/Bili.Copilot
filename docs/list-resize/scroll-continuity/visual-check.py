"""Strict symptom oracle, not a 60fps or product acceptance threshold.

Returns 2 if any measured frame is >90% viewport background. Candidates can
reduce this symptom without passing this deliberately strict zero-blank check.
"""
import json, sys
from pathlib import Path
p=Path(sys.argv[1]);rows=json.loads((p/'video-pixels.json').read_text(encoding='utf-8-sig'))
results=[]
for count in sorted({r['count'] for r in rows}):
 sample=[r for r in rows if r['count']==count]
 bad=[dict(ms=r['ms'],file=r['file']) for r in sample if r['background']>.9]
 results.append(dict(name='NoWholeBackgroundDuringMotion',count=count,passed=not bad,samples=len(sample),bad=bad))
(p/'visual-checks.json').write_text(json.dumps(results,indent=2))
print(json.dumps([dict(count=r['count'],passed=r['passed'],blankSamples=len(r['bad'])) for r in results]))
sys.exit(0 if all(r['passed'] for r in results) else 2)
