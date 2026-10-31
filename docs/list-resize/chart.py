"""Render the matched 500-item native results as a standalone, source-backed chart."""
from pathlib import Path
import json,html
r=Path(__file__).resolve().parents[2]
rows=[x for x in json.loads((r/'artifacts/list-resize/comparison.json').read_text()) if x['count']==500]
labels={'Recommend':'推荐','Hot':'热门','Partition':'分区','Search':'搜索','Live':'直播','Article':'文章','User':'用户 / 头像','Stack':'播放列表','Moment':'动态','Season':'番剧 / 模糊封面','Notify':'普通通知'}
maximum=max(max(x['resizeP95']) for x in rows)
parts=[]
for row in rows:
    a,b=row['resizeP95']
    parts.append(f'<div class="row"><span>{labels[row["family"]]}</span><div class="bars"><div class="red" style="width:{a/maximum*100:.2f}%">{a:.1f}</div><div class="green" style="width:{b/maximum*100:.2f}%">{b:.1f}</div></div><small>{row["realized"][0]} → {row["realized"][1]}</small></div>')
document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><style>
body{font-family:var(--font-sans,system-ui);color:var(--foreground);margin:0}p{line-height:1.6}.legend{display:flex;gap:20px;font-size:13px}.row{display:grid;grid-template-columns:110px 1fr 72px;gap:12px;align-items:center;margin:10px 0;font-size:13px}.bars{display:grid;gap:3px}.bars div{box-sizing:border-box;min-width:42px;height:23px;padding:2px 5px;border-radius:3px;color:var(--foreground)}.red{background:var(--chart-1)}.green{background:var(--chart-2)}small{color:var(--muted-foreground)}.note{font-size:12px;color:var(--muted-foreground)}@media(max-width:460px){.row{grid-template-columns:78px 1fr 50px;gap:6px}}
</style><body><p><strong>同内容、500 条列表：窗口缩放调用耗时 p95（ms）</strong></p><div class="legend"><span>● 基线（上）</span><span>● 公共修复（下）</span><small>右侧：realized 数量</small></div>'''+''.join(parts)+'''<p class="note">固定 DPI 1.5，120 步同轨迹；每步 SetWindowPos 返回后延迟 16ms。柱长表示调用耗时，不是帧率。各模板单次长列表对照，非统计置信区间；详情及未通过项见报告。数据：artifacts/list-resize/comparison.json。</p></body></html>'''
(r/'docs/list-resize/comparison.html').write_text(document,encoding='utf-8')
