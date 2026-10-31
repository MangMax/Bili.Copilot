"""Inventory production list containers and image uses; no app source edits."""
from pathlib import Path
import xml.etree.ElementTree as E

root = Path(__file__).resolve().parents[2]
ui = root / 'src/Desktop/BiliCopilot.UI'
local = lambda name: name.split('}')[-1]
lines = ['# Production list and image paths', '',
         'Generated from production XAML. This is a call-site inventory, not a claim of native test coverage.', '',
         '| Container file | Container | Layout | Template/card descendants |',
         '| --- | --- | --- | --- |']
images = []
for p in sorted(ui.rglob('*.xaml')):
    tree = E.parse(p)
    for n in tree.iter():
        kind = local(n.tag)
        if kind in ('AppRepeater', 'ItemsRepeater', 'ListView', 'GridView', 'ItemsView', 'ItemsControl', 'ListBox', 'TreeView'):
            children = sorted({local(x.tag) for x in n.iter() if 'Card' in local(x.tag) or 'Presenter' in local(x.tag) or 'Layout' in local(x.tag)})
            lines.append(f'| {p.relative_to(ui).as_posix()} | {kind} | {n.get("Layout", "inline/default")} | {", ".join(children)} |')
        if kind in ('BasicCoverImage', 'RecommendationCoverImage', 'BlurCoverImage', 'Image'):
            images.append(f'| {p.relative_to(ui).as_posix()} | {kind} | {n.get("Source", "")} | {n.get("DecodeWidth", "default")} | {n.get("Width", "auto")} × {n.get("Height", "auto")} / {n.get("CornerRadius", "0")} |')
lines += ['', '## Image semantics at each call site', '', '| File | Image | Source | DecodeWidth | Size / corner radius |', '| --- | --- | --- | --- | --- |', *images]
(Path(__file__).parent / 'inventory.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
