global using Windows.Foundation;
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.Input;
using Richasy.WinUIKernel.Share.ViewModels;
using Richasy.BiliKernel.Models.Media;
namespace BiliCopilot.UI {
static class Dependencies {
 public static PopularPageViewModel Model;
 public static Microsoft.UI.Dispatching.DispatcherQueue Queue;
 public static T Get<T>(this object owner) => typeof(T)==typeof(PopularPageViewModel)?(T)(object)Model:typeof(T)==typeof(Microsoft.UI.Dispatching.DispatcherQueue)?(T)(object)Queue:throw new NotSupportedException("Out of experiment boundary: "+typeof(T).FullName);
}}
// Only layout settings are adapted; real page, VM, item VM, controls and bindings remain linked.
namespace BiliCopilot.UI.ViewModels.View {public abstract class LayoutPageViewModelBase:ViewModelBase {public double NavColumnWidth{get;set;}=240;public bool IsNavColumnManualHide{get;set;} protected abstract string GetPageKey();}}
namespace BiliCopilot.UI.Toolkits {
public static class SettingsToolkit {
 public static string Saved="Recommend";
 public static T ReadLocalSetting<T>(SettingNames name,T fallback)=>(T)(object)Saved;
 public static void WriteLocalSetting<T>(SettingNames name,T value)=>Saved=value.ToString();
}
public static class ResourceToolkit {public static string GetLocalizedString(StringNames name)=>name.ToString();}
public static class AppToolkit {public static string FormatDuration(TimeSpan time)=>time.ToString(@"mm\:ss");}
}
namespace BiliCopilot.UI.Pages.Overlay {public class UserSpacePage{}}
namespace BiliCopilot.UI.Models {public class MediaSnapshot(VideoInformation data,bool isPrivate=false){public List<MediaSnapshot> Playlist{get;set;}} public record PinItem(string Id,string Title,string Cover,Constants.PinContentType Type);}
namespace BiliCopilot.UI.Models.Constants {public enum PinContentType{Video}}
namespace BiliCopilot.UI.ViewModels.Core {
public class AppViewModel {public RelayCommand<Models.MediaSnapshot> OpenPlayerCommand{get;}=new(_=>throw new NotSupportedException());public RelayCommand<(string,Richasy.WinUIKernel.Share.Base.InfoType)> ShowTipCommand{get;}=new(_=>throw new NotSupportedException());}
public class NavigationViewModel {public void NavigateToOver(Type type,object data)=>throw new NotSupportedException();}
public class PinnerViewModel {public RelayCommand<Models.PinItem> AddItemCommand{get;}=new(_=>throw new NotSupportedException());}
}
namespace BiliCopilot.UI.Extensions {
 public static class InternalHttpExtensions {
  public static int Requests;
  public static HttpClient ImageClient {get;}=new(new OfflineHandler());
  private sealed class OfflineHandler:HttpMessageHandler {
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) {
    if(r.RequestUri.Host!="fixture.invalid")throw new InvalidOperationException("External network forbidden");
    Interlocked.Increment(ref Requests);
    var bytes=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Assets","Logo.png"),ct);
    Acceptance56.Trace.Write("local-image",new{uri=r.RequestUri.ToString(),bytes=bytes.Length});
    return new(System.Net.HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
   }
  }
 }
}
namespace Acceptance56 {
public sealed partial class CountConverter:Microsoft.UI.Xaml.Data.IValueConverter {
 public object Convert(object v,Type t,object p,string l)=>v?.ToString()??"";
 public object ConvertBack(object v,Type t,object p,string l)=>throw new NotSupportedException();
}
public sealed partial class GridLengthConverter:Microsoft.UI.Xaml.Data.IValueConverter {
 public object Convert(object v,Type t,object p,string l)=>new GridLength((double)v);
 public object ConvertBack(object v,Type t,object p,string l)=>((GridLength)v).Value;
}
}
