from pathlib import Path
import subprocess,json
R=Path.cwd();O=R/'artifacts/seek-fix';F=O/'full'
cmd=['dotnet','build',str(F/'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj'),'-c','Release','-p:Platform=x64','-p:PublishAot=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false','-p:NuGetAudit=false',f'-p:SolutionDir={F}/src/','--configfile',str(F/'NuGet.Config')]
with (O/'build.log').open('w',encoding='utf8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
(O/'build.json').write_text(json.dumps(dict(command=cmd,exit=r.returncode),indent=2));print(r.returncode)
