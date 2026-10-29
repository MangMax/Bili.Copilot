using BiliCopilot.UI.Controls.Player;
using BiliCopilot.UI.Controls.Settings;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Media;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;

namespace Acceptance55;
public partial class App:Application {
 public static AppViewModel Services=new();
 public static SettingsPageViewModel Settings=new();
 public static string DownloadRoot;
 Window main,keeper;
 DownloadButton download;
 TextBlock status;
 TextBox caseName;
 int exitCode;
 bool missing;
 public App(){UnhandledException+=(_,e)=>{File.AppendAllText(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"unhandled.txt"),e.Exception.ToString()+Environment.NewLine);};InitializeComponent();}
 protected override void OnLaunched(LaunchActivatedEventArgs args) {
  Log.Root=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
  DownloadRoot=Path.Combine(Log.Root,"owned-downloads");Directory.CreateDirectory(DownloadRoot);
  SettingsToolkit.Values[SettingNames.DownloadFolder]=DownloadRoot;
  Environment.SetEnvironmentVariable("PATH",Path.Combine(AppContext.BaseDirectory,"Assets","Child")+";"+Environment.GetEnvironmentVariable("PATH"));
  main=new Window {Title="Offline #55 — real download controls / synthetic adapters"};
  main.AppWindow.Resize(new Windows.Graphics.SizeInt32(1320,1380));
  var root=new StackPanel {Spacing=12,Padding=new Thickness(24)};
  root.Children.Add(new TextBlock {Text="#55: real DownloadViewModel + runner + controls",Style=(Style)Resources["TitleTextBlockStyle"]});
  root.Children.Add(new TextBlock {Text="Offline synthetic credentials. Folder opening is recorded by adapter; no Explorer. Settings VM/storage are adapters.",TextWrapping=TextWrapping.Wrap});
  caseName=new TextBox {Header="Owned case name",Text="SuccessOpen"};Id(caseName,"CaseName");root.Children.Add(caseName);
  var row=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};root.Children.Add(row);
  Add(row,"ExitZero",()=>{exitCode=0;missing=false;Status("Exit code 0");});
  Add(row,"ExitSeven",()=>{exitCode=7;missing=false;Status("Exit code 7");});
  Add(row,"MissingExe",()=>{missing=true;Status("Missing executable");});
  Add(row,"Prepare",Prepare);
  Add(row,"TagControls",()=>TagControls(root));
  Add(row,"KeepHost",()=>{keeper=new Window {Title="Owned #55 keeper — harness-only lifecycle control"};keeper.Content=new TextBlock {Text="Host remains alive for close-window boundary only."};keeper.Activate();main.Activate();Log.Write("keeper-opened",new {});});
  Add(row,"CloseMain",()=>main.Close());
  Add(row,"Exit",()=>{keeper?.Close();main.Close();});
  var settings=new DownloadSettingControl();root.Children.Add(settings);
  download=new DownloadButton();Id(download,"RealDownload");root.Children.Add(download);
  var end=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};root.Children.Add(end);
  Add(end,"VerifyClipboard",async()=>{var text=await Clipboard.GetContent().GetTextAsync();var ok=text.StartsWith("BBDown -e avc ")&&text.Contains("--cover-only \"https://invalid.example/offline55\"")&&text.Contains("SYNTHETIC-NOT-A-CREDENTIAL")&&text.Contains("synthetic=offline55");Log.Write("clipboard-owned",new {matches=ok,text});Status(ok?"Owned clipboard matches":"Clipboard mismatch");});
  Add(end,"ClearOwnedClipboard",async()=>{var text=await Clipboard.GetContent().GetTextAsync();if(text.Contains("SYNTHETIC-NOT-A-CREDENTIAL")&&text.Contains("invalid.example/offline55")){Clipboard.Clear();Log.Write("clipboard-cleared",new {});Status("Owned clipboard cleared");}});
  status=new TextBlock {Text="Ready",TextWrapping=TextWrapping.Wrap};Id(status,"Status");root.Children.Add(status);
  main.Content=new ScrollViewer {Content=root};
  main.Closed+=(_,_)=>Log.Write("main-closed",new {keeperExists=keeper!=null});
  root.Loaded+=(_,_)=>{
   // Add stable test IDs to existing production visual instances; no XAML rewriting.
   TagControls(root);
  };
  main.Activate();
  File.WriteAllText(Path.Combine(Log.Root,"identity.json"),JsonSerializer.Serialize(new {schemaVersion=1,pid=Environment.ProcessId,hwnd=WinRT.Interop.WindowNative.GetWindowHandle(main).ToInt64(),root=Log.Root,downloadRoot=DownloadRoot}));
 }
 void TagControls(DependencyObject root){
   var toggles=Descendants(root).OfType<ToggleSwitch>().ToArray();
   var names=new[]{"DownloadWithDanmaku","FilterAISubtitle","OpenFolderAfterDownload","UseExternalBBDown","OnlyCopyCommandWhenDownload","WithoutCredentialWhenGenDownloadCommand"};
   for(int i=0;i<Math.Min(names.Length,toggles.Length);i++){Id(toggles[i],names[i]);AutomationProperties.SetName(toggles[i],names[i]);}
   foreach(var b in Descendants(root).OfType<DropDownButton>()) Id(b,"DownloadMenu");
   Log.Write("ui-tagged",new {toggles=toggles.Length});
 }
 void Prepare(){
  if(!System.Text.RegularExpressions.Regex.IsMatch(caseName.Text,"^[A-Za-z0-9_-]+$"))throw new InvalidOperationException();
  Log.CaseRoot=Path.Combine(Log.Root,caseName.Text);if(Directory.Exists(Log.CaseRoot))throw new InvalidOperationException("Use a fresh case");Directory.CreateDirectory(Log.CaseRoot);
  File.WriteAllText(Path.Combine(Log.CaseRoot,"exit-code"),exitCode.ToString());Environment.SetEnvironmentVariable("BILI55_CASE",Log.CaseRoot);
  Services.BBDownPath=missing?Path.Combine(Log.CaseRoot,"missing.exe"):Path.Combine(AppContext.BaseDirectory,"Assets","Child","BBDown.exe");
  var auth=new SyntheticAuth();var vm=new DownloadViewModel(new ProbeLogger(),auth,auth);
  download.ViewModel=vm;vm.InitializeMetas("https://invalid.example/offline55",Array.Empty<PlayerFormatInformation>());
  Log.Write("prepared",new {name=caseName.Text,exitCode,missing,open=Settings.OpenFolderAfterDownload,external=Settings.UseExternalBBDown,copy=Settings.OnlyCopyCommandWhenDownload});Status("Prepared "+caseName.Text);
 }
 static IEnumerable<DependencyObject> Descendants(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var d in Descendants(c))yield return d;}}
 static void Id(DependencyObject o,string id)=>AutomationProperties.SetAutomationId(o,id);
 void Status(string text)=>status.Text=text;
 static void Add(Panel p,string name,Action action){var b=new Button {Content=name};Id(b,name);b.Click+=(_,_)=>action();p.Children.Add(b);}
}
