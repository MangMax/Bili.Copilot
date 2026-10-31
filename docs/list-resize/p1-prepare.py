"""Reproduce fixed review sources with a worktree-owned identity, one variable per mode."""
from pathlib import Path
import shutil, subprocess, sys

r = Path(__file__).resolve().parents[2]
h = r / 'artifacts/list-p1/host'
mode = sys.argv[1]
cache = 0.5 if mode == 'final' else (1 if mode == 'cache1' else 2)
if not h.exists():
    shutil.copytree(r / 'artifacts/list-review/host', h)
    for p in h.iterdir():
        if p.suffix in ('.csproj', '.props', '.appxmanifest'):
            s = p.read_text(encoding='utf-8-sig').replace('t3-33017109', r.name).replace('BiliCopilot.ListReview3301.Owned', 'BiliCopilot.ListP14011.Owned')
            p.write_text(s, encoding='utf-8')
for p in (r/'artifacts/list-review/host').iterdir():
    if p.suffix in ('.cs', '.xaml'):
        shutil.copy2(p, h/p.name)
for p in h.glob('Diagnostic-*'):
    if p.is_file():
        p.unlink()
if (h/'DiagnosticImage.xaml').exists():
    (h/'DiagnosticImage.xaml').unlink()
for name in ('P1Undo.cs', 'P1Formats.cs', 'diagnostic-provenance.json'):
    if (h/name).exists():
        (h/name).unlink()
(h / 'Directory.Build.props').write_text(f'<Project><PropertyGroup><SourceRoot>{r.as_posix()}</SourceRoot></PropertyGroup></Project>')
for name in ('BasicCoverImage.cs', 'BlurCoverImage.cs'):
    ref = '90e215f0' if mode == 'baseline' else '1797c94a'
    s = subprocess.check_output(['git', 'show', f'{ref}:src/Desktop/BiliCopilot.UI/Controls/{name}'], cwd=r).decode('utf-8-sig')
    if name == 'BasicCoverImage.cs':
        s = s.replace('var width = canvasBitmap.Size.Width;', 'using var probe = Acceptance5053.P1Probe.Span("draw");\n        Acceptance5053.ListProbe.Draws++;\n        var width = canvasBitmap.Size.Width;')
    (h / name).write_text(s, encoding='utf-8')
(h / 'Models.cs').write_bytes((r / 'docs/list-resize/Models.cs').read_bytes())
if mode == 'small-source':
    subprocess.run(['powershell', '-NoProfile', '-File', str(r/'docs/list-resize/p1-small.ps1'), '-Assets', str(h/'Assets')], check=True)
    p = h / 'Models.cs'
    s = p.read_text(encoding='utf-8-sig')
    prefix, user = s.split('public partial class UserItemViewModel', 1)
    p.write_text(prefix + 'public partial class UserItemViewModel' + user.replace('"Cover.png"', '"Small.png"'), encoding='utf-8')
for format in ('jpg', 'webp'):
    if format in sys.argv[2:]:
        shutil.copy2(r/f'artifacts/list-p1/avatar-source/avatar96.{format}',h/f'Assets/avatar96.{format}')
        p=h/'Models.cs';s=p.read_text(encoding='utf-8-sig')
        prefix,user=s.split('public partial class UserItemViewModel',1)
        p.write_text(prefix+'public partial class UserItemViewModel'+user.replace('"Cover.png"',f'"avatar96.{format}"'),encoding='utf-8')
s = ((r / 'src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml').read_text(encoding='utf-8-sig') if mode == 'final' else subprocess.check_output(['git','show','1797c94a:src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml'],cwd=r).decode('utf-8-sig'))
(h / 'Repeater.xaml').write_text(s.replace('Value="2"', f'Value="{cache}"'), encoding='utf-8')
s = (r / 'docs/list-resize/Matrix.cs').read_text(encoding='utf-8-sig')
s = s.replace('(green?2:20)', f'(green?{cache}:20)')
if 'all' not in sys.argv[2:]:
    s = s.replace('new[]{"Recommend","Hot","Partition","Search","Live","Article","User","Stack"}', 'new[]{"User"}')
if '50' in sys.argv[2:]:
    s = s.replace('new[]{50,500}', 'new[]{50}')
s = s.replace('ListProbe.Measures=ListProbe.Arranges=ListProbe.Draws=0;', 'P1Probe.Start(); ir.ElementPrepared += Prepared; ir.ElementClearing += Clearing;\n                void Prepared(ItemsRepeater s, ItemsRepeaterElementPreparedEventArgs e) => P1Probe.Mark("prepared", e.Index);\n                void Clearing(ItemsRepeater s, ItemsRepeaterElementClearingEventArgs e) => P1Probe.Mark("clearing", -1);\n                ListProbe.Measures=ListProbe.Arranges=ListProbe.Draws=0;')
s = s.replace('await Task.Delay(800);timer.Stop();', 'await Task.Delay(800);timer.Stop(); ir.ElementPrepared -= Prepared; ir.ElementClearing -= Clearing; P1Probe.Stop(Path.Combine(output,$"matrix-events-{family}-{count}.json"));')
(h / 'Matrix.cs').write_text(s, encoding='utf-8')
(h / 'FocusMode.cs').write_text('namespace Acceptance5053; public partial class App { const bool ReviewGreen = '+str(mode != 'baseline').lower()+'; }')
(h / 'P1Probe.cs').write_text('''using System.Diagnostics;
using System.Text.Json;
namespace Acceptance5053;
public static class P1Probe {
 static readonly Stopwatch Clock = new(); static readonly List<object> Events = new();
 public static void Start() { Events.Clear(); Clock.Restart(); }
 public static void Mark(string kind,int index=0) { if(Clock.IsRunning) Events.Add(new {kind,index,ms=Clock.Elapsed.TotalMilliseconds}); }
 public static IDisposable Span(string kind) {Mark(kind+"-start"); return new End(kind);}
 sealed class End(string kind):IDisposable {public void Dispose()=>Mark(kind+"-end");}
 public static void Stop(string path) {Clock.Stop(); File.WriteAllText(path,JsonSerializer.Serialize(Events));}
}
''', encoding='utf-8')
s = (r / 'docs/list-resize/independent-run.ps1').read_text(encoding='utf-8-sig').replace('artifacts/list-review', 'artifacts/list-p1').replace('BiliCopilot.ListReview3301.Owned', 'BiliCopilot.ListP14011.Owned').replace('list-review-3301','list-p1-4011').replace('AddSeconds(45)', 'AddMinutes(8)')
s = s.replace("$ownedPath=$owned.Path", """$ownedPath=$owned.Path
    Get-ChildItem $layout -File | Where-Object Name -in 'Harness.dll','Harness.exe','Richasy.WinUIKernel.Share.dll','Microsoft.WinUI.dll' | Get-FileHash | ConvertTo-Json | Set-Content "$out/binary-hashes.json"
    git -C $root rev-parse HEAD | Set-Content "$out/head.txt"
"""
)
s += '''
if(Test-Path "$out/matrix-error.txt"){throw (Get-Content "$out/matrix-error.txt" -Raw)}
if(Test-Path "$out/matrix-checks.json"){
    $failed=@(Get-Content "$out/matrix-checks.json" -Raw | ConvertFrom-Json | Where-Object {$_ -like '*/FAIL'})
    if($failed.Count){Write-Output $failed; exit 2}
}
'''
(r / 'docs/list-resize/p1-run.ps1').write_text(s, encoding='utf-8')
print(h, mode)
