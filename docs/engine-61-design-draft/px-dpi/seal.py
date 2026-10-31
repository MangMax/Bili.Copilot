import pathlib, subprocess, hashlib, json, gzip, shutil
root = pathlib.Path(__file__).resolve().parents[3]
here = pathlib.Path(__file__).resolve().parent
out = root/'artifacts/px-dpi-parent-fix'
source = out/'source'
sha = subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
assert not subprocess.check_output(['git','-C',str(source),'status','--porcelain'],text=True)
subprocess.run(['git','-C',str(source),'archive','--format=zip','-o',str(out/'candidate-source.zip'),'HEAD'],check=True)
evidence = here/'evidence'
evidence.mkdir(exist_ok=True)
hashes = {}
for stage in ['red','green']:
    for name in ['build.txt','run.txt','results.json']:
        data = (out/stage/name).read_bytes()
        target = evidence/(stage+'-'+name+'.gz')
        target.write_bytes(gzip.compress(data,mtime=0))
        assert gzip.decompress(target.read_bytes()) == data
        hashes[stage+'/'+name] = hashlib.sha256(data).hexdigest()

shutil.copyfile(out/'candidate.patch',here/'candidate.patch')
files = subprocess.check_output(['git','-C',str(source),'ls-files'],text=True).splitlines()
manifest = dict(baseCommit='4a44888647eb781c4343de3512f5f70683bd2cdb',candidateCommit=sha,
                sourceRoot=str(source), artifacts={n:dict(path=str(out/n),sha256=hashlib.sha256((out/n).read_bytes()).hexdigest()) for n in ['candidate.bundle','candidate-source.zip','candidate.patch']},
                files={n:hashlib.sha256((source/n).read_bytes()).hexdigest() for n in files},evidence=hashes)
(here/'source.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(sha, len(files))
