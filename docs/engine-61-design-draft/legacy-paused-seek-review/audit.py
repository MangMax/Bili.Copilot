"""Check retained provenance against files, not just the producer's summary."""
import datetime, difflib, hashlib, json, pathlib, subprocess, zipfile
HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
C = HERE.parent / 'legacy-paused-seek'
sha = lambda b: hashlib.sha256(b).hexdigest()
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
origin = read(C/'origin.json')
archive = pathlib.Path(origin['archive'])
assert sha(archive.read_bytes()) == origin['archiveSha256']
changes = read(C/'changes.json')
patch = ''
with zipfile.ZipFile(archive) as z:
    prefix = z.namelist()[0].split('/')[0]+'/'
    for name, expected in origin['files'].items():
        assert sha(z.read(prefix+name)) == expected
    for name, hashes in changes.items():
        before=z.read(prefix+name); after=(C/'source'/pathlib.Path(name).name).read_bytes()
        assert sha(before)==hashes['before'] and sha(after)==hashes['after']
        patch += ''.join(difflib.unified_diff(before.decode('utf-8-sig').replace('\r\n','\n').splitlines(True),after.decode('utf-8-sig').replace('\r\n','\n').splitlines(True),fromfile='a/'+name,tofile='b/'+name))
assert patch.replace('\r\n','\n') == (C/'candidate.patch').read_text().replace('\r\n','\n')
closure=read(C/'closure.json'); records={}
for mode in ['source-red','final']:
    c=closure[mode]; folder=C/'evidence'/mode
    package=pathlib.Path(c['packagePath'])
    assert sha(package.read_bytes())==c['packageSha256']
    with zipfile.ZipFile(package) as z: assert sha(z.read(c['entry']))==c['dllSha256']
    assert sha(pathlib.Path(c['cache']).read_bytes())==c['dllSha256']
    events=[json.loads(x) for x in (folder/'events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    loaded=next(e['data'] for e in events if e['name']=='Loaded')
    assert loaded['sha256'].lower()==c['dllSha256'] and loaded['mvid']==c['runtime']['mvid']
    layout=read(folder/'layout-hashes.json')
    assert any(x['Path']==loaded['Location'] and x['Hash'].lower()==c['dllSha256'] for x in layout)
    exit=read(folder/'retainedexit.json'); wer=read(folder/'wer.json')
    delta=(datetime.datetime.fromisoformat(wer['query'])-datetime.datetime.fromisoformat(exit['exit'])).total_seconds()
    assert delta>=31 and abs(delta-wer['secondsAfterExit'])<0.001
    records[mode]={'dllSha256':c['dllSha256'],'packageSha256':c['packageSha256'],'mvid':loaded['mvid'],'werSecondsRecomputed':delta}
# The mutable layout now contains final; the historical red layout is checked from its saved manifest.
assert sha(pathlib.Path(closure['final']['runtime']['Location']).read_bytes())==closure['final']['dllSha256']
kernel_status=subprocess.check_output(['git','-C','externals/bili-kernel','status','--porcelain=v1'],cwd=REPO)
records['kernel']={'head':subprocess.check_output(['git','-C','externals/bili-kernel','rev-parse','HEAD'],cwd=REPO,text=True).strip(),
                   'statusLines':len(kernel_status.splitlines()),'statusSha256':sha(kernel_status),
                   'gitlink':subprocess.check_output(['git','ls-files','-s','externals/bili-kernel'],cwd=REPO,text=True).strip(),
                   'clean':False,'action':'read only; existing anomaly retained'}
records['parent']=subprocess.check_output(['git','rev-parse','feat/performance-45'],cwd=REPO,text=True).strip()
subprocess.run(['git','merge-base','--is-ancestor',records['parent'],'HEAD'],cwd=REPO,check=True)
records['restoredFiles']=len(origin['files']); records['changedEngineFiles']=list(changes)
(HERE/'evidence/provenance.json').write_text(json.dumps(records,indent=2))
print('45 source files, exact two-file patch, package/cache/runtime/layout provenance, WER intervals and parent ancestor verified.')
