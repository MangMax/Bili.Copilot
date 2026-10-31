"""Compile unchanged production method bodies; replace only external collaborators.
No native code, timing sleeps, or copy of the lifecycle algorithm in the tests.
"""
import pathlib, subprocess, sys, json, hashlib
root = pathlib.Path.cwd()
dest = root / 'artifacts/speed-close' / sys.argv[1]
dest.mkdir(parents=True, exist_ok=True)
source = root / 'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel'
def method(text, signature):
    start = text.index(signature)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]
life = (source / 'PlayerViewModel.cs').read_text(encoding='utf-8-sig')
commands = (source / 'PlayerViewModel.Commands.cs').read_text(encoding='utf-8-sig')
bodies = [method(life, s) for s in ['public Task CloseAsync()', 'private async Task TryCleanupAsync(', 'public async ValueTask DisposeAsync()', 'private async Task DisposeResourcesAsync()']]
bodies += [method(commands, 'private async Task IncreaseSpeedAsync()')]
# CapturePlaybackValidity is compiled once from the integrated Speed partial below.
speed = source / 'PlayerViewModel.Speed.cs'
if speed.exists():
    (dest / 'Speed.cs').write_text(speed.read_text(encoding='utf-8-sig'), encoding='utf-8')
(dest / 'Production.cs').write_text('using BiliCopilot.UI.ViewModels.Core;\nnamespace BiliCopilot.UI.ViewModels.Core;\npublic sealed partial class PlayerViewModel {\n' + '\n'.join(bodies) + '\n}', encoding='utf-8')
for name in ['Adapters.cs', 'Tests.cs']:
    (dest / name).write_bytes((pathlib.Path(__file__).parent / name).read_bytes())
(dest / 'Probe.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0"/><PackageReference Include="xunit" Version="2.9.3"/><PackageReference Include="xunit.runner.visualstudio" Version="4.0.1"/></ItemGroup></Project>''')
(dest / 'source.json').write_text(json.dumps({str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in [source/'PlayerViewModel.cs', source/'PlayerViewModel.Commands.cs'] + ([speed] if speed.exists() else [])}, indent=2))
result = subprocess.run(['dotnet','test',str(dest/'Probe.csproj'),'-c','Release','--logger','trx;LogFileName=results.trx','--results-directory',str(dest)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
(dest/'run.log').write_text(result.stdout, encoding='utf-8')
print(result.stdout)
sys.exit(result.returncode)
