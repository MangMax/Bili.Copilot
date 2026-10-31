"""Rebuild the archived focused native host in this checkout; never writes the kernel worktree."""
from pathlib import Path
import argparse,io,zipfile,subprocess,uuid,json,hashlib,re
p=argparse.ArgumentParser();p.add_argument('--kernel-repo',required=True,type=Path);p.add_argument('--ref',default='HEAD');p.add_argument('--identity',default='BiliCopilot.Recycle'+uuid.uuid4().hex[:8]+'.Owned');args=p.parse_args()
if not re.fullmatch(r'BiliCopilot\.Recycle[A-Za-z0-9]+\.Owned',args.identity):raise SystemExit('Use a unique BiliCopilot.Recycle*.Owned identity.')
r=Path(__file__).resolve().parents[3];a=r/'artifacts/recycled-cards';h=a/'host';s=a/'source'
if a.exists():raise SystemExit('Use a fresh checkout: artifacts/recycled-cards already exists; evidence is not overwritten.')
h.mkdir(parents=True);s.mkdir()
with zipfile.ZipFile(r/'docs/list-resize/recycled-cards-evidence.zip') as z:
 expected=json.loads(z.read('recycled-cards/production-inputs.normalized.sha256.json'))
 for name in z.namelist():
  prefix='recycled-cards/replay/host/'
  if name.startswith(prefix) and not name.endswith('/'):
   out=h/name[len(prefix):];out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(z.read(name))
zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','archive','--format=zip',args.ref,'src'],cwd=r))).extractall(s)
for name,digest in expected.items():
 if hashlib.sha256((s/name).read_bytes()).hexdigest()!=digest:raise SystemExit('Source mismatch: check out the repair commit: '+name)
k=s/'externals/bili-kernel';k.mkdir(parents=True)
zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','-C',str(args.kernel_repo),'archive','--format=zip','0ae5e4e000a41a07f6696746706df11c06e8835b']))).extractall(k)
(h/'Directory.Build.props').write_text('<Project><PropertyGroup><SourceRoot>'+s.as_posix()+'</SourceRoot></PropertyGroup></Project>',encoding='utf-8')
# Whole-object bindings require no reflection property paths or added runtime dependency.
print('Prepared',h)

manifest=h/'Package.appxmanifest';manifest.write_text(manifest.read_text(encoding='utf-8').replace('BiliCopilot.Recycle9F8D.Owned',args.identity),encoding='utf-8')
