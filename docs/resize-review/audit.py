import hashlib,json,pathlib,zipfile,xml.etree.ElementTree as ET,subprocess
root=pathlib.Path(__file__).resolve().parents[2]
src=root/'docs/resize-diagnosis'
out=root/'artifacts/resize-review'
with zipfile.ZipFile(src/'evidence.zip') as z:
    manifest=json.loads((src/'evidence-manifest.json').read_text())
    for f in manifest:
        b=z.read(f['path']); assert len(b)==f['bytes'] and hashlib.sha256(b).hexdigest()==f['sha256']
    def read(p): return json.loads(z.read(p).decode('utf-8-sig'))
    stages={}
    for stage in ['red-final','green-final','hot-final','green1','fixed-diagnostic']:
        rows=read(stage+'/resize-results.json'); values=[]
        for r in rows:
            assert len(r['resize'])==120 and r['count']==r['startCount']
            if stage.endswith('-final'): assert r['listUpdates']==0
            for name,seq in [('resize',r['resize']),('heartbeat',r['beats'])]:
                assert abs(max(seq)-r[name+'Max'])<1e-8
                assert abs(sorted(seq)[int((len(seq)-1)*.95)]-r[name+'P95'])<1e-8
            assert r['pass']==(max(r['beats'])<200 and r['realized']<100)
            values.append({k:r.get(k) for k in ['count','realized','Decoders','Pixels','Surfaces','NativePeak','Native','heartbeatMax','resizeP95','allocated','pass']})
        stages[stage]=values
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests={}; names={}
    for label in ['targeted','full']:
        tree=ET.fromstring(z.read('tests/'+label+'.trx'))
        tests[label]=tree.find('t:ResultSummary/t:Counters',ns).attrib
        results=tree.findall('t:Results/t:UnitTestResult',ns)
        assert all(r.attrib['outcome']=='Passed' for r in results)
        names[label]={r.attrib['testName'] for r in results}
    assert names['targeted']<=names['full'] and len(names['targeted'])==98 and len(names['full'])==572
    exit_records={s:read(s+'/exit.json') for s in ['red-final','green-final','hot-final']}
    for s,e in exit_records.items():
        assert e['absent'] and e['observedSeconds']>=31 and e['path'] and e['pid']>0 and read(s+'/wer.json')==[]
    report=dict(manifestVerified=len(manifest),stages=stages,tests=tests,targetedIsSubset=True,exitRecords=exit_records)
    (out/'audit.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))

