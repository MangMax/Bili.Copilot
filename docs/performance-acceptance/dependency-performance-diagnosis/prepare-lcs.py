"""Generate isolated, exact-source LCS variants; no production edits."""
import hashlib, json, pathlib, subprocess, sys
root = pathlib.Path(__file__).resolve().parents[3]
dest = pathlib.Path(sys.argv[1]).resolve()
dest.mkdir(parents=True, exist_ok=False)
path = 'src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs'
revisions = {'Rectangular':'eff8d24c87620cf2b6025a11b8f8ddda27b1a116',
             'Jagged':'dbea98d', 'Shortcut':'60abad7834f7037501fcb99696494d3dcebb8977'}
ledger = {}
for name, rev in revisions.items():
    sha = subprocess.check_output(['git','rev-parse',rev],cwd=root,text=True).strip()
    source = subprocess.check_output(['git','show',f'{sha}:{path}'],cwd=root)
    text = source.decode().replace('namespace BiliCopilot.UI.Toolkits;',f'namespace {name};')
    (dest/f'{name}.cs').write_text(text,encoding='utf-8')
    ledger[name] = dict(commit=sha,sourceSha256=hashlib.sha256(source).hexdigest(),
                        generatedSha256=hashlib.sha256((dest/f'{name}.cs').read_bytes()).hexdigest())
(dest/'source-ledger.json').write_text(json.dumps(ledger,indent=2))
(dest/'Program.cs').write_bytes((pathlib.Path(__file__).parent/'LcsProgram.cs').read_bytes())
(dest/'Lcs.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>
<RuntimeIdentifier>win-x64</RuntimeIdentifier><SelfContained>true</SelfContained>
<RuntimeFrameworkVersion>9.0.22</RuntimeFrameworkVersion>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
</PropertyGroup></Project>''')
(dest/'global.json').write_bytes((root/'global.json').read_bytes())
