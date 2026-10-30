"""Validate the complete parent freeze; copy payload without touching old registration/data."""
import hashlib, json, pathlib, shutil, subprocess, re, datetime
root = pathlib.Path(__file__).resolve().parents[3]
source = root.parent / 't3-1d19cc80/artifacts/integration-net11-player-timer-fix/be7105b003470782c74fbc8aaac0ea6a23a44a60/Release'
out = root / 'artifacts/full-app-net11'
evidence = pathlib.Path(__file__).resolve().parent
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
rows = json.loads((source/'files.json').read_text())
assert {str(p.relative_to(source)) for p in source.rglob('*') if p.is_file()} - {'files.json'} == {r['path'] for r in rows}
for r in rows:
    p = source/r['path']
    assert p.stat().st_size == r['bytes'] and sha(p) == r['sha256'], r['path']
assert not subprocess.check_output(['git','diff','be7105b','HEAD','--','src','tests','global.json','externals'],cwd=root)
activation = source/'activation'
assert not list(activation.rglob('*.appxrecipe'))
runtime = json.loads((activation/'BiliCopilot.UI.runtimeconfig.json').read_text())
assert runtime['runtimeOptions']['includedFrameworks'][0]['version'] == '11.0.0-rc.3.26522.148'
assert sha(activation/'coreclr.dll') == '46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2'
layout = out/'layout'
assert not layout.exists()
shutil.copytree(activation,layout)
p = layout/'AppxManifest.xml'
before = sha(p)
text = p.read_text(encoding='utf-8-sig')
text, count = re.subn(r'<Identity\s+[^>]+/>', '<Identity Name="Mang.BiliCopilot.PerfAcceptance.Caa6a7be" Publisher="CN=BiliCopilotDev" Version="2.2610.0.1" ProcessorArchitecture="x64" />', text)
assert count == 1
p.write_text(text,encoding='utf-8')
expected = {str(p.relative_to(activation)):sha(p) for p in activation.rglob('*') if p.is_file()}
actual = {str(p.relative_to(layout)):sha(p) for p in layout.rglob('*') if p.is_file()}
assert expected.keys() == actual.keys()
assert [k for k in expected if expected[k] != actual[k]] == ['AppxManifest.xml']
record = dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), source=str(source), layout=str(layout), freezeFiles=len(rows), activationFiles=len(actual), sourceCommit='be7105b003470782c74fbc8aaac0ea6a23a44a60', testedSourceEquivalentTo='043d1e8a87f67e0e56967a2bfc536818c2bd4282', kernel='0ae5e4e000a41a07f6696746706df11c06e8835b', kernelLocalOnly=True, runtime=runtime, manifestBefore=before, manifestAfter=sha(p), files=actual)
(evidence/'provenance.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in record.items() if k not in ('files','runtime')}))
