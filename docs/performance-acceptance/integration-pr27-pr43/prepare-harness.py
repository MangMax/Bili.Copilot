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
shutil.copy2(ui/'Controls/Components/RefreshResultTipControl.xaml', dest)
shutil.copy2(pathlib.Path(__file__).with_name('RuntimeChecks.cs'), dest)
links = '''
<ItemGroup>
<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Toolkits/Pure/Recommend*.cs;$(SourceRoot)/src/Desktop/BiliCopilot.UI/Toolkits/Pure/RefreshTipState.cs" Link="Production/%(Filename)%(Extension)"/>
<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/Components/RefreshResultTipControl.xaml.cs" Link="RefreshResultTipControl.xaml.cs"/>
<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/ViewModels/Components/VideoPartitionDetailViewModel/*.cs;$(SourceRoot)/src/Desktop/BiliCopilot.UI/ViewModels/Items/PartitionViewModel.cs" Link="Production/%(Filename)%(Extension)"/>
</ItemGroup>
'''
edit('Harness.csproj', '</Project>', links+'</Project>')

