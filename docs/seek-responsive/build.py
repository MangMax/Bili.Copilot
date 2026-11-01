"""Build the isolated supplementary host, without registration or launch."""
from pathlib import Path
import json, subprocess, shutil
R=Path.cwd(); O=R/'artifacts/seek-responsive'; F=O/'full'
command=['dotnet','build',str(F/'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj'),'-c','Release','-p:Platform=x64','-p:PublishAot=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false','-p:NuGetAudit=false',f'-p:SolutionDir={F}/src/','--configfile',str(F/'NuGet.Config')]
with (O/'build-reproduced.log').open('w',encoding='utf8') as log:
 result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
(O/'build-reproduced.json').write_text(json.dumps(dict(command=command,exit=result.returncode),indent=2))
assert result.returncode==0
for name in ['launch-retain.ps1','cleanup.ps1']:
 shutil.copyfile(Path(__file__).resolve().parent/name,O/name)
