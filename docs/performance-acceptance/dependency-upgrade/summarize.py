"""Summarize raw controlled runs; no significance or whole-app extrapolation."""
import datetime as dt
import json
import pathlib
import statistics as st

root = pathlib.Path(__file__).resolve().parents[3]
source = root / 'artifacts/dependency-upgrade'
result = {'native': [], 'list': [], 'excludedNative': []}
for path in sorted(source.glob('native-*/idle-baseline.json')):
    exit_file=path.parent/'exit.json'
    exit_data=json.loads(exit_file.read_text(encoding='utf-8-sig')) if exit_file.exists() else {}
    if not exit_data.get('explicitExit') or exit_data.get('unhandledCount') != 0:
        result['excludedNative'].append(dict(run=path.parent.name,reason='No clean explicit-exit gate'))
        continue
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    for phase in ('visible', 'hidden', 'restored'):
        rows = [r for r in data['rows'] if r['phase'] == phase]
        assert len(rows) == 61, path
        assert all(r['scale'] == 1.5 for r in rows), path
        seconds = (dt.datetime.fromisoformat(rows[-1]['utc']) - dt.datetime.fromisoformat(rows[0]['utc'])).total_seconds()
        result['native'].append(dict(run=path.parent.name, phase=phase, samples=len(rows), seconds=seconds,
            cpuMsPerSecond=(rows[-1]['cpuMilliseconds']-rows[0]['cpuMilliseconds'])/seconds,
            privateMiBMedian=st.median(r['privateBytes'] for r in rows)/1048576,
            workingSetMiBMedian=st.median(r['workingSet'] for r in rows)/1048576,
            handlesFirst=rows[0]['handles'], handlesLast=rows[-1]['handles'],
            threadsFirst=rows[0]['threads'], threadsLast=rows[-1]['threads']))
for path in sorted(source.glob('list-*.json')):
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    if 'rows' not in data:
        continue
    for size in (100, 1000, 5000):
        rows = [r for r in data['rows'] if r['size'] == size]
        assert len(rows) == 10, path
        values = [r['milliseconds'] for r in rows]
        result['list'].append(dict(run=path.stem, framework=data['framework'], size=size,
            medianMs=st.median(values), minMs=min(values), maxMs=max(values),
            allocatedBytesMedian=st.median(r['allocatedBytes'] for r in rows)))
(source/'summary.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
