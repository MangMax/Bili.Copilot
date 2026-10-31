"""Validate a completed native resize run (red runs intentionally fail)."""
import json, pathlib, sys
p=pathlib.Path(sys.argv[1])
rows=json.loads((p/'resize-results.json').read_text(encoding='utf-8-sig'))
assert len(rows)==3
assert not (p/'resize-error.txt').exists()
for row in rows:
    assert row['count']==row['startCount']
    print(f"count={row['count']} realized={row['realized']} heartbeatMax={row['heartbeatMax']:.2f}ms decoders={row['Decoders']} pass={row['pass']}")
exit_record=json.loads((p/'exit.json').read_text(encoding='utf-8-sig'))
assert exit_record['absent'] and exit_record['observedSeconds']>=31
assert exit_record['path'] and exit_record['pid']>0
assert json.loads((p/'wer.json').read_text(encoding='utf-8-sig'))==[]
assert all(row['pass'] for row in rows), 'Resize responsiveness gate FAILED'
print('PASS')
