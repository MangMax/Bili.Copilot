"""Apply one reviewed dependency group from the captured inventory (exact versions)."""
import json,pathlib,re,sys
root=pathlib.Path(__file__).resolve().parents[3]
data=json.loads((root/'artifacts/dependency-upgrade/inventory.json').read_text(encoding='utf-8-sig'))
group=sys.argv[1]
def category(name):
    if any(x in name for x in ['Analyzers','analyzers','CodeAnalysis','SourceLink','CsWin32']): return 'tools'
    if any(x in name for x in ['WinUI','Win2D','WindowsAppSDK','Windows.SDK','WebView','ComputeSharp','Danmaku.','MpvKernel','Vortice','CsWinRT']): return 'native'
    return 'managed'
for row in data['rows']:
    if not row['references'] or row['area']=='tests' or category(row['id'])!=group: continue
    if row['area'].startswith('externals') and all('Samples' in p for p in row['references']): continue
    if group=='tools' and row['area'].startswith('externals'): continue
    meta=data['metadata'][row['id']]
    selected=meta.get('latestStable') or meta.get('latestAvailable')
    if not selected: print('BLOCKED metadata',row['id']);continue
    p=root/row['file']; text=p.read_text(encoding='utf-8-sig')
    pattern=r'((?:PackageVersion|PackageReference) Include="'+re.escape(row['id'])+r'" Version=")[^"]+(" )'
    text,count=re.subn(pattern,lambda m:m[1]+selected+m[2],text)
    if count: p.write_text(text,encoding='utf-8');print(row['id'],row['old'],'=>',selected)
