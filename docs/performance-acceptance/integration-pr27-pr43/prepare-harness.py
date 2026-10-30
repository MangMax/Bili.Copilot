import pathlib, shutil, sys
root, dest = map(pathlib.Path, sys.argv[1:])
ui = root / 'src/Desktop/BiliCopilot.UI'
def edit(name, old, new):
    p = dest / name
    s = p.read_text(encoding='utf-8-sig')
    assert old in s, (name, old)
    p.write_text(s.replace(old, new), encoding='utf-8-sig')
edit('Package.appxmanifest', 'BiliCopilot.Acceptance5053D26C', 'BiliCopilot.Integration2743.Controls')
edit('App.xaml.cs', 'public partial class App:Application', 'public partial class App:Application')
edit('App.xaml.cs', 'Add("Close",()=>window.Close());', 'Add("IntegrationChecks",()=>_ = RunIntegrationChecks());\n  Add("Close",()=>window.Close());')
edit('Adapters.cs', 'public static T ReadLocalSetting<T>(SettingNames name,T fallback)=>(T)(object)Saved;', 'public static bool Incremental;\n public static T ReadLocalSetting<T>(SettingNames name,T fallback)=>name==SettingNames.IncrementalRecommendRefresh?(T)(object)Incremental:(T)(object)Saved;')
edit('Adapters.cs', ':throw new NotSupportedException(typeof(T).FullName);', ':typeof(T)==typeof(Microsoft.Extensions.Logging.ILogger<BiliCopilot.UI.ViewModels.Components.VideoPartitionDetailViewModel>)?(T)(object)Microsoft.Extensions.Logging.Abstractions.NullLogger<BiliCopilot.UI.ViewModels.Components.VideoPartitionDetailViewModel>.Instance:throw new NotSupportedException(typeof(T).FullName);')
edit('Strings.cs', 'public enum StringNames', 'public enum StringNames')
p=dest/'Strings.cs'
s=p.read_text(encoding='utf-8-sig'); s=s.replace('enum StringNames {','enum StringNames { RefreshingRecommendVideos, UndoRefresh, Close,') if 'enum StringNames {' in s else s.replace('enum StringNames\n{','enum StringNames\n{\n RefreshingRecommendVideos, UndoRefresh, Close,')
p.write_text(s,encoding='utf-8-sig')

shutil.copy2(pathlib.Path(__file__).with_name('RuntimeChecks.cs'), dest)
links = '''
<ItemGroup>
<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Toolkits/Pure/Recommend*.cs" Link="Production/%(Filename)%(Extension)"/>

<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/ViewModels/Components/VideoPartitionDetailViewModel/*.cs;$(SourceRoot)/src/Desktop/BiliCopilot.UI/ViewModels/Items/PartitionViewModel.cs" Link="Production/%(Filename)%(Extension)"/>
</ItemGroup>
'''
edit('Harness.csproj', '</Project>', links+'</Project>')
edit('Fixture.cs', 'new VideoInformation(default,null,bvId:$"{Scenario}', 'new VideoInformation(new($"{Scenario}-{name}-{Page}-{i}","Fixture",null),null,bvId:$"{Scenario}')
edit('App.xaml.cs', 'using System.Reflection;', 'using System.Reflection;\nusing Richasy.WinUIKernel.Share;\nusing RichasyKernel;')


edit('Adapters.cs', 'public Uri Cover=>new("ms-appx:///Assets/Logo.png");', 'public Uri Cover=>null;')

edit('Harness.csproj', '</Project>', '<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/RecommendationScrollAnchor.cs" Link="Production/RecommendationScrollAnchor.cs"/></ItemGroup></Project>')

for folder, name in [('Popular','PopularMainHeader'),('VideoPartition','VideoPartitionMainHeader'),('VideoPartition','VideoPartitionMainBody')]:
    shutil.copy2(ui/f'Controls/{folder}/{name}.xaml',dest)
    edit('Harness.csproj','</Project>',f'<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/{folder}/{name}.xaml.cs" Link="{name}.xaml.cs"/></ItemGroup></Project>')
for name in ['Controls/VideoPartition/VideoPartitionDetailControlBase.cs','Controls/Components/UndoRefreshButtonAnimator.cs','Controls/RecommendationCoverImage.cs','Toolkits/Pure/CoverDecodeSize.cs']:
    edit('Harness.csproj','</Project>',f'<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/{name}" Link="Production/{pathlib.Path(name).name}"/></ItemGroup></Project>')
edit('Strings.cs','RefreshingRecommendVideos,','RefreshingRecommendVideos, Refresh, PlayAll, Sort,')
edit('App.xaml','<Style x:Key="PageScrollViewStyle"', '<Style x:Key="IconButtonStyle" TargetType="Button"/><Style x:Key="TitleBlockStyle" TargetType="TextBlock"/><Style x:Key="PageScrollViewStyle"')
edit('Adapters.cs',':throw new NotSupportedException(typeof(T).FullName);',':typeof(T)==typeof(Microsoft.Extensions.Logging.ILogger<BiliCopilot.UI.Controls.RecommendationCoverImage>)?(T)(object)Microsoft.Extensions.Logging.Abstractions.NullLogger<BiliCopilot.UI.Controls.RecommendationCoverImage>.Instance:throw new NotSupportedException(typeof(T).FullName);')
edit('App.xaml','<local:CountConverter x:Key="CountConverter" />','<local:CountConverter x:Key="CountConverter"/><local:CountConverter x:Key="SortTypeTextConverter"/><converters:ObjectToBoolConverter x:Key="ObjectToBoolReverseConverter" IsReverse="True"/><converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/><converters:BoolToVisibilityConverter x:Key="BoolToVisibilityReverseConverter" IsReverse="True"/>')
shutil.copy2(ui/'Controls/Components/PartitionItemControl.xaml',dest)
edit('Harness.csproj','</Project>','<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/Components/PartitionItemControl.xaml.cs" Link="PartitionItemControl.xaml.cs"/></ItemGroup></Project>')

