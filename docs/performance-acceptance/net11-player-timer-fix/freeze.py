import sys
import hashlib
import json
import pathlib
import shutil
import xml.etree.ElementTree as ET

base = pathlib.Path(sys.argv[1]).resolve()
fixture = base / 'fixture'
outputs = list((fixture / 'bin').rglob('AppxManifest.xml'))
assert len(outputs) == 1, outputs
output = outputs[0].parent
frozen = fixture / 'frozen-output'
assert not frozen.exists()
def extended(p):
    return pathlib.Path('\\\\?\\' + str(p.resolve()))
def copy(source, target):
    source, target = extended(source), extended(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
def digest(p):
    with extended(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
for p in output.rglob('*'):
    if p.is_file() and not p.name.endswith('.appxrecipe'):
        copy(p, frozen / p.relative_to(output))
recipes = list(output.glob('*.build.appxrecipe'))
assert len(recipes) == 1
extra = []
for item in ET.parse(recipes[0]).iter():
    if item.tag.split('}')[-1] != 'AppxPackagedFile':
        continue
    source = pathlib.Path(item.attrib['Include']).resolve()
    relative = next(x.text for x in item if x.tag.split('}')[-1] == 'PackagePath')
    dest = (frozen / relative).resolve()
    assert dest.is_relative_to(frozen)
    if dest.exists():
        continue
    assert source.is_relative_to(base) or source.is_relative_to(pathlib.Path.home() / '.nuget/packages'), source
    copy(source, dest)
    extra.append(dict(path=str(dest.relative_to(frozen)), source=str(source), sha256=digest(dest)))
files = [dict(path=str(p.relative_to(frozen)), sha256=digest(p), size=p.stat().st_size)
         for p in frozen.rglob('*') if p.is_file()]
(fixture / 'frozen-all-files.json').write_text(json.dumps(files, indent=2))
(fixture / 'frozen-content-provenance.json').write_text(json.dumps(extra, indent=2))
source_files = [dict(path=str(p.relative_to(fixture)), sha256=digest(p)) for p in fixture.rglob('*')
                if p.is_file() and not any(x in p.relative_to(fixture).parts for x in ('bin', 'obj', 'frozen-output'))]
(base / 'fixture-input-files.json').write_text(json.dumps(source_files, indent=2))
print(json.dumps(dict(files=len(files), dlls=sum(x['path'].lower().endswith('.dll') for x in files), recipeAdditions=len(extra))))

for p in base.rglob('project.assets.json'):
    dest = base / 'frozen-assets' / p.relative_to(base)
    if 'frozen-assets' not in p.parts: copy(p, dest)
