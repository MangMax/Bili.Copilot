from pathlib import Path
import hashlib,json,zipfile,subprocess,os,shutil
R=Path.cwd();O=R/'artifacts/combined-60cd';F=O/'full';P=O/'package-audit';P.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report={}
for lib in ['Core','Legacy','Share']:
 pkg=next((F/'feed').glob(f'Danmaku.{lib}.*.nupkg'))
 with zipfile.ZipFile(pkg) as z:
  n=next(n for n in z.namelist() if n.endswith(f'/Danmaku.{lib}.dll'));(P/Path(n).name).write_bytes(z.read(n));spec=z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))).decode('utf-8-sig')
  (P/f'{lib}.nuspec').write_text(spec,encoding='utf8')
 with zipfile.ZipFile(pkg.with_suffix('.snupkg')) as z:
  n=next(n for n in z.namelist() if n.endswith('.pdb'));(P/Path(n).name).write_bytes(z.read(n))
 report[lib]=dict(package=str(pkg),packageSha=sha(pkg),dllSha=sha(P/f'Danmaku.{lib}.dll'),pdbSha=sha(P/f'Danmaku.{lib}.pdb'))
tool=R.parent/'t3-9dc8c6c9/artifacts/px-dpi-review-v2/package-audit/tool/bin/Release/net11.0'
shutil.copytree(tool,P/'tool',dirs_exist_ok=True)
env=dict(os.environ,RX2_SOURCE_DOCUMENTS=str(P/'source-documents'))
cmd=['dotnet',str(P/'tool/Inspect.dll')]+[str(P/f'Danmaku.{n}.dll') for n in report]
with (P/'metadata.json').open('w',encoding='utf8') as f:p=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
(P/'packages.json').write_text(json.dumps(report,indent=2));print('PE/PDB/source matched for all three')
