global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
using System.Reflection;
using Richasy.WinUIKernel.Share.Toolkits;
using Microsoft.Extensions.Logging.Abstractions;
namespace BiliCopilot.UI {
 internal static class Dependencies {
  public static T Get<T>(this object caller) where T:class => typeof(T)==typeof(IFontToolkit) ? (T)(object)DispatchProxy.Create<IFontToolkit,Acceptance49.FontProxy>() : typeof(T)==typeof(Microsoft.Extensions.Logging.ILogger<Richasy.Danmaku.DanmakuRenderer>) ? (T)(object)NullLogger<Richasy.Danmaku.DanmakuRenderer>.Instance : throw new NotSupportedException(typeof(T).FullName);
 }
}
namespace BiliCopilot.UI.Toolkits {
 public static class SettingsToolkit {
  static readonly Dictionary<Models.Constants.SettingNames,object> values=new();
  public static T ReadLocalSetting<T>(Models.Constants.SettingNames key,T fallback)=>values.TryGetValue(key,out var value)?(T)value:fallback;
  public static void WriteLocalSetting<T>(Models.Constants.SettingNames key,T value)=>values[key]=value!;
 }
 public static class AppToolkit {
  public static Windows.UI.Color HexToColor(string value)=>Microsoft.UI.Colors.White;
 }
}
namespace Acceptance49 {
 // Font enumeration is the only asynchronous service adapter. Real VM executes its command.
 public class FontProxy:DispatchProxy {
  protected override object Invoke(MethodInfo method,object[] args) {
   if(method.Name!="GetFontsAsync")throw new NotSupportedException(method.Name);
   var resultType=method.ReturnType.GenericTypeArguments.Single();
   object value=resultType.IsArray?Array.CreateInstance(typeof(SystemFont),0):new List<SystemFont>();
   return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null,new[]{value})!;
  }
 }
}
