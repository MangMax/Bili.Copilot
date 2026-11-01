"""Read package/PDB/source closure; build only the metadata reader, never engines."""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile

root = Path(__file__).resolve().parents[3]
source = root / 'docs/seek-responsive-fix'
output = root / 'artifacts/seek-responsive-integration/Closure'
sha = lambda b: hashlib.sha256(b).hexdigest()
for name, expected in json.loads((source/'manifest.json').read_text()).items():
    assert sha((source/name).read_bytes()) == expected, name
with zipfile.ZipFile(source/'source-snapshot.zip') as z:
    z.extractall(output/'snapshot')
for row in json.loads((source/'packages.json').read_text()):
    p = source/row['path']
    assert sha(p.read_bytes()) == row['sha256']
    with zipfile.ZipFile(p) as z:
        for n in z.namelist():
            if n.endswith(('.dll', '.pdb')):
                (output/Path(n).name).write_bytes(z.read(n))
cmd = ['C:/Program Files/dotnet/dotnet.exe', 'run', '--project', str(Path(__file__).parent/'audit-tool/Audit.csproj'), '--', str(output)]
with (output/'audit.log').open('wb') as log:
    subprocess.run(cmd, cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
sources = json.loads((source/'sources.json').read_text())
for name, info in sources.items():
    repo = output/name
    subprocess.run(['git', 'clone', '-q', str(source/(name+'.bundle')), str(repo)], check=True)
    def git(*args):
        return subprocess.check_output(['git', '-C', str(repo), *args])
    assert git('rev-parse', 'HEAD').decode().strip() == info['head']
    assert git('diff', info['base'], info['head']).replace(b'\r\n', b'\n') == (source/(name+'.patch')).read_bytes().replace(b'\r\n', b'\n')
count = 0
rows = json.loads((output/'metadata.json').read_text())
for assembly in rows:
    for doc in assembly['documents']:
        path = doc['source'].replace('\\', '/').split('/seek-fix/', 1)[1]
        if '/obj/' in path:
            continue
        repo, relative = path.split('/', 1)
        actual = subprocess.check_output(['git', '-C', str(output/repo), 'show', 'HEAD:'+relative])
        expected = (output/'source-documents'/(doc['hash']+'.source')).read_bytes()
        assert actual.replace(b'\r\n', b'\n') == expected.replace(b'\r\n', b'\n'), path
        count += 1
summary = {'command': cmd, 'dllPdbPairs': len(rows), 'productionDocumentsMatchedToBundle': count, 'sources': sources}
(output/'summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2))
