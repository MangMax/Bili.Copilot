"""Recompute only sanitized UIA/process observations; never infer business pages."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent / 'evidence-56-authenticated'
rows = [json.loads(line) for line in (root / 'samples.jsonl').read_text(encoding='utf-8-sig').splitlines()]
by_label = {row['label']: row for row in rows}
assert len(by_label) == len(rows)
assert all(r['pid'] == 48012 and r['businessBatches'] is None and r['actualItems'] is None and r['realizedCards'] is None for r in rows)
cycles = [by_label[f'cycle-{i}-{kind}'] for i in range(1, 11) for kind in 'BCA']
comparisons = {}
for kind, base in [('A', 'A-return'), ('B', 'B-first'), ('C', 'C-first')]:
    selected = [r for r in cycles if r['label'].endswith(kind)]
    comparisons[kind] = {
        'samples': len(selected),
        'sameUiaSequenceAsFirst': sum(r['uiaSequenceHash'] == by_label[base]['uiaSequenceHash'] for r in selected),
        'sameVisibleTextAsFirst': sum(r['visibleHashes'] == by_label[base]['visibleHashes'] for r in selected),
        'uiaCardCounts': sorted({r['uiaRootCards'] for r in selected}),
        'privateBytesMin': min(r['privateBytes'] for r in selected),
        'privateBytesMax': max(r['privateBytes'] for r in selected),
    }
summary = {'samples': len(rows), 'completedCycles': 10, 'businessPages': None, 'comparisons': comparisons,
           'maxUiaRootCards': max(r['uiaRootCards'] for r in rows),
           'maxPrivateBytes': max(r['privateBytes'] for r in rows),
           'maxWorkingSetBytes': max(r['workingSetBytes'] for r in rows),
           'meaning': 'UIA text/geometry and process proxies only; no VM, heap-root, request or budget claim'}
for before, after in [('off-top-before-nav', 'after-nav-return'), ('before-refresh', 'after-refresh'), ('after-refresh', 'refresh-idle')]:
    a, b = by_label[before], by_label[after]
    summary[f'{before} -> {after}'] = {
        'sameUiaSequence': a['uiaSequenceHash'] == b['uiaSequenceHash'],
        'sameVisibleTexts': a['visibleHashes'] == b['visibleHashes'],
        'firstVisibleYSame': a['visibleFirstY'] == b['visibleFirstY'],
    }
(root / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

# Standalone SVG chart, using sample ordinal rather than implying uniform time spacing.
chart_rows = [by_label['A-return']] + [by_label[f'cycle-{i}-A'] for i in range(1, 11)]
limit = max(r['workingSetBytes'] for r in chart_rows) / 1048576 * 1.1
def points(key):
    return ' '.join(f'{50+i*60:.1f},{240-r[key]/1048576/limit*200:.1f}' for i, r in enumerate(chart_rows))
ticks = ''.join(f'<text x="{50+i*60}" y="262" text-anchor="middle">{i}</text>' for i in range(11))
y_ticks = ''.join(f'<text x="44" y="{244-i*50}" text-anchor="end">{limit*i/4:.0f}</text>' for i in range(5))
html = f'''<!doctype html><html lang="zh"><meta charset="utf-8"><style>
:root{{--foreground:#ddd;--muted:#aaa;--chart-1:#56b4e9;--chart-2:#e69f00;--border:#777;--font-sans:system-ui}}
body{{font-family:var(--font-sans);color:var(--foreground)}}svg{{width:100%;height:290px}}text{{fill:var(--muted);font-size:12px}}
</style><p>真实完整应用：每轮返回推荐页的进程内存（MiB）</p>
<svg viewBox="0 0 700 290" role="img" aria-label="Private bytes and working set for initial return and ten cycles">
<path d="M50 40V240H650" fill="none" stroke="var(--border)"/>{ticks}{y_ticks}
<polyline points="{points('privateBytes')}" fill="none" stroke="var(--chart-1)" stroke-width="2"/>
<polyline points="{points('workingSetBytes')}" fill="none" stroke="var(--chart-2)" stroke-width="2"/>
</svg><p><span style="color:var(--chart-1)">● private bytes</span>　<span style="color:var(--chart-2)">● working set</span></p>
<p>横轴0为首次返回，1–10为回访轮次，非请求页数。瞬时全进程样本；不代表缓存独占内存、峰值、稳定预算或泄漏。</p></html>'''
(root / 'memory.html').write_text(html, encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False))
