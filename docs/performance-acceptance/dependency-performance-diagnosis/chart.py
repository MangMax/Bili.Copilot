import html,json,math,pathlib
root=pathlib.Path(__file__).resolve().parents[3];data=root/'artifacts/dependency-performance-diagnosis'
runs=[]
for group in (1,2): runs+=json.loads((data/f'native-group{group}-summary.json').read_text(encoding='utf-8-sig'))['native']
limit=math.ceil(max(r['phases']['restored'][h]['cpuMsPerSecond'] for r in runs for h in ('full','first','last'))/20)*20
svg=['<svg viewBox="0 0 720 310" role="img" aria-label="各独立进程恢复阶段 CPU，条为完整阶段，圆点为前后半段" style="width:100%;height:310px">']
for v in range(0,limit+1,20):
    y=255-v/limit*210
    svg.append(f'<line x1="45" y1="{y}" x2="710" y2="{y}" stroke="var(--border)"/><text x="38" y="{y+4}" text-anchor="end">{v}</text>')
for i,r in enumerate(runs):
    arm=r['run'][-1].upper();x=62+i*81;vals=r['phases']['restored'];v=vals['full']['cpuMsPerSecond'];y=255-v/limit*210
    color={'A':1,'B':2,'C':3}[arm]
    svg.append(f'<rect x="{x}" y="{y}" width="44" height="{255-y}" fill="var(--chart-{color})" opacity=".75"><title>{html.escape(r["run"])}: {v:.2f} ms/s</title></rect>')
    for h,dx,fill in [('first',10,'var(--foreground)'),('last',34,'var(--background)')]:
        val=vals[h]['cpuMsPerSecond'];yy=255-val/limit*210
        svg.append(f'<circle cx="{x+dx}" cy="{yy}" r="4" fill="{fill}" stroke="var(--foreground)"><title>{h}: {val:.2f} ms/s</title></circle>')
    svg.append(f'<text x="{x+22}" y="275" text-anchor="middle">{arm}{i%4+1}</text><text x="{x+22}" y="{y-8}" text-anchor="middle">{v:.2f}</text>')
svg.append('<text x="205" y="303" text-anchor="middle">组 1 · 图形兼容组</text><text x="530" y="303" text-anchor="middle">组 2 · AppSDK 组</text></svg>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>
body{color:var(--foreground);font-family:var(--font-sans);margin:0}p{line-height:1.5;margin:8px 0}svg text{fill:var(--foreground);font:12px var(--font-sans)}small{color:var(--muted-foreground)}
</style><p><strong>恢复阶段 CPU（ms/s）</strong> · 每条代表一个独立进程</p>'''+''.join(svg)+'''<p><small>条：完整 60 个时间间隔；实点/空点：前/后半段。未剔除样本，不是 61 次独立试验。A：最终候选；B：Win2D/图标兼容回退；C：B 的图形 + 旧 AppSDK。两组按各自时间顺序解读。</small></p>
<p><strong>非空 LCS 额外分配</strong>（jagged 相对 rectangular，bytes/operation）</p>'''
for size,value in [(128,4624),(512,18448),(1024,36880)]:
    page+=f'<div style="display:flex;align-items:center;gap:12px;margin:8px 0"><span style="width:105px">{size} × {size}</span><div style="height:16px;width:{value/36880*55:.2f}%;background:var(--chart-4)"></div><span>{value:,}</span></div>'
page+='<p><small>默认分层编译下，两进程/版本，每场景 5 次暖机、12 次测量。jagged 与 shortcut 的非空分支分配及行为一致；详见报告中的全部耗时和原始数据。</small></p></html>'
(pathlib.Path(__file__).parent/'results.html').write_text(page,encoding='utf-8')
