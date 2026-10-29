from pathlib import Path
import sys

h = Path(sys.argv[1])
p = h / 'Harness.csproj'
s = p.read_text(encoding='utf-8-sig')
for name in ('Core', 'Legacy', 'Share'):
    s = s.replace(f'<PackageReference Include="Danmaku.{name}" Version="0.0.1-preview6" />', f'<ProjectReference Include="../trace/danmaku-kernel-4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.{name}/Danmaku.{name}.csproj" />')
p.write_text(s, encoding='utf-8')
(h / 'Directory.Packages.props').write_text('<Project />', encoding='utf-8')
p = h / 'Package.appxmanifest'
p.write_text(p.read_text().replace('Acceptance49C4D3', 'Acceptance49Trace9D66'), encoding='utf-8')
p = h / 'App.xaml.cs'
s = p.read_text(encoding='utf-8-sig')
a = s.index('AppDomain.CurrentDomain.FirstChanceException')
b = s.index('InitializeComponent();', a)
s = s[:a] + s[b:]
s = s.replace('Acceptance 49 C9D5', 'Trace49 9D66')
s = s.replace('Add("Motion",', '''Add("SingleRolling",()=>Single(Richasy.BiliKernel.Models.DanmakuLocation.Scroll));
  Add("SingleTop",()=>Single(Richasy.BiliKernel.Models.DanmakuLocation.Top));
  Add("ExportTrace",()=>_ = ExportTrace());
  Add("Motion",''')
s = s.replace(' DispatcherTimer motionTimer;', ''' readonly string runId = Guid.NewGuid().ToString("N");
 int traceExport;
 void Single(Richasy.BiliKernel.Models.DanmakuLocation location) {
  motionTimer?.Stop(); vm.ClearAll(); vm.Resume();
  BiliCopilot.UI.Toolkits.SettingsToolkit.WriteLocalSetting(SettingNames.DanmakuLocation, location);
  vm.AddDanmaku(location == Richasy.BiliKernel.Models.DanmakuLocation.Top ? "SINGLE TOP" : "SINGLE ROLLING");
 }
 async Task ExportTrace() {
  var index = ++traceExport;
  var assembly = typeof(DanmakuFrostMaster).Assembly;
  var path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,$"trace-{index}.json");
  var snapshot = assembly.GetType("Richasy.Danmaku.Legacy.LegacyTrace49").GetMethod("Snapshot");
  await Task.Run(()=> {
   var data = snapshot.Invoke(null,null);
   var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location)));
   File.WriteAllText(path, JsonSerializer.Serialize(new{runId,pid=Environment.ProcessId,dll=assembly.Location,dllHash=hash,data}));
  });
  Write("traceExport",new{runId,index,path}); status.Text=$"Exported {index}";
 }
 DispatcherTimer motionTimer;''')
s = s.replace('new{utc=DateTimeOffset.UtcNow,kind,data}', 'new{utc=DateTimeOffset.UtcNow,runId,pid=Environment.ProcessId,kind,data}')
p.write_text(s, encoding='utf-8')
