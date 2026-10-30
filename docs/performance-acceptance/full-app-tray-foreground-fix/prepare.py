import pathlib,json,hashlib,shutil,subprocess,re,datetime
root=pathlib.Path.cwd(); evidence=root/'docs/performance-acceptance/full-app-tray-foreground-fix'; out=root/'artifacts/full-app-tray-foreground-fix'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
old=root.parent/'t3-4d615e72/artifacts/full-app-net11/layout-v2'
p=read(root/'docs/performance-acceptance/full-app-net11/provenance.json')['files']; p['AppxManifest.xml']=read(root/'docs/performance-acceptance/full-app-net11/verification.json')['deployedManifestSha256']
a={str(f.relative_to(old)):sha(f) for f in old.rglob('*') if f.is_file()};assert a==p
reg=read(evidence/'registration-before.json');assert pathlib.Path(reg['InstallLocation'])==old and reg['Version']=='2.2610.0.2'
source=root.parent/'t3-1d19cc80/artifacts/integration-tray-foreground-fix/334d47c78e04bebe836f6a8191b16859e6343843/Release'
summary=read(root/'docs/performance-acceptance/integration-tray-foreground-fix/summary.json');assert sha(source/'files.json')==summary['stages']['Release']['manifestSha256']
rows=read(source/'files.json');assert {str(f.relative_to(source)) for f in source.rglob('*') if f.is_file()}-{'files.json'}=={r['path'] for r in rows}
for r in rows:
 f=source/r['path'];assert f.stat().st_size==r['bytes'] and sha(f)==r['sha256'],r['path']
assert not subprocess.check_output(['git','diff','334d47c','HEAD','--','src','tests','global.json','externals'])
activation=source/'activation';layout=out/'layout-v3';assert not layout.exists();shutil.copytree(activation,layout)
m=layout/'AppxManifest.xml';text=m.read_text(encoding='utf-8-sig');text,n=re.subn(r'<Identity\s+[^>]+/>','<Identity Name="Mang.BiliCopilot.PerfAcceptance.Caa6a7be" Publisher="CN=BiliCopilotDev" Version="2.2610.0.3" ProcessorArchitecture="x64" />',text);assert n==1;m.write_text(text,encoding='utf-8')
before={str(f.relative_to(activation)):sha(f) for f in activation.rglob('*') if f.is_file()};after={str(f.relative_to(layout)):sha(f) for f in layout.rglob('*') if f.is_file()};assert before.keys()==after.keys();assert [k for k in before if before[k]!=after[k]]==['AppxManifest.xml']
record=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=str(source),layout=str(layout),sourceCommit='334d47c78e04bebe836f6a8191b16859e6343843',parent='1384779e798aba1a368bae2c566b976ff101a935',freezeFiles=len(rows),oldFiles=a,files=after,graph=summary['resolvedGraphs'][1],runtime=read(layout/'BiliCopilot.UI.runtimeconfig.json'))
(evidence/'provenance.json').write_text(json.dumps(record,indent=2));print('Old 670 hashes PASS; Release '+str(len(rows))+' bidirectional files PASS; new '+str(len(after))+' files, only manifest changed')
