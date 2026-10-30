import hashlib, json, pathlib, shutil, sys
source, target = map(lambda p: pathlib.Path(p).resolve(), sys.argv[1:3])
assert source.is_dir() and not target.exists()
def ext(p): return pathlib.Path('\\\\?\\' + str(p))
files = []
for p in source.rglob('*'):
    if not p.is_file(): continue
    relative = p.relative_to(source)
    destination = target / relative
    ext(destination.parent).mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ext(p), ext(destination))
    with ext(destination).open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    files.append(dict(path=str(relative), sha256=digest, size=p.stat().st_size))
target.with_suffix('.json').write_text(json.dumps(files, indent=2))
print(json.dumps(dict(frozen=str(target), files=len(files))))
