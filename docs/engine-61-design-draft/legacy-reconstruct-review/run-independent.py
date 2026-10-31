"""After legacy-reconstruct/compat.py, build a managed seam and load sealed DLLs."""
from pathlib import Path
import shutil, subprocess, zipfile

O = Path('artifacts/reconstruct-final')
h = O / 'independent'
h.mkdir(exist_ok=True)
shutil.copyfile(O/'boundary/Boundary.csproj', h/'Independent.csproj')
shutil.copyfile(Path(__file__).with_name('Independent.cs'), h/'Program.cs')
p = subprocess.run(['dotnet','build',str(h),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false'],capture_output=True)
(O/'independent-build.log').write_bytes(p.stdout+p.stderr)
p.check_returncode()
b = h/'bin/x64/Release/net11.0-windows10.0.22621.0'
for name in ['Legacy','Share']:
    for ext,pack in [('dll','nupkg'),('pdb','snupkg')]:
        with zipfile.ZipFile(O/f'feed/Danmaku.{name}.0.0.1-reconstruct0b4f.final.{pack}') as z:
            (b/f'Danmaku.{name}.{ext}').write_bytes(z.read(next(n for n in z.namelist() if n.endswith(f'/Danmaku.{name}.{ext}'))))
p = subprocess.run(['dotnet',str(b/'Independent.dll')],capture_output=True)
(O/'independent-run.log').write_bytes(p.stdout+p.stderr)
print(p.stdout.decode(errors='replace'),p.stderr.decode(errors='replace'))
p.check_returncode()
