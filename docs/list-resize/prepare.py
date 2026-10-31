"""Extend the existing offline harness with real list-family templates."""
from pathlib import Path
import shutil, re, subprocess
r=Path(__file__).resolve().parents[2]
ui=r/'src/Desktop/BiliCopilot.UI'
h=r/'artifacts/list-resize/host'
if h.exists(): raise SystemExit('Fresh host required')
h.mkdir()
old=r/'artifacts/resize-diagnosis/host'
for p in old.iterdir():
    if p.is_file(): shutil.copy2(p,h/p.name)
shutil.copytree(old/'Assets',h/'Assets')
# The recommendation acceptance host may already have exercised this candidate.
# Red preparation must not inherit its global cache style or linked lifecycle helper.
for name in ('App.xaml','Harness.csproj'):
    p=h/name;s=p.read_text(encoding='utf-8-sig')
    s=s.replace('<ResourceDictionary Source="Repeater.xaml" />','')
    for control in ('ReloadableCoverImage','RepeaterCachePolicy'):
        s=re.sub(r'<Compile Include="[^\"]*/'+control+r'\.cs"[^>]*/>','',s)
    p.write_text(s,encoding='utf-8')
def edit(name,a,b):
    p=h/name;s=p.read_text(encoding='utf-8-sig');assert a in s,(name,a);p.write_text(s.replace(a,b),encoding='utf-8')
edit('Package.appxmanifest','BiliCopilot.Resize5a5e.Owned','BiliCopilot.ListResize6a3c.Owned')
edit('App.xaml.cs','Add("Close",()=>window.Close());','Add("MatrixRed",()=>_ = RunMatrix(false)); Add("MatrixGreen",()=>_ = RunMatrix(true)); Add("Close",()=>window.Close());')
# Both stages link the policy; only green loads its style. Candidate preparation
# separately replaces the two image classes, preserving identical instrumentation.
edit('Harness.csproj','</Project>','<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/RepeaterCachePolicy.cs" Link="Production/RepeaterCachePolicy.cs" /></ItemGroup></Project>')
sources=[]
for kind,presenters in [('Live',['RecommendLivePresenter']),('Article',['PartitionArticlePresenter'])]:
    folder=ui/f'Controls/Components/{kind}CardControl'
    sources += [folder/f'{kind}CardControl.cs']
    for name in presenters:
        shutil.copy2(folder/f'{name}.xaml',h/f'{name}.xaml')
        sources += [folder/f'{name}.xaml.cs']
    s=(folder/f'{kind}CardControl.xaml').read_text(encoding='utf-8-sig')
    # Keep exactly the production style for the selected presenter.
    key='RecommendLiveCardStyle' if kind=='Live' else 'PartitionArticleCardStyle'
    s=re.sub(r'    <Style x:Key="([^"]+)".*?</Style>',lambda m:m[0] if m[1]==key else '',s,flags=re.S)
    (h/f'{kind}Cards.xaml').write_text(s,encoding='utf-8')
    sources += [r/f'src/Desktop/BiliCopilot.UI.Models/Constants/{kind}CardStyle.cs']
sources += [ui/'Controls/Components/UserCardControl.xaml.cs']
shutil.copy2(ui/'Controls/Components/UserCardControl.xaml',h/'UserCardControl.xaml')
for p in sources:
    edit('Harness.csproj','</Project>',f'<ItemGroup><Compile Include="{p.as_posix()}" Link="{p.name}" /></ItemGroup></Project>')
