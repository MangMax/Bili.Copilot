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
 public static T Get<T>(this object owner) => typeof(T)==typeof(PopularPageViewModel)?(T)(object)Model:typeof(T)==typeof(Microsoft.UI.Dispatching.DispatcherQueue)?(T)(object)Queue:throw new NotSupportedException(typeof(T).FullName);
}}
namespace BiliCopilot.UI.Pages {public class PopularPage{}}
namespace BiliCopilot.UI.ViewModels.View {public abstract class LayoutPageViewModelBase:ViewModelBase {protected abstract string GetPageKey();}}
namespace BiliCopilot.UI.Toolkits {
public static class SettingsToolkit {
 public static string Saved="Recommend";
 public static T ReadLocalSetting<T>(SettingNames name,T fallback)=>(T)(object)Saved;
 public static void WriteLocalSetting<T>(SettingNames name,T value)=>Saved=value.ToString();
}
public static class ResourceToolkit {public static string GetLocalizedString(StringNames name)=>name.ToString();}
}
namespace BiliCopilot.UI.ViewModels.Items {
[WinRT.GeneratedBindableCustomProperty]
public sealed partial class VideoItemViewModel:ViewModelBase {
 public VideoItemViewModel(VideoInformation data,VideoCardStyle style){Data=data;Style=style;}
 public VideoInformation Data{get;}
 public VideoCardStyle Style{get;}
 public string Title=>Data.BvId; public Uri Avatar=>Cover; public string Subtitle=>"Offline"; public string PublishRelativeTime=>"Today";
 public string Author=>"Offline fixture";
 public Uri Cover=>new("ms-appx:///Assets/Logo.png");
 public string Duration=>"03:00";
 public string RecommendReason=>"Controlled data";
 public string TagName=>"Acceptance";
 public double PlayCount=>1234;
 public double LikeCount=>123;
 public double DanmakuCount=>12;
 public bool CanOpenUserSpace=>false;
 public RelayCommand PlayCommand{get;}=new(()=>{});
 public RelayCommand ShowUserSpaceCommand{get;}=new(()=>{});
 public RelayCommand PlayInPrivateCommand{get;}=new(()=>{});
 public RelayCommand AddToViewLaterCommand{get;}=new(()=>{});
 public RelayCommand RemoveViewLaterCommand{get;}=new(()=>{});
 public RelayCommand RemoveHistoryCommand{get;}=new(()=>{});
 public RelayCommand RemoveFavoriteCommand{get;}=new(()=>{});
 public RelayCommand OpenInBroswerCommand{get;}=new(()=>{});
 public RelayCommand CopyUriCommand{get;}=new(()=>{});
 public RelayCommand PinCommand{get;}=new(()=>{});
}}
namespace BiliCopilot.UI.Models {public class MediaSnapshot(VideoInformation data){public List<MediaSnapshot> Playlist{get;set;}}}
namespace BiliCopilot.UI.ViewModels.Core {public class AppViewModel {public RelayCommand<Models.MediaSnapshot> OpenPlayerCommand{get;}=new(_=>{});}}
namespace BiliCopilot.UI.Extensions { public static class InternalHttpExtensions {public static HttpClient ImageClient {get;}=new(new OfflineHandler()); private sealed class OfflineHandler:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>throw new InvalidOperationException("Network forbidden in fixture");}}}namespace Acceptance5053 {
public sealed partial class CountConverter:Microsoft.UI.Xaml.Data.IValueConverter{
 public object Convert(object v,Type t,object p,string l)=>v?.ToString()??"";
 public object ConvertBack(object v,Type t,object p,string l)=>throw new NotSupportedException();
}}
