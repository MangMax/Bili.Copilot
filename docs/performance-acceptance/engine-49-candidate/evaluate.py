"""Validate complete result discovery and report the actual harness outcomes."""
import json
from pathlib import Path
import sys

cases = {'DequeuedPause', 'DequeuedResume', 'DequeuedClear', 'DequeuedSetList',
         'DequeuedSeek', 'DequeuedClose', 'Zero', 'Threshold5000', 'Threshold5001',
         'Backward', 'Repeated', 'Empty', 'End'}
expected = {engine + '_' + case for engine in ('DirectX', 'Win2D') for case in cases}
rows = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
assert len(rows) == len(expected) and {row['name'] for row in rows} == expected
assert all(type(row['passed']) is bool for row in rows)
failed = sum(not row['passed'] for row in rows)
print(f'Passed: {len(rows) - failed}, Failed: {failed}, Skipped: 0')
for row in rows:
    if not row['passed']:
        print('FAIL ' + row['name'])
sys.exit(1 if failed else 0)