s=(ui/'Controls/Components/VideoCardControl/VideoCardControl.xaml').read_text(encoding='utf-8-sig')
keys=['RecommendVideoCardStyle','HotVideoCardStyle','RankVideoCardStyle','PartitionVideoCardStyle','SearchVideoCardStyle','PlayerPlaylistVideoCardStyle']
s=re.sub(r'    <Style x:Key="([^"]+)".*?</Style>',lambda m:m[0] if m[1] in keys else '',s,flags=re.S)
(h/'Cards.xaml').write_text(s,encoding='utf-8')
templates=['<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:items="using:BiliCopilot.UI.ViewModels.Items" xmlns:components="using:BiliCopilot.UI.Controls.Components">']
for family,path in {'Partition':'VideoPartition/VideoPartitionMainBody','Search':'Search/VideoSectionDetailControl','Live':'LivePartition/LiveRecommendMainBody','Article':'Article/ArticlePartitionMainBody','User':'Search/UserSectionDetailControl','Stack':'Player/VideoPlaylistSection'}.items():
    text=(ui/f'Controls/{path}.xaml').read_text(encoding='utf-8-sig')
    template=re.search(r'<DataTemplate x:DataType=.*?</DataTemplate>',text,re.S)[0]
    templates.append(template.replace('<DataTemplate ',f'<DataTemplate x:Key="{family}" ',1))
for family in ['Recommend','Hot']:
    templates.append(f'<DataTemplate x:Key="{family}" x:DataType="items:VideoItemViewModel"><components:VideoCardControl Style="{{StaticResource {family}VideoCardStyle}}" ViewModel="{{x:Bind}}" /></DataTemplate>')
templates.append('</ResourceDictionary>')
(h/'MatrixTemplates.xaml').write_text('\n'.join(templates),encoding='utf-8')
edit('MatrixTemplates.xaml','<ResourceDictionary ','<ResourceDictionary x:Class="Acceptance5053.MatrixTemplates" ',)
(h/'MatrixTemplates.xaml.cs').write_text('namespace Acceptance5053; public sealed partial class MatrixTemplates : Microsoft.UI.Xaml.ResourceDictionary { public MatrixTemplates() { InitializeComponent(); } }',encoding='utf-8')
edit('App.xaml','<ResourceDictionary Source="Cards.xaml" />','<ResourceDictionary Source="Cards.xaml" /><ResourceDictionary Source="LiveCards.xaml" /><ResourceDictionary Source="ArticleCards.xaml" />')
edit('App.xaml','</ResourceDictionary>','<local:LevelConverter x:Key="LevelImageConverter" /></ResourceDictionary>')
edit('Strings.cs','RefreshingRecommendVideos,','Viewer, Follow, LikeCount, DanmakuCount, RefreshingRecommendVideos,')
# Genuine image sources for both covers and avatars; network still forbidden.
edit('Adapters.cs','public Uri Avatar=>null;','public Uri Avatar=>Cover;')
shutil.copy2(r/'docs/list-resize/Matrix.cs',h/'Matrix.cs')
shutil.copy2(r/'docs/list-resize/Models.cs',h/'Models.cs')
# Instrument only virtual callbacks on the real BasicCoverImage, with no layout queries or I/O.
edit('Harness.csproj','$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/BasicCoverImage.cs;','')
s=subprocess.check_output(['git','show','90e215f0:src/Desktop/BiliCopilot.UI/Controls/BasicCoverImage.cs'],cwd=r).decode('utf-8-sig').replace('var width = canvasBitmap.Size.Width;','Acceptance5053.ListProbe.Draws++;\n        var width = canvasBitmap.Size.Width;')
s=s.replace('private void DrawBlurImage(','''protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Measures++; return base.MeasureOverride(s); }
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Arranges++; return base.ArrangeOverride(s); }
    private void DrawBlurImage(''')
(h/'BasicCoverImage.cs').write_text(s,encoding='utf-8')
# Layouts copied verbatim from the application resources.
layouts=re.findall(r'    *<UniformGridLayout\s+.*?/>',(ui/'App.xaml').read_text(encoding='utf-8-sig'),re.S)
for s in layouts:
    key=re.search(r'x:Key="([^"]+)"',s)[1]
    if key not in (h/'App.xaml').read_text(encoding='utf-8'):
        edit('App.xaml','</ResourceDictionary>',s+'\n</ResourceDictionary>')
print(h)
