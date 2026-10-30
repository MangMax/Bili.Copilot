import pathlib, json, hashlib, sys, runpy
root=pathlib.Path(sys.argv[1]); repo=pathlib.Path(__file__).resolve().parents[3]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=[]
for folder in sorted(root.glob('final*')):
    result=read(folder/'result.json'); assert result['error'] is None and result['processAbsent'] and result['packageAbsent']
    provenance=read(folder/'provenance.json'); project=pathlib.Path(provenance['project']).parent
    assert sha(pathlib.Path(provenance['builtDll']))==provenance['dllHash']==sha(pathlib.Path(provenance['layout'])/'Harness.dll')
    assert sha(project/'Experiment.cs')==sha(pathlib.Path(__file__).parent/'Experiment.cs')
    assert (project/'Themes/Generic.xaml').read_bytes()==(repo/'src/Desktop/BiliCopilot.UI/Controls/Markdown/MarkdownTextBlock.xaml').read_bytes()
    mode='historical' if 'historical' in folder.name else 'current'
    count=0
    for copied in (project/'source/src/Desktop/BiliCopilot.UI/Controls/Markdown').rglob('*'):
        if not copied.is_file():continue
        relative=copied.relative_to(project/'source'); original=repo/relative
        data=copied.read_bytes()
        if mode=='historical' and copied.name=='MarkdownTextBlock.xaml.cs':
            assert data.count(b'        GC.Collect();\r\n')==1
            data=data.replace(b'        GC.Collect();\r\n',b'')
        assert data==original.read_bytes(),str(relative)
        count+=1
    records=read(folder/'records.json')
    assert sum('cycle' in x for x in records)==50
    assert sum(x.get('name')=='WarmupRender' for x in records)==5
    assert not any(x.get('pass') is False or 'failure' in x for x in records)
    modules=read(folder/'modules.json')
    assert any(m['name']=='Harness.dll' and m['hash']==provenance['dllHash'] for m in modules)
    rows.append(dict(run=folder.name,sourceFiles=count,dllHash=provenance['dllHash'],hostHash=sha(project/'Experiment.cs'),imageHash=sha(project/'Assets/Logo.png'),fixtureHash=sha(folder/'fixture.md'),assertions=sum('pass' in x for x in records),readinessRechecks=sum(x.get('boundary')=='ReadinessRecheck' for x in records),runtime=[m for m in modules if m['name']=='coreclr.dll'],cleanup=result))
assert len(rows)==8
assert len({r['imageHash'] for r in rows})==len({r['fixtureHash'] for r in rows})==1
(root/'verification.json').write_text(json.dumps(rows,indent=2))
print('Verified eight runs: source byte equivalence except the historical GC line, loaded/build DLL hashes, fixture hashes, 50 cycles, 5 warmups, assertions, and cleanup.')
