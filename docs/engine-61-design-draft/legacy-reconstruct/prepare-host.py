from pathlib import Path
import shutil,subprocess,sys
r=Path.cwd(); d=Path(__file__).resolve().parent
name=sys.argv[1]; code=sys.argv[2]; package=sys.argv[3] if len(sys.argv)>3 else 'clean'
o=r/'artifacts'/('reconstruct-'+name);o.mkdir()
base=r/'artifacts'/('reconstruct-'+package)
shutil.copytree(base/'host',o/'host',ignore=shutil.ignore_patterns('bin','obj'))
shutil.copyfile(d/code,o/'host/Program.cs')
old='BiliCopilot.Reconstruct0b4f'+package.replace('-','');new='BiliCopilot.Reconstruct0b4f'+name.replace('-','')
p=o/'host/Package.appxmanifest';p.write_text(p.read_text(encoding='utf-8-sig').replace(old,new),encoding='utf-8')
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(o/n).write_text('<Project/>')
(o/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{base / "feed"}"/><add key="offline" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
s=(base/'run.ps1').read_text(encoding='utf-8-sig').replace(old,new).replace("@('TOP','TOPSTABLE','BOTTOM','ROLL','REVERSE','BLANK','OTHER')","@()")
if code=='Regression.cs':s=s.replace('$phases=@()',"$phases=@('BACKWARD','REPEATED')").replace('-ne 18','-ne 12')
if code=='Extended.cs':s=s.replace('-ne 18','-ne 21')
if code=='EarlyClose.cs':s=s.replace('-ne 18','-ne 5')
(o/'run.ps1').write_text(s,encoding='utf-8-sig')
with (o/'build.log').open('w',encoding='utf-8') as f:p=subprocess.run(['dotnet','build',str(o/'host/Spike.csproj'),'-c','Release','-p:Platform=x64','-p:NuGetAudit=false','--configfile',str(o/'NuGet.Config')],stdout=f,stderr=subprocess.STDOUT)
print('build',p.returncode);p.check_returncode()
