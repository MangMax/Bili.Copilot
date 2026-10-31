"""Add production dynamic, poster/blur, and ordinary notification list templates."""
from pathlib import Path
import re,shutil,sys,subprocess
r=Path(__file__).resolve().parents[2];ui=r/'src/Desktop/BiliCopilot.UI';h=r/'artifacts/list-resize/host'
def edit(name,a,b):
    p=h/name;s=p.read_text(encoding='utf-8-sig');assert a in s,(name,a);p.write_text(s.replace(a,b),encoding='utf-8')
def link(p):
    edit('Harness.csproj','</Project>',f'<ItemGroup><Compile Include="{p.as_posix()}" Link="{p.name}" /></ItemGroup></Project>')
for kind,presenter,key in [('Season','IndexSeasonPresenter','IndexSeasonCardStyle'),('Moment','VideoMomentPresenter','VideoMomentCardStyle')]:
    folder=ui/f'Controls/Components/{kind}CardControl'
    link(folder/f'{kind}CardControl.cs');link(folder/f'{presenter}.xaml.cs')
    link(r/f'src/Desktop/BiliCopilot.UI.Models/Constants/{kind}CardStyle.cs')
    shutil.copy2(folder/f'{presenter}.xaml',h/f'{presenter}.xaml')
    s=(folder/f'{kind}CardControl.xaml').read_text(encoding='utf-8-sig')
    s=re.sub(r'    <Style[^>]*?/>','',s)
    s=re.sub(r'    <Style x:Key="([^"]+)".*?</Style>',lambda m:m[0] if m[1]==key else '',s,flags=re.S)
    (h/f'{kind}Cards.xaml').write_text(s,encoding='utf-8')
    edit('App.xaml','<ResourceDictionary Source="Cards.xaml" />',f'<ResourceDictionary Source="Cards.xaml" /><ResourceDictionary Source="{kind}Cards.xaml" />')
for name in ['NotifyMessageCardControl']:
    link(ui/f'Controls/Message/{name}.xaml.cs')
    (h/f'{name}.xaml').write_bytes(subprocess.check_output(['git','show',f'90e215f0:src/Desktop/BiliCopilot.UI/Controls/Message/{name}.xaml'],cwd=r))
for path in ['Components/EmoteTextBlock','Components/MomentCardControl/MomentItemOperationPanel']:
    link(ui/f'Controls/{path}.xaml.cs');shutil.copy2(ui/f'Controls/{path}.xaml',h/f'{Path(path).name}.xaml')
# Keep red images pristine. Green builds later replace only the source base and add its lifecycle helper.
s=subprocess.check_output(['git','show','90e215f0:src/Desktop/BiliCopilot.UI/Controls/BlurCoverImage.cs'],cwd=r).decode('utf-8-sig')
(h/'BlurCoverImage.cs').write_text(s,encoding='utf-8')
link(ui/'Extensions/BlurCanvasEffect.cs')
for family,path in {'Moment':'Moment/VideoMomentSectionDetailControl','Season':'Pgc/EntertainmentIndexMainControl','Notify':'Message/NotifyMessageBody'}.items():
    text=(ui/f'Controls/{path}.xaml').read_text(encoding='utf-8-sig')
    template=re.search(r'<DataTemplate x:DataType=.*?</DataTemplate>',text,re.S)[0]
    template=template.replace('<DataTemplate ',f'<DataTemplate x:Key="{family}" ',1).replace('local:NotifyMessage','messages:NotifyMessage')
    edit('MatrixTemplates.xaml','</ResourceDictionary>',template+'\n</ResourceDictionary>')
edit('MatrixTemplates.xaml','xmlns:items=', 'xmlns:messages="using:BiliCopilot.UI.Controls.Message" xmlns:items=')
edit('Strings.cs','Viewer,','Like, Reply, MarkWantWatch, MarkWatching, MarkWatched, WantWatch, Watching, Watched, Follow, Unfollow, Description, CopyUrl, MorePeople, Viewer,')
edit('App.xaml','</ResourceDictionary>','<converters:BoolToIconVariantConverter x:Key="BoolToIconVariantConverter" /></ResourceDictionary>')
p=h/'Strings.cs';s=p.read_text();s=s.replace('Viewer, Follow,','Viewer,');p.write_text(s)
edit('App.xaml.cs','Add("MatrixRed",','Add("ExtraRed",()=>_ = RunMatrix(false,true)); Add("ExtraGreen",()=>_ = RunMatrix(true,true)); Add("MatrixRed",')
edit('App.xaml.cs','Add("MatrixRed",','Add("ImageChecks",()=>_ = RunOnlyImageChecks()); Add("MatrixRed",')
shutil.copy2(ui/'Styles/Repeater.xaml',h/'Repeater.xaml')
shutil.copy2(r/'docs/list-resize/ExtraModels.cs',h/'ExtraModels.cs')
shutil.copy2(r/'docs/list-resize/Matrix.cs',h/'Matrix.cs')
shutil.copy2(r/'docs/list-resize/CoverChecks.cs',h/'CoverChecks.cs')
(h/'GalleryAdapter.cs').write_text('namespace BiliCopilot.UI.Forms; public class GalleryWindow { public GalleryWindow(Richasy.BiliKernel.Models.Appearance.BiliImage image, System.Collections.Generic.IList<Richasy.BiliKernel.Models.Appearance.BiliImage> items) {} public void Activate()=>throw new System.InvalidOperationException("Gallery navigation is outside this offline template fixture"); }',encoding='utf-8')
edit('Adapters.cs','=>throw new InvalidOperationException("Network forbidden in fixture");','''{ if(r.RequestUri.Host!="fixture.invalid") throw new InvalidOperationException("Network forbidden in fixture");
    Acceptance5053.FixtureNetwork.Hits++;
    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new ByteArrayContent(File.ReadAllBytes(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"))) }); }''')
