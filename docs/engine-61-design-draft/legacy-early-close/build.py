from pathlib import Path
import json,hashlib,zipfile,shutil,subprocess
r=Path.cwd(); d=r/'docs/engine-61-design-draft/legacy-early-close'; o=r/'artifacts/legacy-early-close'; o.mkdir(parents=True,exist_ok=True)
m=json.loads((d.parent/'legacy-paused-seek/origin.json').read_text()); a=Path(m['archive']); assert hashlib.sha256(a.read_bytes()).hexdigest()==m['archiveSha256']
with zipfile.ZipFile(a) as z:z.extractall(o/'extract')
s=next((o/'extract').iterdir())
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(o/n).write_text('<Project />')
for layer in ['legacy-ready','legacy-early-close']:
 for p in (d.parent/layer/'source').glob('*.cs'):shutil.copyfile(p,s/'src/Danmaku.Legacy'/p.name)
p=subprocess.run(['dotnet','build',str(s/'src/Danmaku.Legacy/Danmaku.Legacy.csproj'),'-c','Release','-p:Platform=x64','-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false'],capture_output=True)
(o/'build.log').write_bytes(p.stdout+p.stderr); print('build exit',p.returncode); print((p.stdout+p.stderr).decode(errors='replace')[-2000:]); raise SystemExit(p.returncode)
