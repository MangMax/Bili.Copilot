"""Show all run CPU rates; no aggregate significance or causal claim."""
import html, json
from prepare import BASE, HERE
data=json.loads((BASE/'analysis.json').read_text(encoding='utf-8'))
maximum=max((s['cpuMsPerSecond'] for r in data['runs'] for s in r['states']),default=1)*1.12
parts=['<!doctype html><html><head><meta charset="utf-8"><style>body{font-family:var(--font-sans,system-ui);color:var(--foreground);margin:0}p{color:var(--muted-foreground);font-size:13px}.row{display:grid;grid-template-columns:95px 1fr 65px;gap:10px;align-items:center;margin:9px 0;font-size:12px}.track{height:15px;background:var(--secondary)}.bar{height:15px;background:var(--chart-1)}.b{background:var(--chart-2)}h3{font-size:14px;margin:20px 0 8px}.value{text-align:right;font-variant-numeric:tabular-nums}</style></head><body><p>同源离线设置页 · CPU 毫秒/秒，越低越少 · ABBA，每臂 2 个进程。非冷启动、非纯 JIT 比较。</p>']
for phase,title in [('visible','可见'),('hidden','隐藏'),('restored','恢复')]:
    parts.append('<h3>'+title+'</h3>')
    for run in data['runs']:
        state=next(s for s in run['states'] if s['phase']==phase)
        value=state['cpuMsPerSecond'];color=' b' if run['arm']=='net11' else ''
        parts.append(f'<div class="row"><span>{html.escape(run["run"])}</span><div class="track"><div class="bar{color}" style="width:{value/maximum*100:.3f}%"></div></div><span class="value">{value:.3f}</span></div>')
parts.append('<p>统一横轴；全部原始样本、内存/句柄/线程区间及背景活动见报告。小样本不支持总体提升或显著性结论。</p></body></html>')
(HERE/'results.html').write_text(''.join(parts),encoding='utf-8')
