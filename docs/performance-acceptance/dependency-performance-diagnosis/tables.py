import json, pathlib
root=pathlib.Path(__file__).resolve().parents[3]
data=root/'artifacts/dependency-performance-diagnosis'
lines=['## Measurements','', 'CPU entries below are full / first-half / last-half **ms/s**, never independent-sample estimates.','']
for group in (1,2):
    p=data/f'native-group{group}-summary.json'
    if not p.exists(): continue
    d=json.loads(p.read_text(encoding='utf-8-sig'))
    lines += [f'### Native group {group}', '', '| Run (chronological) | Visible | Hidden | Restored |','|---|---:|---:|---:|']
    for run in d['native']:
        values=[' / '.join(f"{run['phases'][phase][part]['cpuMsPerSecond']:.2f}" for part in ('full','first','last')) for phase in ('visible','hidden','restored')]
        lines.append('| '+run['run']+' | '+' | '.join(values)+' |')
    lines += ['']
lines += ['### LCS process-median ranges','', 'Two independent processes per variant; every median uses all twelve measured iterations. Timing in milliseconds. Allocation is bytes/operation (Compute+Apply).', '',
          '| Items / workload | Rectangular ms | Jagged pre-shortcut ms | Final shortcut ms | Rectangular → final bytes |', '|---|---:|---:|---:|---:|']
for row in json.loads((data/'lcs-summary.json').read_text(encoding='utf-8-sig')):
    v=row['variants']
    values=[f"{min(v[x]['processMediansMs']):.4f}–{max(v[x]['processMediansMs']):.4f}" for x in ('Rectangular','Jagged','Shortcut')]
    lines.append(f"| {row['n']} / {row['kind']} | "+' | '.join(values)+f" | {v['Rectangular']['allocatedBytes'][0]:,} → {v['Shortcut']['allocatedBytes'][0]:,} |")
report=root/'docs/performance-acceptance/dependency-performance-diagnosis.md'
text=report.read_text(encoding='utf-8').split('## Measurements')[0]
report.write_text(text+'\n'+'\n'.join(lines)+'\n',encoding='utf-8')
