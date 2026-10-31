"""Plot every retained large-PNG NuGet User sample, without averaging away reds."""
from pathlib import Path
import json,html
r=Path(__file__).resolve().parents[2];a=r/'artifacts/list-p1'
groups=[('原始 cache20',['baseline']),('仅生命周期 cache20',['lifecycle','lifecycle-rebuild']),('生命周期 + cache2',['candidate','small-source']),('中间尝试 cache1',['cache1','final-main']),('修复 cache0.5',['half-user-png'])]
parts=[]
for count,limit in ((50,300),(500,1600)):
    svg=[];left=160;plot=480
    for tick in ([0,100,200,300] if count==50 else [0,200,800,1600]):
        x=left+tick/limit*plot
        color='var(--destructive)' if tick==200 else 'var(--border)'
        svg.append(f'<line x1="{x}" x2="{x}" y1="20" y2="240" stroke="{color}" stroke-dasharray="4 4"/><text x="{x}" y="260" text-anchor="middle">{tick} ms</text>')
    for i,(label,stages) in enumerate(groups):
        y=40+i*44;svg.append(f'<text x="0" y="{y+4}">{label}</text>')
        values=[]
        for j,stage in enumerate(stages):
            rows=json.loads((a/stage/'matrix-results.json').read_text(encoding='utf-8-sig'))
            row=next(x for x in rows if x['family']=='User' and x['count']==count)
            value=max(row['beats']);values.append(f'{value:.1f}')
            x=left+value/limit*plot;dy=(j-(len(stages)-1)/2)*10
            color='var(--success)' if i==4 else ('var(--destructive)' if value>200 else 'var(--chart-1)')
            svg.append(f'<circle cx="{x}" cy="{y+dy}" r="5" fill="{color}"><title>{html.escape(stage)}: {value:.2f} ms</title></circle>')
        svg.append(f'<text x="{left}" y="{y+18}" class="value">{" / ".join(values)}</text>')
    parts.append(f'<section><h3>User {count} · 最大 UI 心跳间隔</h3><svg viewBox="0 0 680 274" role="img" aria-label="User {count} 原始样本对照">{"".join(svg)}</svg></section>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>
body{margin:0;color:var(--foreground);font-family:var(--font-sans)}h3{font-size:15px;margin:12px 0 4px}svg{display:block;width:100%;height:274px;overflow:visible}text{fill:var(--foreground);font:12px var(--font-sans)}.value{fill:var(--muted-foreground);font-size:11px}p{font-size:12px;line-height:1.6;color:var(--muted-foreground);margin:8px 0}section+section{border-top:1px solid var(--border);margin-top:12px}
</style>'''+''.join(parts)+'''<p>每个点为一次真实原始样本；红虚线为未放宽的 200ms 门槛。两图横轴不同。使用相同 1600×900 PNG、宽度轨迹和 1.5 DPI；含后台用户进程干扰，未宣称统计显著性。小源、JPEG/WebP/AVIF 和诊断源码插桩数据另列于报告。短列表仍比全部保留的 cache20 基线慢，不能解读为每项指标都改善。</p></html>'''
(r/'docs/list-resize/p1-comparison.html').write_text(page,encoding='utf-8')
print('p1-comparison.html')
