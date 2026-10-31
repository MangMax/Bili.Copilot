import pathlib, hashlib, json, xml.etree.ElementTree as ET
r=pathlib.Path.cwd(); o=r/'artifacts/speed-close/full'; base=pathlib.Path('src/Desktop/BiliCopilot.UI')
result={}
for folder in ['ViewModels/Core/PlayerViewModel','Controls/Core','Controls/Danmaku','ViewModels/Core/DanmakuRenderViewModel','Forms']:
    for p in (r/base/folder).glob('*'):
        if p.suffix not in ['.cs','.xaml']:continue
        rel=p.relative_to(r)
        assert p.read_bytes()==(o/rel).read_bytes(),str(rel)
        result[str(rel)]=hashlib.sha256(p.read_bytes()).hexdigest()
(o/'source-equality.json').write_text(json.dumps(result,indent=2))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
summary={}
for name,path,expected in [('red','red/results.trx',1),('controlled','final2/results.trx',8),('suite','suite/suite.trx',563)]:
    tree=ET.parse(r/'artifacts/speed-close'/path)
    counters=tree.find('.//t:Counters',ns).attrib
    rows=tree.findall('.//t:UnitTestResult',ns)
    assert len(rows)==expected and int(counters['executed'])==expected
    assert int(counters['failed'])==(1 if name=='red' else 0)
    summary[name]={'counters':counters,'tests':[{'name':x.attrib['testName'],'outcome':x.attrib['outcome']} for x in rows]}
(r/'artifacts/speed-close/test-validation.json').write_text(json.dumps(summary,indent=2))
print('Source equality and actual TRX discovery/outcomes verified.')
metadata=json.loads((o/'consumer-pdb.json').read_text(encoding='utf-8-sig'))[0]
hashes={d['hash'].lower() for d in metadata['documents']}
for rel,value in result.items():
    if rel.endswith('.cs'):assert value in hashes,rel
assert hashlib.sha256((o/'layout2/BiliCopilot.UI.dll').read_bytes()).hexdigest()==metadata['sha256'].lower()
live={}
for run in ['runtime','runtime2']:
    exit_info=json.loads((o/run/'retainedexit.json').read_text(encoding='utf-8-sig'))
    wer=json.loads((o/run/'wer.json').read_text(encoding='utf-8-sig'))
    assert exit_info['exitCode']==0 and not exit_info['forced']
    assert wer['secondsAfterExit']>=31 and wer['applicationLogReadable'] and not wer['matches']
    data=[json.loads(line)['data'] for line in (o/run/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert any(d['kind']=='speed-close-enter' and d['pending']==20 for d in data)
    assert any(d['kind']=='speed-close-done' and d['disposed'] and d['completed'] for d in data)
    assert sum(d['kind']=='initialized' for d in data)>=2
    assert any(d['kind']=='visual' and not d['panel'] for d in data)
    assert not any(d['kind'] in ['unhandled','startup-error','action-error'] for d in data)
    if run=='runtime2':
        for speed in [1.25,1.5,1.75]:
            assert any(d['kind']=='state' and d.get('speed')==speed for d in data),speed
    live[run]={'exit':exit_info,'wer':wer,'speedClose':True,'reopened':True,'unloaded':True}
(o/'acceptance-validation.json').write_text(json.dumps(live,indent=2))
print('PDB source checksums, layout identity, real speed states, teardown and delayed events verified.')
