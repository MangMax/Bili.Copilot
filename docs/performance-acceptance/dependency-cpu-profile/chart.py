import json
from pathlib import Path

here=Path(__file__).resolve().parent
root=here.parents[2]/'artifacts/dependency-cpu-profile'
runs=[('r2-up-profile','候选 · profile',2),('r3-base-ref','Baseline · 参照',1),('r4-up-ref','候选 · 参照',2),('r5-base-profile','Baseline · profile',1)]
bars=[]
for i,(run,label,color) in enumerate(runs):
    data=json.loads((root/run/'summary.json').read_text())
    full,first,last=data['phases']['restored']['cpuMsPerSecond_full_first_last']
    y=30+i*48
    bars.append(f'<text x="2" y="{y+18}">{label}</text><rect x="162" y="{y}" width="{full*4.3:.2f}" height="27" rx="3" fill="var(--chart-{color})"><title>{run}: {full:.2f} ms/s; first half {first:.2f}; last half {last:.2f}</title></rect><text x="{172+full*4.3:.2f}" y="{y+18}">{full:.2f}</text>')
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>
body{margin:0;color:var(--foreground);font-family:var(--font-sans)}figure{margin:0}figcaption{font-weight:600;margin-bottom:8px}svg{width:100%;height:250px}svg text{fill:var(--foreground);font-size:14px}p{font-size:13px;color:var(--muted-foreground);line-height:1.5;margin:0}line{stroke:var(--border)}
</style><figure><figcaption>恢复阶段 CPU · ms/s</figcaption><svg viewBox="0 0 590 250" role="img" aria-label="四个独立进程恢复阶段CPU。候选profile58.69，baseline参照33.64，候选参照45.22，baseline profile78.62。不能据此归因包回退。">'''+''.join(bars)+'''<line x1="162" x2="550" y1="226" y2="226"/><text x="158" y="246">0</text><text x="326" y="246">40</text><text x="498" y="246">80</text></svg><p>按运行顺序排列；每条件仅 1 个进程。profile 与参照是不同观测条件，不可直接相减为采集开销。native on-CPU 栈仍不可用，整体性能验收维持 HOLD。</p></figure></html>'''
(here/'results.html').write_text(html,encoding='utf-8')
