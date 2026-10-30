global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Windows.Foundation;
global using System.Text;
using Acceptance46;
using CommunityToolkit.Mvvm.Input;
using BiliCopilot.UI.Models.Constants;
namespace BiliCopilot.UI {
static class GlobalDependencies { public static Registry Kernel {get;}=new(); }
public class Registry { public T GetRequiredService<T>(string key=null)=>(T)Services.Map[typeof(T)]; }
static class Dependencies { public static T Get<T>(this object owner)=>GlobalDependencies.Kernel.GetRequiredService<T>(); }
}
namespace BiliCopilot.UI.Toolkits {
public static class ResourceToolkit { public static string GetLocalizedString(StringNames name)=>name.ToString(); }
public static class FileToolkit {public static bool IsLocalDataExist(string name,string folder="")=>true;}
public static class SettingsToolkit {
public static bool Streaming=true;
public static T ReadLocalSetting<T>(SettingNames name,T fallback)=>name==SettingNames.IsAIStreamingResponse?(T)(object)Streaming:fallback;
public static void WriteLocalSetting<T>(SettingNames name,T value){}
}
}
namespace BiliCopilot.UI.ViewModels.Core {
public class AppViewModel {public RelayCommand<object> ShowTipCommand {get;}=new(value=>App.Log("tip",value));}
}
namespace BiliCopilot.UI.Extensions {
public static class InternalHttpExtensions {
public static HttpClient ImageClient {get;}=new(new Deny());
sealed class Deny:HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>throw new InvalidOperationException("Network forbidden");}
}}
