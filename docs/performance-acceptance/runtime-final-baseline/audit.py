"""Pre-run source/asset isolation and immediate frozen-byte audit."""
import difflib, hashlib, json, pathlib, subprocess
from prepare import ROOT, BASE, ledger, digest, save

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def normalized(p,arm):
    return p.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace(str(BASE/arm),'<ARM>').replace('net10.0','net11.0')

def main():
    result={'sourceDifferences':[], 'fixtureDifferences':[], 'packageDifferences':[], 'selectedAssetDifferences':[], 'arms':{}}
    for category,key in [('source','source-files.json'),('fixture','fixture-inputs.json')]:
        a={r['path']:r for r in read(BASE/'net10'/key)}
        b={r['path']:r for r in read(BASE/'net11'/key)}
        assert a.keys()==b.keys(),category
        for name in a:
            pa=BASE/'net10'/category/name;pb=BASE/'net11'/category/name
            assert digest(pa)==a[name]['sha256'] and digest(pb)==b[name]['sha256'],name
            if a[name]['sha256']==b[name]['sha256']:continue
            assert pa.suffix in ('.csproj','.json'), (category,name)
            if name=='global.json':
                assert read(pa)['sdk']['version']=='10.0.403' and read(pb)['sdk']['version']=='11.0.100-rc.3.26522.148'
            elif name=='source-hashes.json':
                assert read(pa)==read(pb)
            else: assert normalized(pa,'net10')==normalized(pb,'net11'),(category,name)
            result[category+'Differences'].append(name)
    graphs={}
    for arm in ('net10','net11'):
        area=BASE/arm;frozen=area/'fixture/frozen-output'
        expected=read(area/'frozen-files.json');assert ledger(frozen)==expected
        g=read(area/'package-graph.json');graphs[arm]=g
        packages={k:v for k,v in g['libraries'].items() if v['type']=='package'}
        assert len(packages)==(138 if arm=='net10' else 134),len(packages)
        assert 'H.NotifyIcon.WinUI/2.4.1' in packages
        runtime=read(frozen/'BiliCopilot.UI.runtimeconfig.json')
        result['arms'][arm]={'frozenFiles':len(expected),'packages':len(packages),
            'graphSha256':digest(area/'package-graph.json'),'sourceLedgerSha256':digest(area/'source-files.json'),
            'fixtureLedgerSha256':digest(area/'fixture-inputs.json'),'frozenLedgerSha256':digest(area/'frozen-files.json'),
            'coreclrSha256':digest(frozen/'coreclr.dll'),'runtime':runtime}
        text=(area/'fixture/IdleBaseline.cs').read_text()
        assert 'process.Refresh' not in text and 'GetCurrentProcess' not in text
        for name in subprocess.check_output(['git','ls-files','src/Desktop/BiliCopilot.UI'],cwd=ROOT,text=True).splitlines():
            rel=pathlib.Path(name).relative_to('src/Desktop/BiliCopilot.UI')
            if str(rel).replace('\\','/') in {'App.xaml.cs','GlobalDependencies.cs','BiliCopilot.UI.csproj','Package.appxmanifest','Package.Dev.appxmanifest','Toolkits/FontCatalog.cs'}:continue
            original=ROOT/name; copied=area/'fixture'/rel
            if digest(original)!=digest(copied):
                # Historical preparation uses Set-Content on top-level text files;
                # preserve raw hashes, but permit its terminal-newline/BOM change.
                assert original.suffix in ('.cs','.appxmanifest','.ps1')
                assert original.read_text(encoding='utf-8-sig').rstrip('\n')==copied.read_text(encoding='utf-8-sig').rstrip('\n'),name
        source=(ROOT/'src/Desktop/BiliCopilot.UI/App.xaml.cs').read_text(encoding='utf-8-sig')
        fixture=(area/'fixture/App.xaml.cs').read_text(encoding='utf-8-sig')
        expectedApp=source.replace('Richasy.BiliCopilot"','DependencyUpgrade.Final4F1"').replace('GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);','InitializeTrayLifecycle(); Offline48.Start();')
        assert fixture.strip()==expectedApp.strip(),'Unexpected App change'
    a=graphs['net10'];b=graphs['net11']
    for k in sorted(set(a['libraries'])|set(b['libraries'])):
        av=a['libraries'].get(k);bv=b['libraries'].get(k)
        if av!=bv:result['packageDifferences'].append({'name':k,'net10':av,'net11':bv})
    expectedExtra={f'Microsoft.Extensions.{name}/10.0.0' for name in ('DependencyInjection.Abstractions','Logging.Abstractions','Options','Primitives')}
    assert {r['name'] for r in result['packageDifferences']}==expectedExtra,result['packageDifferences']
    assert all(r['net10'] and r['net11'] is None for r in result['packageDifferences'])
    for rid in ('','/win-x64'):
        at=next(v for k,v in a['targets'].items() if k.endswith(rid) and ('/' in k)==bool(rid))
        bt=next(v for k,v in b['targets'].items() if k.endswith(rid) and ('/' in k)==bool(rid))
        for k in sorted(set(at)|set(bt)):
            if at.get(k)!=bt.get(k):result['selectedAssetDifferences'].append({'rid':rid,'name':k,'net10':at.get(k),'net11':bt.get(k)})
    save(BASE/'preflight.json',result)
    print(json.dumps({'arms':result['arms'],'sourceDifferences':result['sourceDifferences'],'fixtureDifferences':result['fixtureDifferences'],'assetDeltaCount':len(result['selectedAssetDifferences'])},indent=2))
if __name__=='__main__':main()
