"""Behavior gate independent of native exit success; red is never silently green."""
import json, pathlib, sys
rows = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
expected = {f'{name}{cycle}' for cycle in (1, 2) for name in
            ('LoadedTimerRuns', 'LoadedTimerTicks', 'UnloadedTimerStops', 'NoReadAfterUnload')}
expected.add('LateTickDoesNotReadClosedVm')
valid = len(rows) == len(expected) and {r['name'] for r in rows} == expected
failed = [r['name'] for r in rows if r['status'] != 'PASS']
print(json.dumps(dict(valid=valid, failed=failed)))
sys.exit(0 if valid and not failed else 2)
