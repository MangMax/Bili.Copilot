"""Read-only source, binary, trace and artifact provenance verification."""
import hashlib
import json
import pathlib
import sys

repo = pathlib.Path(__file__).resolve().parents[3]
evidence = pathlib.Path(sys.argv[1]).resolve()
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()
for row in read(evidence/'linked-source-hashes.json'):
    assert sha(repo/row['path']) == row['sha256'].upper(), row['path']
verified=[]
for name in sys.argv[2:]:
    folder=evidence/name
    p=read(folder/'provenance.json')
    layout=pathlib.Path(p['layout'])
    build=pathlib.Path(p['project']).parent
    for filename in ['App.xaml.cs','App.xaml','Adapters.cs','Fixture.cs','Harness.csproj']:
        assert sha(build/filename) == sha(pathlib.Path(__file__).parent/filename), filename
    assert p['pid'] != 9608
    assert sha(layout/'Harness.dll') == p['dllHash'] == sha(pathlib.Path(p['builtDll']))
    for m in read(folder/'modules.json'):
        assert sha(pathlib.Path(m['path'])) == m['hash'], m
    if (folder/'loaded-dependency-hashes.json').exists():
        for m in read(folder/'loaded-dependency-hashes.json'):
            assert sha(pathlib.Path(m['path'])) == m['hash'], m
    for m in read(folder/'dependency-hashes.json'):
        assert sha(layout/m['name']) == m['hash'], m
    for m in read(folder/'production-hashes.json'):
        assert sha(pathlib.Path(m['Path'])) == m['Hash'], m
    for m in read(folder/'fixture-hashes.json'):
        assert sha(pathlib.Path(m['Path'])) == m['Hash'], m
    trace=read(folder/'trace-hash.json')
    assert sha(pathlib.Path(trace['Path'])) == trace['Hash']
    result=read(folder/'result.json')
    assert result['processAbsent'] and result['packageAbsent'] and result['error'] is None
    verified.append({'run':name,'dll':p['dllHash'],'trace':trace['Hash'],'screenshots':len(list(folder.glob('*.png')))})
print(json.dumps({'verified':verified,'linkedSources':len(read(evidence/'linked-source-hashes.json'))}))
