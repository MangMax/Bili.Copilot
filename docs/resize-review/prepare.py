import pathlib, shutil, xml.etree.ElementTree as ET
r=pathlib.Path(__file__).resolve().parents[2]; h=r/'artifacts/resize-review/native'; h.mkdir(exist_ok=False)
o=r/'artifacts/resize-review/original/host'
tree=ET.parse(o/'Harness.csproj'); p=tree.getroot()
for group in list(p):
    for node in list(group):
        if node.tag in ('Compile','ProjectReference'): group.remove(node)
g=ET.SubElement(p,'ItemGroup')
for f in ['Controls/RecommendationCoverImage.cs','Controls/BasicCoverImage.cs','Extensions/BasicScaleEffect.cs','Toolkits/Pure/CoverDecodeSize.cs']:
    ET.SubElement(g,'Compile',Include=str(r/'src/Desktop/BiliCopilot.UI'/f),Link=pathlib.Path(f).name)
tree.write(h/'Harness.csproj',encoding='unicode')
(h/'Directory.Build.props').write_text('<Project/>')
shutil.copytree(o/'Assets',h/'Assets')
shutil.copy(r/'src/Desktop/BiliCopilot.UI/app.manifest',h/'app.manifest')
(h/'Package.appxmanifest').write_text((o/'Package.appxmanifest').read_text().replace('Resize5a5e','ResizeReviewB5d1'))
(h/'App.xaml').write_text('''<Application x:Class="Review.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Application.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" /></ResourceDictionary.MergedDictionaries></ResourceDictionary></Application.Resources></Application>''')
(h/'Adapters.cs').write_text('''global using Windows.Foundation;
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
namespace BiliCopilot.UI { static class Dependencies { public static T Get<T>(this object owner)=>(T)(object)Microsoft.Extensions.Logging.Abstractions.NullLogger<BiliCopilot.UI.Controls.RecommendationCoverImage>.Instance; } }
namespace BiliCopilot.UI.Extensions { public static class InternalHttpExtensions {public static HttpClient ImageClient {get;}=new(new Offline()); private sealed class Offline:HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)=>throw new InvalidOperationException("Network forbidden");}}}
''')
shutil.copy(r/'docs/resize-review/NativeChecks.cs',h/'App.xaml.cs')
