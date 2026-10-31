"""Fix configuration receipt TOCTOU; test-only scheduling hook never enters source."""
import pathlib, subprocess, shutil, json, hashlib, argparse
p=argparse.ArgumentParser();p.add_argument('--name',required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[3];here=pathlib.Path(__file__).resolve().parent
review=root.parent/'t3-eb8247e9';origin=root.parent/'t3-f7b39952'
out=root/'artifacts'/a.name;out.mkdir();source=out/'source'
m=json.loads((origin/'docs/engine-61-design-draft/ix/source.json').read_text())
bundle=pathlib.Path(m['artifacts']['candidate.bundle']['path'])
assert hashlib.sha256(bundle.read_bytes()).hexdigest()==m['artifacts']['candidate.bundle']['sha256']
subprocess.run(['git','-c','core.autocrlf=false','clone',str(bundle),str(source)],check=True)
subprocess.run(['git','-C',str(source),'checkout','--detach',m['candidateCommit']],check=True)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:(out/n).write_text('<Project />')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')
sdk=root.parent/'t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe'
probe=here/'RevisionProbe.cs'
for fixed in [False,True]:
    if fixed:
        f=source/'src/Danmaku.Core/CoreExecutionBridge.cs';s=f.read_text()
        old='lock (configurationGate) { config = configuration.Clone(); revision = configurationRevision; }'
        assert s.count(old)==1
        s=s.replace(old,'''lock (configurationGate)
            {
                // Bind the request and its snapshot in the same critical section.
                if (requestedConfiguration.HasValue && requestedConfiguration.Value != configurationRevision)
                    return CoreResourceOutcome.Superseded;
                config = configuration.Clone(); revision = configurationRevision;
            }''')
        f.write_text(s)
    stage=out/('green' if fixed else 'red');stage.mkdir()
    shutil.copytree(source,stage/'source',ignore=shutil.ignore_patterns('.git','bin','obj'))
    shutil.copytree(review/'artifacts/ix-regression/independent56/host',stage/'host',ignore=shutil.ignore_patterns('bin','obj'))
    core=stage/'source/src/Danmaku.Core'
    (core/'Rx2Friend.cs').write_text('[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Rx2Probe")]')
    f=core/'CoreExecutionBridge.cs';s=f.read_text()
    s=s.replace('    private readonly CoreResourceOwner owner;', '    internal Action<long?>? ReviewConfigurationChecked;\n    private readonly CoreResourceOwner owner;')
    s=s.replace('            var stamp = owner.Capture();','            ReviewConfigurationChecked?.Invoke(requestedConfiguration);\n            var stamp = owner.Capture();')
    f.write_text(s)
    f=stage/'host/Program.cs';s=f.read_text();assert ' File.WriteAllText' in s
    f.write_text(s.replace(' File.WriteAllText',' RevisionCases(Case);\n File.WriteAllText',1))
    shutil.copyfile(probe,stage/'host/RevisionProbe.cs')
    for args,log in [(['build',str(stage/'host/Fixture.csproj'),'-c','Release','-m:1','-p:Platform=x64','-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false'],'build.txt'),([str(stage/'host/bin/x64/Release/net11.0-windows10.0.22621.0/Rx2Probe.dll'),str(stage/'results.json')],'run.txt')]:
        with (stage/log).open('w') as f:r=subprocess.run([str(sdk),*args],stdout=f,stderr=subprocess.STDOUT,timeout=300)
        if log=='build.txt':assert r.returncode==0
    rows=json.loads((stage/'results.json').read_text());assert len(rows)==len({x['name'] for x in rows})==59
    failed=[x['name'] for x in rows if not x['passed']]
    assert failed==([] if fixed else ['IX_ConfigurationChangesBetweenIdentityCheckAndSnapshot','IX_DebugChangesBetweenIdentityCheckAndSnapshot','IX_LayerChangesBetweenIdentityCheckAndSnapshot']),failed
    assert r.returncode==(0 if fixed else 1)
    print(stage.name,len(rows)-len(failed),failed,flush=True)
subprocess.run(['git','-C',str(source),'diff','--check'],check=True)
with (out/'candidate.patch').open('wb') as f:subprocess.run(['git','-C',str(source),'diff','--binary',m['candidateCommit']],stdout=f,check=True)
