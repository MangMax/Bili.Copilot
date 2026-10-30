import html
import json
import pathlib
import sys
root=pathlib.Path(sys.argv[1])
runs=sys.argv[2:]
data={name:[r['data'] for r in map(json.loads,(root/name/'records.jsonl').read_text(encoding='utf-8-sig').splitlines()) if r['kind']=='sample'] for name in runs}
def chart(title,labels,series,maximum,unit):
    svg=f'<svg viewBox="0 0 800 270" role="img" aria-label="{html.escape(title)}">'
    for i in range(5):
        y=220-i*45;value=maximum*i/4
        svg+=f'<path d="M60 {y}H770" stroke="#dce3eb"/><text x="50" y="{y+4}" text-anchor="end">{value:g}</text>'
    for i,label in enumerate(labels):
        x=60+i*710/max(1,len(labels)-1)
        svg+=f'<text x="{x}" y="245" text-anchor="middle">{html.escape(str(label))}</text>'
    for si,(name,values,color) in enumerate(series):
        points=' '.join(f'{60+i*710/max(1,len(values)-1):.2f},{220-v/maximum*180:.2f}' for i,v in enumerate(values))
        svg+=f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="3"/>'
        for i,v in enumerate(values):
            svg+=f'<circle cx="{60+i*710/max(1,len(values)-1):.2f}" cy="{220-v/maximum*180:.2f}" r="4" fill="{color}"><title>{html.escape(name)}: {v:.2f} {unit}</title></circle>'
        svg+=f'<text x="{70+si*240}" y="20" fill="{color}">{html.escape(name)}</text>'
    return f'<section><h2>{title}</h2>{svg}</svg></section>'
first={r['checkpoint']:r for r in data[runs[0]]}
keys=['A-1','A-5','A-15-middle','A-15-bottom']
one=chart('固定加载梯度 · 条目与实际树内卡片', ['30条','150条','450条中部','450条底部'],[(label,[first[k][field] for k in keys],color) for label,field,color in [('当前条目','items','#255dbe'),('树内卡片（含保留元素）','realizedCards','#bd5f16'),('viewport相交','visibleCards','#218373')]],500,'个')
series=[]
for name,color in zip(runs,['#255dbe','#bd5f16']):
    by={r['checkpoint']:r for r in data[name]}
    series.append((name,[by[k]['privateBytes']/1048576 for k in ['A-return']+[f'loop-{i}-Recommend' for i in range(1,11)]],color))
two=chart('无新增条目的十轮往返 · 进程 Private Bytes（MiB）',['初始']+list(range(1,11)),series,800,'MiB')
doc='<!doctype html><html lang="zh"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><style>body{font:15px system-ui;margin:24px;color:#172b45;background:#fff}h1{font-size:22px}h2{font-size:17px}p{color:#43556c;line-height:1.6}section{max-width:900px}svg{width:100%;font:12px system-ui}</style><h1>#56 合成数据补充实验</h1><p>真实生产页面/卡片；不是原真实业务50页验收。树内卡片不等于全堆存活数，进程内存不用于推算预算或泄漏。</p>'+one+two+'<p>来源：同 DLL 独立运行 '+', '.join(runs)+' 的 records.jsonl。十轮内缓存660槽、660个不同VM/640个Id、服务请求数不变；整体资源未由此证明稳定。</p></html>'
(root/'comparison.html').write_text(doc,encoding='utf-8')
