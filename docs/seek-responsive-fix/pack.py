from pathlib import Path
import subprocess,json,sys
R=Path.cwd();O=R/'artifacts/seek-fix';V='0.0.1-seekd1fc.20261101.2'
feed=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else R/'docs/seek-responsive-fix/candidate-feed'
assert not any(feed.glob('*'+V+'*')), 'Never overwrite a sealed version; select a new output directory'
feed.mkdir(parents=True,exist_ok=True)
for repo,proj in [('legacy','Danmaku.Share'),('legacy','Danmaku.Legacy'),('core','Danmaku.Core')]:
 cmd=['dotnet','pack',str(O/repo/'src'/proj/(proj+'.csproj')),'-c','Release','-p:Platform=x64','-p:PackageVersion='+V,'-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false','-o',str(feed)]
 with (O/('pack-'+proj+'.log')).open('w',encoding='utf8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
 print(proj,r.returncode,flush=True)
 if r.returncode:break
