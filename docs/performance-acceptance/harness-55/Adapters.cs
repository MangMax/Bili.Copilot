global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Input;
using BiliCopilot.UI.Models.Constants;
using Richasy.WinUIKernel.Share.ViewModels;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI {
 internal static class Dependencies {
  internal static T Get<T>(this object caller) where T:class => typeof(T)==typeof(ViewModels.Core.AppViewModel) ? (T)(object)Acceptance55.App.Services : typeof(T)==typeof(ViewModels.View.SettingsPageViewModel) ? (T)(object)Acceptance55.App.Settings : throw new NotSupportedException(typeof(T).Name);
 }
}
namespace BiliCopilot.UI.Models.Constants {
 public enum SettingNames { DownloadFolder, OpenFolderAfterDownload, UseExternalBBDown, OnlyCopyCommandWhenDownload, WithoutCredentialWhenGenDownloadCommand, PreferCodec, DownloadWithDanmaku, FilterAISubtitle }
 public enum PreferCodecType { H264,H265,AV1 }
 public enum StringNames { Copied,Quality,BatchDownload,DownloadAllParts,SelectPartsDownload,Extra,DownloadCover,DownloadDanmaku,DownloadAudio,DownloadSubtitle }
}
namespace BiliCopilot.UI.Extensions {
 public static class InternalHttpExtensions { public const string VideoUserAgent="Synthetic-acceptance55"; }
 public sealed class Locale:Microsoft.UI.Xaml.Markup.MarkupExtension {
  public string Name {get;set;}
  protected override object ProvideValue()=>Name;
 }
}
namespace BiliCopilot.UI.Toolkits {
 public static class SettingsToolkit {
  internal static readonly Dictionary<SettingNames,object> Values=new();
  public static T ReadLocalSetting<T>(SettingNames key,T fallback)=>Values.TryGetValue(key,out var value)?(T)value:fallback;
  public static void WriteLocalSetting<T>(SettingNames key,T value) {Values[key]=value; Acceptance55.Log.Write("setting",new {key=key.ToString(),value});}
 }
 public static class ResourceToolkit {public static string GetLocalizedString(StringNames name)=>name.ToString();}
}
namespace Richasy.BiliKernel.Bili.Authorization {
 public record SyntheticToken(string AccessToken);
 public interface IBiliTokenResolver {SyntheticToken GetToken();}
 public interface IBiliCookiesResolver {string GetCookieString();}
 public sealed class SyntheticAuth:IBiliTokenResolver,IBiliCookiesResolver {
  public SyntheticToken GetToken()=>new("SYNTHETIC-NOT-A-CREDENTIAL");
  public string GetCookieString()=>"synthetic=offline55";
 }
}
namespace Richasy.BiliKernel.Models.Media {
 public sealed class PlayerFormatInformation {public string Description {get;set;} public int Quality {get;set;}}
 public sealed class VideoPart {}
 public sealed class EpisodeInformation {}
}
namespace BiliCopilot.UI.ViewModels.Core {
 public sealed class AppViewModel {
  public string BBDownPath {get;set;}
  public string FFmpegPath=>"synthetic-ffmpeg-not-executed";
  public IRelayCommand<object> ShowTipCommand {get;}=new RelayCommand<object>(x=>Acceptance55.Log.Write("tip",new {value=x.ToString()}));
 }
 // Name lookup shadows Process only in the ViewModel namespace. Runner is in Toolkits
 // and receives this real System.Diagnostics.Process subclass; Start/Wait are NOT overridden.
 public sealed class Process:System.Diagnostics.Process {
  readonly string caseRoot=Acceptance55.Log.CaseRoot;
  public Process(){Acceptance55.Log.Write("process-created",new {});}
  public new static System.Diagnostics.Process Start(string executable,string directory) {
   if(executable!="explorer.exe" || directory!=Acceptance55.App.DownloadRoot) throw new InvalidOperationException("Unexpected shell boundary");
   Acceptance55.Log.Write("folder-adapter",new {directory}); return null;
  }
  protected override void Dispose(bool disposing) {
   Microsoft.Win32.SafeHandles.SafeProcessHandle handle=null;
   try {handle=SafeHandle;} catch(InvalidOperationException) {}
   base.Dispose(disposing);
   if(disposing) Acceptance55.Log.WriteAt(caseRoot,"process-disposed",new {handleCaptured=handle!=null,handleClosed=handle?.IsClosed});
  }
 }
}
namespace BiliCopilot.UI.ViewModels.View {
 // Binding-only settings adapter. Production settings VM also initializes AI/media services;
 // none are composed here. Production DownloadSettingControl XAML is linked unchanged.
 public sealed partial class SettingsPageViewModel:ViewModelBase {
  bool Read(SettingNames k,bool fallback=false)=>Toolkits.SettingsToolkit.ReadLocalSetting(k,fallback);
  void Write(SettingNames k,bool v,string property){Toolkits.SettingsToolkit.WriteLocalSetting(k,v);OnPropertyChanged(property);}
  public string DefaultDownloadPath=>Acceptance55.App.DownloadRoot;
  public bool DownloadWithDanmaku {get=>Read(SettingNames.DownloadWithDanmaku);set=>Write(SettingNames.DownloadWithDanmaku,value,nameof(DownloadWithDanmaku));}
  public bool FilterAISubtitle {get=>Read(SettingNames.FilterAISubtitle,true);set=>Write(SettingNames.FilterAISubtitle,value,nameof(FilterAISubtitle));}
  public bool OpenFolderAfterDownload {get=>Read(SettingNames.OpenFolderAfterDownload,true);set=>Write(SettingNames.OpenFolderAfterDownload,value,nameof(OpenFolderAfterDownload));}
  public bool UseExternalBBDown {get=>Read(SettingNames.UseExternalBBDown);set=>Write(SettingNames.UseExternalBBDown,value,nameof(UseExternalBBDown));}
  public bool OnlyCopyCommandWhenDownload {get=>Read(SettingNames.OnlyCopyCommandWhenDownload);set=>Write(SettingNames.OnlyCopyCommandWhenDownload,value,nameof(OnlyCopyCommandWhenDownload));}
  public bool WithoutCredentialWhenGenDownloadCommand {get=>Read(SettingNames.WithoutCredentialWhenGenDownloadCommand);set=>Write(SettingNames.WithoutCredentialWhenGenDownloadCommand,value,nameof(WithoutCredentialWhenGenDownloadCommand));}
  public IRelayCommand ChooseDownloadFolderCommand {get;}=new RelayCommand(()=>throw new NotSupportedException("No picker in acceptance"));
  public IRelayCommand OpenDownloadFolderCommand {get;}=new RelayCommand(()=>Acceptance55.Log.Write("settings-folder-adapter",new {}));
 }
}
namespace Acceptance55 {
 public sealed class ProbeLogger:ILogger<BiliCopilot.UI.ViewModels.Core.DownloadViewModel> {
  public IDisposable BeginScope<TState>(TState state) where TState:notnull=>null;
  public bool IsEnabled(LogLevel level)=>true;
  public void Log<TState>(LogLevel level,EventId id,TState state,Exception ex,Func<TState,Exception,string> formatter)=>Acceptance55.Log.Write("production-log",new {level=level.ToString(),message=formatter(state,ex)});
 }
 public static class Log {
  static readonly object Gate=new();
  public static string Root,CaseRoot;
  public static void Write(string kind,object data)=>WriteAt(CaseRoot??Root,kind,data);
  public static void WriteAt(string root,string kind,object data) { lock(Gate) File.AppendAllText(Path.Combine(root,"events.jsonl"),System.Text.Json.JsonSerializer.Serialize(new {schemaVersion=1,utc=DateTimeOffset.UtcNow,pid=Environment.ProcessId,kind,data})+Environment.NewLine); }
 }
}
