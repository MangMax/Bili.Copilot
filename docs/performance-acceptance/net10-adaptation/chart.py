import json, math, pathlib
here=pathlib.Path(__file__).parent
rows=json.loads((here/'summary.json').read_text(encoding='utf-8'))[:4]
labels={'visible':'Visible','hidden':'Hidden','restored':'Restored'}
def chart(key,title,unit):
    maximum=math.ceil(max(r['phases'][p][key] for r in rows for p in labels)/10)*10
    svg=[f'<svg role="img" aria-label="{title}" viewBox="0 0 680 260" style="width:100%;height:260px">']
    for tick in range(5):
        y=210-tick*45
        svg.append(f'<line x1="45" x2="670" y1="{y}" y2="{y}" stroke="var(--border,#777)"/><text x="38" y="{y+4}" text-anchor="end">{maximum*tick/4:g}</text>')
    for group,phase in enumerate(labels):
        for i,row in enumerate(rows):
            value=row['phases'][phase][key]; h=value/maximum*180; x=65+group*205+i*42
            color='var(--chart-1,#60a5fa)' if 'net9' in row['run'] else 'var(--chart-2,#f59e0b)'
            svg.append(f'<rect x="{x}" y="{210-h:.2f}" width="30" height="{h:.2f}" fill="{color}"><title>{row["run"]}: {value:.2f} {unit}</title></rect><text x="{x+15}" y="{202-h:.2f}" text-anchor="middle">{value:.1f}</text>')
        svg.append(f'<text x="{125+group*205}" y="238" text-anchor="middle">{labels[phase]}</text>')
    svg.append('</svg>')
    return f'<h3>{title} ({unit})</h3>'+''.join(svg)
html='''<!doctype html><html><meta charset="utf-8"><style>body{font-family:var(--font-sans,system-ui);color:var(--foreground,#ddd);margin:0}h3{font-size:15px;margin:16px 0 4px}p{font-size:13px;line-height:1.5;color:var(--muted-foreground,#999)}svg text{fill:var(--foreground,#ddd);font:11px system-ui}.legend{display:flex;gap:20px;font-size:13px}.a{color:var(--chart-1,#60a5fa)}.b{color:var(--chart-2,#f59e0b)}</style><body><div class="legend"><span class="a">■ net9: r1, r3</span><span class="b">■ net10: r2, r4</span></div><p>Bars in run order: r1 / r2 / r3 / r4. Same app source; common tray 2.3.2. External OS protocol v3.</p>'''
html+=chart('cpuMsPerSecond','Process CPU','ms/s')+chart('privateMiBMedian','Median private memory','MiB')
html+='<p>Only two completed runs per arm. Earlier v2 r3-net9 crashed and remains FAIL; it has no complete counters. Framework asset differences remain. These observations do not establish a general performance win or resolve the native crash.</p></body></html>'
(here/'results.html').write_text(html,encoding='utf-8')
