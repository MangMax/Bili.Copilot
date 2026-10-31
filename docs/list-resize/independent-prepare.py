"""Bounded review host, own identity; no writes to source checkout."""
from pathlib import Path
import shutil, subprocess, sys
r=Path(__file__).resolve().parents[2]
h=r/'artifacts/list-review/host'
source=r/'artifacts/list-resize/green-extra-navigation/sources'
original=Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-6a3c2729/artifacts/list-resize/host')
mode=sys.argv[1]
s=(r/'docs/list-resize/run.ps1').read_text(encoding='utf-8-sig')
s=s.replace("'ImageChecks'","'ImageChecks','Focus'",1).replace('artifacts/list-resize','artifacts/list-review').replace('BiliCopilot.ListResize6a3c.Owned','BiliCopilot.ListReview3301.Owned').replace('list-resize-6a3c','list-review-3301').replace('AddMinutes(12)','AddSeconds(45)')
s=s[:s.index('if(Test-Path "$out/matrix-error.txt")')]+ 'if(Test-Path "$out/matrix-focus.json"){Get-Content "$out/matrix-focus.json"}\n'
(r/'docs/list-resize/independent-run.ps1').write_text(s)
if not h.exists():
    shutil.copytree(source,h)
    shutil.copytree(original/'Assets',h/'Assets')
    for name in ('Package.appxmanifest','app.manifest'):
        shutil.copy2(original/name,h/name)
    for p in h.iterdir():
        if p.suffix in ('.csproj','.props','.appxmanifest'):
            s=p.read_text(encoding='utf-8-sig').replace('t3-6a3c2729','t3-33017109').replace('BiliCopilot.ListResize6a3c.Owned','BiliCopilot.ListReview3301.Owned')
            p.write_text(s,encoding='utf-8')
    p=h/'App.xaml.cs';s=p.read_text(encoding='utf-8-sig')
    s=s.replace('Add("Close",()=>window.Close());','Add("Focus",()=>_ = RunFocus()); Add("Close",()=>window.Close());')
    p.write_text(s,encoding='utf-8')
    shutil.copy2(r/'docs/list-resize/IndependentFocus.cs',h/'IndependentFocus.cs')
(h/'Directory.Build.props').write_text(f'<Project><PropertyGroup><SourceRoot>{r.as_posix()}</SourceRoot></PropertyGroup></Project>')
for name in ('BasicCoverImage.cs','BlurCoverImage.cs'):
    ref='90e215f0' if mode=='baseline' else '1797c94a'
    (h/name).write_bytes(subprocess.check_output(['git','show',f'{ref}:src/Desktop/BiliCopilot.UI/Controls/{name}'],cwd=r))
if len(sys.argv)>2 and sys.argv[2]=='user50':
    p=h/'Matrix.cs';s=(source/'Matrix.cs').read_text(encoding='utf-8-sig')
    s=s.replace('new[]{"Recommend","Hot","Partition","Search","Live","Article","User","Stack"}','new[]{"User"}').replace('new[]{50,500}','new[]{50}')
    p.write_text(s,encoding='utf-8')
    p=h/'BasicCoverImage.cs';s=p.read_text(encoding='utf-8-sig')
    s=s.replace('var width = canvasBitmap.Size.Width;','Acceptance5053.ListProbe.Draws++;\n        var width = canvasBitmap.Size.Width;')
    s=s.replace('private void DrawBlurImage(','''protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Measures++; return base.MeasureOverride(s); }
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Arranges++; return base.ArrangeOverride(s); }
    private void DrawBlurImage(''')
    p.write_text(s,encoding='utf-8')
(h/'FocusMode.cs').write_text('namespace Acceptance5053; public partial class App { const bool ReviewGreen = '+str(mode!='baseline').lower()+'; }')
print(h,mode)
