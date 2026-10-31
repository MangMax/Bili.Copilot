"""Local RX2 receipt fix; preserves original source and review evidence."""
import argparse, hashlib, json, pathlib, shutil, subprocess

p = argparse.ArgumentParser()
p.add_argument('--name', required=True)
a = p.parse_args()
root = pathlib.Path(__file__).resolve().parents[3]
review = root.parent/'t3-9cce6a26'
delivery = root.parent/'t3-f7aca006'
out = root/'artifacts'/a.name
out.mkdir()
base = '31074a3dddd955cb725a89da882135085e856675'
manifest = json.loads((delivery/'docs/engine-61-design-draft/rx2/source.json').read_text())
bundle = pathlib.Path(manifest['bundle'])
assert hashlib.sha256(bundle.read_bytes()).hexdigest() == manifest['bundleSha256']
source = out/'source'
subprocess.run(['git', '-c', 'core.autocrlf=false', 'clone', str(bundle), str(source)], check=True)
subprocess.run(['git', '-C', str(source), 'checkout', '--detach', base], check=True)
for n in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props']:
    (out/n).write_text('<Project />')
(out/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="C:/Users/hp/.nuget/packages"/></packageSources></configuration>')

cases = (review/'docs/engine-61-design-draft/rx2-review/ReviewCases.cs').read_text()
# Mirror both proven receipt races for Resume, without changing original cases.
mirror = cases[cases.index('            run('):cases.index('\n        }\n        finally')]
mirror = mirror.replace('Pause','@P@').replace('Resume','Pause').replace('@P@','Resume')
mirror = mirror.replace('pause','@p@').replace('resume','pause').replace('@p@','resume')
mirror = mirror.replace('TimeSpan.FromMilliseconds(100)', 'TimeSpan.Zero')
cases = cases.replace('\n        }\n        finally', '\n'+mirror+'\n        }\n        finally')
(out/'ReviewCases.cs').write_text(cases)
fixture = review/'artifacts/rx2-review-input/artifacts/rx2-input/fixed/docs/engine-61-design-draft/c1a/Fixture.csproj'
program = (delivery/'docs/engine-61-design-draft/rx2/Program.cs').read_text().replace('ExecutionCases(Case);', 'ExecutionCases(Case); ReviewCases(Case);')
dotnet = str(root.parent/'t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe')

def run(label, expected):
    stage = out/label
    stage.mkdir()
    shutil.copytree(source, stage/'source', ignore=shutil.ignore_patterns('.git','bin','obj'))
    (stage/'source/src/Danmaku.Core/Rx2Friend.cs').write_text('[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Rx2Probe")]')
    host = stage/'host'
    host.mkdir()
    (host/'Fixture.csproj').write_text(fixture.read_text().replace('<OutputType>', '<AssemblyName>Rx2Probe</AssemblyName><OutputType>'))
    (host/'Program.cs').write_text(program)
    (host/'ReviewCases.cs').write_text(cases)
    shutil.copyfile(delivery/'docs/engine-61-design-draft/rx2/ExecutionProbe.cs', host/'ExecutionProbe.cs')
    commands = [([dotnet,'build',str(host/'Fixture.csproj'),'-c','Release','-m:1','-p:Platform=x64','-p:TreatWarningsAsErrors=false','-p:NuGetAudit=false'],'build.txt'),
                ([dotnet,str(host/'bin/x64/Release/net11.0-windows10.0.22621.0/Rx2Probe.dll'),str(stage/'results.json')],'run.txt')]
    for command, log in commands:
        with (stage/log).open('w') as f:
            result = subprocess.run(command, stdout=f, stderr=subprocess.STDOUT, timeout=180)
        if log == 'build.txt': assert result.returncode == 0, stage/log
    rows = json.loads((stage/'results.json').read_text())
    assert len(rows) == len({x['name'] for x in rows}) == 25
    failures = [x['name'] for x in rows if not x['passed']]
    assert failures == expected, failures
    assert result.returncode == (1 if expected else 0)
    print(label, len(rows)-len(failures), failures, flush=True)

expected = ['Review_PauseQueuedThenResume_PauseMustBeSuperseded', 'Review_PauseCallbackResumes_PauseMustBeSuperseded',
            'Review_ResumeQueuedThenPause_ResumeMustBeSuperseded', 'Review_ResumeCallbackPauses_ResumeMustBeSuperseded']
run('red', expected)
f = source/'src/Danmaku.Core/DanmakuScheduler.cs'
s = f.read_text()
start = s.index('    private void Intent(')
end = s.index('    public void Pause()', start)
block = s[start:end].replace('private void Intent(', 'private long Intent(').replace('if (_isClosing || _fault != null) return;', 'if (_isClosing || _fault != null) return _intentRevision;')
block = block.rstrip()
assert block.endswith('}')
block = block[:-1]+'    return revision;\n    }\n    internal long PauseTracked() => Intent(false, PauseRequested);\n    internal long ResumeTracked() => Intent(true, ResumeRequested);\n'
f.write_text(s[:start]+block+s[end:])
f = source/'src/Danmaku.Core/CoreExecutionBridge.cs'
s = f.read_text()
for control in ['Pause','Resume']:
    start = s.index('    internal Task<CoreResourceOutcome> '+control+'Async()')
    end = s.index('\n    internal ',start+1)
    block = s[start:end].replace('try { scheduler.'+control+'(); }', 'long requestedIntent;\n        try { requestedIntent = scheduler.'+control+'Tracked(); }')
    block = block.replace('return FrameAsync();', 'return RenderAsync(null, null, requestedIntent);')
    s = s[:start]+block+s[end:]
s = s.replace('RenderAsync(SchedulerSelection? selection, Realtime? realtime)', 'RenderAsync(SchedulerSelection? selection, Realtime? realtime, long? requestedIntent = null)')
s = s.replace('if (state.Error != null) return CoreResourceOutcome.Faulted;', 'if (state.Error != null) return CoreResourceOutcome.Faulted;\n            // A control receipt cannot borrow a newer control\'s successful frame.\n            if (requestedIntent.HasValue && state.Intent != requestedIntent.Value) return CoreResourceOutcome.Superseded;')
f.write_text(s)
run('green', [])
subprocess.run(['git','-C',str(source),'diff','--check'],check=True)
with (out/'candidate.patch').open('wb') as f:
    subprocess.run(['git','-C',str(source),'diff','--binary',base],stdout=f,check=True)
print('Candidate source and red/green evidence:',out,flush=True)
