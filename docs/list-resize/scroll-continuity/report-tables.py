"""Refresh only the marked measured tables from audit.py's summary ledger."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[3]
rows=json.loads((root/'artifacts/scroll-continuity/summary.json').read_text())
def get(stage,count):return next(r for r in rows if r['stage']==stage and r['count']==count)
def pair(a,b,key):return f'{a[key]:.0f} → {b[key]:.0f}'
lines=['| 家族/数量 | 整片背景总 ms | 最长连续 ms | realized 最大 | private MB | resize heartbeat ms |',
       '|---|---:|---:|---:|---:|---:|']
for label,stem in [('视频','inertia-search'),('User','inertia-user'),('直播','inertia-live'),('文章/修复后唯一源','fixed-article'),('Season/唯一源','real-season'),('文章/1600×900 均一源压力','inertia-article')]:
 for count in (50,500):
  a,b=get(stem+'05',count),get(stem+'2',count)
  lines.append(f'| {label}/{count} | '+' | '.join(pair(a,b,k) for k in ('blankMs','blankMaxMs','maxRealized','privateMB','resizeMax'))+' |')
lines+=['','已经 warmup 区域的 Return 轨迹，六组数量/家族在两档均为 **0** 个整片背景样本。',
        '因此不能称这条轨迹的留白被修复；可确认的是反向工作量减少：','',
        '| Return 500 项 | 反向 prepared | 反向 Basic draw | private MB |','|---|---:|---:|---:|']
for family in ('user','season','article'):
 a,b=get('return-'+family+'05',500),get('return-'+family+'2',500)
 lines.append(f'| {family} | '+' | '.join(pair(a,b,k) for k in ('reversePrepared','reverseDraws','privateMB'))+' |')
lines+=['','Season 的计数器只统计 Basic，不统计 Blur，0 不能解释为没有图片工作。',
        'Return Article 使用旧均一源，不能代替修复后唯一头像的连续性能验收。']
p=root/'docs/list-resize/scroll-continuity-results.md';s=p.read_text(encoding='utf-8')
start='<!-- MEASUREMENTS -->';end='<!-- END MEASUREMENTS -->'
assert start in s
if end in s:s=s[:s.index(start)]+start+s[s.index(end)+len(end):]
s=s.replace(start,start+'\n\n'+'\n'.join(lines)+'\n\n'+end)
p.write_text(s,encoding='utf-8')
