import pathlib,json,sys,hashlib,runpy
root=pathlib.Path(sys.argv[1])
summary=json.loads((root/'summary.json').read_text())
rows=[x for x in summary if x['run'].startswith('final') and 'pauseTotalMs' in x]
bars=''
for r in rows:
    mode='当前' if 'current' in r['run'] else '历史一行 GC'
    color='var(--chart-1,#638bdf)' if 'current' in r['run'] else 'var(--chart-2,#dd9862)'
    bars+=f'<div class="row"><span>{r["run"].split("-")[0]} · {mode}</span><div class="track"><div style="width:{r["pauseTotalMs"]/120*100:.2f}%;background:{color}"></div></div><strong>{r["pauseTotalMs"]:.2f}</strong></div>'
html='''<!doctype html><html lang="zh"><meta charset="utf-8"><style>body{font-family:var(--font-sans,system-ui);color:var(--foreground,#ddd)}h3{font-size:17px;margin:8px 0}p{font-size:13px;color:var(--muted-foreground,#aaa)}.row{display:grid;grid-template-columns:170px 1fr 65px;align-items:center;gap:8px;margin:13px 0;font-size:13px}.track{height:18px;background:var(--secondary,#333)}.track div{height:18px}strong{text-align:right}@media(max-width:500px){.row{grid-template-columns:120px 1fr 52px;font-size:11px}}</style><h3>50 次清理周期的运行时暂停总时长（ms）</h3><p>同源 Release JIT 控件 · 3 组交替顺序 · 每轮 5 次预热 · 零报告丢事件</p>'''+bars+'''<p>刻度 0–120 ms；挂起开始至恢复完成，含挂起协调。不是整个 collection duration。</p><p>当前臂总暂停较低，但最大单次暂停较长；不代表整机速度或长期无泄漏。GC 原因仍有未归因的 Induced 事件。</p></html>'''
(root/'comparison.html').write_text(html,encoding='utf-8')
