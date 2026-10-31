import json, pathlib
r=pathlib.Path(__file__).resolve().parents[2]
a=r/'artifacts/resize-diagnosis'
red=json.loads((a/'red-final/resize-results.json').read_text(encoding='utf-8-sig'))
green=json.loads((a/'green-final/resize-results.json').read_text(encoding='utf-8-sig'))
bars=[]
for i,(x,y) in enumerate(zip(red,green)):
    top=35+i*75
    bars.append(f'<text x="0" y="{top+18}">{x["count"]} 条</text>')
    for offset,row,color in [(0,x,'--chart-1'),(26,y,'--chart-2')]:
        value=row['heartbeatMax']; width=value/600*440
        bars.append(f'<rect x="65" y="{top+offset}" width="{width:.1f}" height="18" rx="3" fill="var({color})"/><text x="{72+width:.1f}" y="{top+offset+14}">{value:.1f} ms</text>')
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>
body{font-family:var(--font-sans);color:var(--foreground);margin:0}svg{width:100%;height:270px}text{fill:var(--foreground);font-size:13px}.legend{display:flex;gap:18px;font-size:13px}.swatch{display:inline-block;width:12px;height:12px;border-radius:2px;margin-right:5px}p{font-size:13px;color:var(--muted-foreground);line-height:1.5}
</style><div class="legend"><span><i class="swatch" style="background:var(--chart-1)"></i>原实现</span><span><i class="swatch" style="background:var(--chart-2)"></i>尺寸合并 + 并发上限</span></div><svg viewBox="0 0 570 270" role="img" aria-label="80、200、500条推荐列表，UI心跳最大间隔从522、425、421毫秒降至124、123、124毫秒">'''+''.join(bars)+'''</svg><p>相同本地图片、尺寸轨迹、DPI 1.5；每档 realized 均为 76。数值为 UI 心跳最大间隔，越低越好；不是帧率或全局 CPU 收益。</p></html>'''
(pathlib.Path(__file__).parent/'comparison.html').write_text(html,encoding='utf-8')
