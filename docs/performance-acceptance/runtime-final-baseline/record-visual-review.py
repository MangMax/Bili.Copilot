"""Record the individually inspected screenshots, excluding packaged PNG assets."""
import json
from prepare import BASE, save, digest

rows=[]
for run in ('a1-net10','b1-net11','b2-net11','a2-net10'):
    for area in ('player','main'):
        for p in sorted((BASE/run/area).glob('*.png')):
            if run=='b2-net11' and p.name=='font-settings.png':
                note='Font flyout absent; structured font assertion passed but screenshot does not certify flyout visibility.'
            elif area=='player' and p.stem!='results':
                note='Owned synthetic player: white video, right-edge clipped danmaku where present; not real media quality evidence.'
            else:
                note='Owned controller, Settings page or subtitle font flyout visible. No account or signed media URL.'
            rows.append({'path':p.relative_to(BASE).as_posix(),'sha256':digest(p),'reviewed':True,'observation':note})
assert len(rows)==48
save(BASE/'visual-review.json',rows)
print('48 screenshots recorded after individual review')
