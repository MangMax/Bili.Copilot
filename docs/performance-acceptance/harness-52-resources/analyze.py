"""Validate the paired experiment and inspect original pixels; never rewrite originals."""
import csv
import hashlib
import json
import statistics
import sys
from pathlib import Path

root = Path(sys.argv[1])
records = json.loads((root / 'records.json').read_text(encoding='utf-8-sig'))
assert not any('failure' in x for x in records), 'Experiment contains a failure'
samples = [x for x in records if x.get('experiment') == 'resources']
assert len(samples) == 84, f'Expected 6 pairs × 2 modes × 7 boundaries, got {len(samples)}'
rows = []
for pair in range(1, 7):
    for mode in ('limited', 'full'):
        group = {x['phase']: x for x in samples if x['pair'] == pair and x['mode'] == mode}
        assert set(group) == {'before', 'loaded-0', 'loaded-250', 'loaded-1000', 'cleared-0', 'cleared-250', 'cleared-1000'}
        for phase, sample in group.items():
            assert sample['dpi'] == 96 and sample['scale'] == 1
            if phase.startswith('loaded'):
                assert len(sample['images']) == 1
                im = sample['images'][0]
                assert (im['sourceWidth'], im['sourceHeight']) == (1200, 2400)
                assert abs(im['width'] - 240) < 1 and abs(im['height'] - 480) < 1
                assert (im['requestWidth'], im['requestHeight']) == ((240, 480) if mode == 'limited' else (1200, 2400))
            else:
                assert sample['images'] == []
        row = dict(pair=pair, mode=mode)
        for metric in ('privateBytes', 'workingSet', 'managed'):
            row[metric + '_loaded_delta'] = group['loaded-1000'][metric] - group['before'][metric]
            row[metric + '_cleared_delta'] = group['cleared-1000'][metric] - group['before'][metric]
        row['gen2_delta'] = group['cleared-1000']['gen2'] - group['before']['gen2']
        rows.append(row)
with (root / 'paired-deltas.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=rows[0]); writer.writeheader(); writer.writerows(rows)
summary = {'samples': len(samples), 'pairs': 6, 'forcedGC': False, 'metrics': {}}
for metric in ('privateBytes', 'workingSet', 'managed'):
    summary['metrics'][metric] = {}
    for mode in ('limited', 'full'):
        values = [x[metric + '_loaded_delta'] / 1048576 for x in rows if x['mode'] == mode]
        summary['metrics'][metric][mode] = dict(medianMiB=statistics.median(values), minMiB=min(values), maxMiB=max(values))
(root / 'resource-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')

for kind in ('path', 'text'):
    bodies = [(root / f'{kind}-{route}.utf8.bin').read_bytes() for route in ('MyImage', 'DefaultSVGRenderer', 'SvgImageSource')]
    assert bodies[0] == bodies[1] == bodies[2]
    assert (root / f'{kind}.http-latin1.bin').read_bytes().decode('latin-1') == bodies[0].decode('utf-8')
assert 'café' in (root / 'text-MyImage.txt').read_text(encoding='utf-8')

# Standalone SVG-in-HTML chart: paired process metrics, never native allocation estimates.
panels = []
for index, (metric, title) in enumerate([('privateBytes', 'Private bytes'), ('workingSet', 'Working set'), ('managed', 'Managed bytes')]):
    data = [r[metric + '_loaded_delta'] / 1048576 for r in rows]
    low, high = min(0, min(data)), max(0, max(data))
    span = max(1, high - low); low -= span * .12; high += span * .12
    def y(v): return 205 - (v - low) / (high - low) * 150
    parts = [f'<g transform="translate({index * 300},0)"><text x="40" y="24" class="title">{title}</text>']
    for v in [low, 0, high]:
        parts.append(f'<line x1="42" x2="288" y1="{y(v):.2f}" y2="{y(v):.2f}" stroke="#ddd"/><text x="0" y="{y(v)+4:.2f}">{v:.1f}</text>')
    for pair in range(1, 7):
        x = 56 + (pair - 1) * 43
        parts.append(f'<text x="{x}" y="228">{pair}</text>')
        vals = [next(r for r in rows if r['pair'] == pair and r['mode'] == mode)[metric + '_loaded_delta'] / 1048576 for mode in ('limited', 'full')]
        parts.append(f'<line x1="{x-4}" x2="{x+4}" y1="{y(vals[0]):.2f}" y2="{y(vals[1]):.2f}" stroke="#aaa"/>')
        for offset, value, color in zip([-4, 4], vals, ['#008080', '#c35712']):
            parts.append(f'<circle cx="{x+offset}" cy="{y(value):.2f}" r="4" fill="{color}"><title>Pair {pair}: {value:.3f} MiB</title></circle>')
    parts.append('</g>'); panels.append(''.join(parts))
html = '<!doctype html><meta charset="utf-8"><style>body{font:14px var(--font-sans,system-ui);margin:0;color:var(--foreground,#24313b)}svg{width:100%;height:245px}svg text{font:11px var(--font-sans,system-ui);fill:var(--foreground,#24313b)}.title{font-weight:700;font-size:14px}p{max-width:850px;line-height:1.5}</style><p>Loaded +1000 ms minus the matching before-load boundary (MiB). Six pairs; teal = production limited decode, orange = full-size fixture. Same 1200×2400 BMP, 240×480 DIP layout, 96 DPI.</p><svg viewBox="0 0 900 245">' + ''.join(panels) + '</svg><p>Pair order alternates. Process metrics include native, managed, JIT, HTTP, and composition activity; they do not attribute decoder allocations. Natural GC and allocator reuse make some deltas negative. No forced GC. Raw loaded and cleared samples: records.json.</p>'
(root / 'resource-chart.html').write_text(html, encoding='utf-8')
print(json.dumps(summary, indent=2))
