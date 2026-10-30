using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Richasy.AgentKernel;
using Richasy.AgentKernel.Chat;
using Richasy.AgentKernel.Models;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.Comment;
using Richasy.BiliKernel.Models.Subtitle;
using Richasy.BiliKernel.Models.Comment;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.WinUIKernel.Share.Toolkits;
using Microsoft.Extensions.DependencyInjection;
using Richasy.WinUIKernel.Share;
using Richasy.WinUIKernel.AI;
using RichasyKernel;
namespace Acceptance46;

public sealed class Gate {
 public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously), Release=new(TaskCreationOptions.RunContinuationsAsynchronously), Returned=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public Exception Error;
}
public class BoundaryProxy:DispatchProxy {
 public Func<MethodInfo,object[],object> Call;
 protected override object Invoke(MethodInfo m,object[] a)=>Call(m,a);
 public static T Make<T>(Func<MethodInfo,object[],object> f) where T:class {var p=Create<T,BoundaryProxy>();((BoundaryProxy)(object)p).Call=f;return p;}
}
public static class Services {
 public static Dictionary<Type,object> Map=new();
 public static Dictionary<string,Gate> Gates=new();
 public static Queue<StreamPlan> Plans=new();
 public static List<string> Prompts=new();
 public static Gate Hold(string name){var g=new Gate();Gates.Add(name,g);return g;}
 static object Result(MethodInfo m,object value){
  if(m.ReturnType==typeof(Task))return Task.CompletedTask;
  if(m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition()==typeof(Task<>)) {
   Gates.Remove(m.Name,out var g);g?.Entered.TrySetResult();
   return typeof(Services).GetMethod(nameof(Complete),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(m.ReturnType.GenericTypeArguments[0]).Invoke(null,[value,g,m.Name]);
  }
  return value;
 }
 static async Task<T> Complete<T>(object value,Gate gate,string method){
  if(gate!=null){await gate.Release.Task;gate.Returned.TrySetResult();if(gate.Error!=null)throw gate.Error;}
  App.Log("service-return",new{method});return (T)value;
 }
 public static void Initialize(){
  Map[typeof(ISettingsToolkit)]=BoundaryProxy.Make<ISettingsToolkit>((m,a)=>m.Name=="ReadLocalSetting"?a[1]:m.ReturnType==typeof(bool)?false:null);
  Map[typeof(IFileToolkit)]=BoundaryProxy.Make<IFileToolkit>((m,a)=>{App.Log("file",new{method=m.Name,name=a[0]});return Result(m,"{title}|{description}|{subtitle}|{comments}|{content}");});
  Map[typeof(IChatConfigManager)]=BoundaryProxy.Make<IChatConfigManager>((m,a)=>{
   App.Log("config",new{method=m.Name});
   object value=null;
   if(m.Name=="GetChatConfigAsync" && (ChatProviderType)a[0]==ChatProviderType.OpenAI){
    var config=new OpenAIChatConfig();
    foreach(var p in config.GetType().GetProperties().Where(p=>p.CanWrite&&p.PropertyType==typeof(string)))p.SetValue(config,p.Name.Contains("Endpoint")?"https://offline.invalid":"SYNTHETIC-NOT-A-KEY");
    value=config;
   }
   return Result(m,value);
  });
  Map[typeof(IChatService)]=BoundaryProxy.Make<IChatService>((m,a)=>m.Name switch {
   "get_Client"=>new Client(),"Initialize"=>null,"GetPredefinedModels"=>new List<ChatModel>{new("offline","Offline controlled model",false,false)},_=>throw new NotSupportedException(m.Name)
  });
  Map[typeof(ISubtitleService)]=BoundaryProxy.Make<ISubtitleService>((m,a)=>{App.Log("subtitle",new{method=m.Name});return Result(m,m.Name=="GetSubtitleMetasAsync"?new List<SubtitleMeta>{new("fixture","中文","https://offline.invalid/subtitle")}:new List<SubtitleInformation>{new(0,2,"固定字幕 🧪"),new(2,4,"第二行 é")});});
  Map[typeof(ICommentService)]=BoundaryProxy.Make<ICommentService>((m,a)=>{App.Log("comment",new{method=m.Name,id=a[0],target=a[1].ToString()});return Result(m,new CommentView([new("synthetic-comment",new EmoteText("固定热门评论 🧪",null),"",false,null,DateTimeOffset.UnixEpoch)],a[0].ToString(),null,true,0));});
  Map[typeof(BiliCopilot.UI.ViewModels.Core.AppViewModel)]=new BiliCopilot.UI.ViewModels.Core.AppViewModel();
  Map[typeof(IResourceToolkit)]=BoundaryProxy.Make<IResourceToolkit>((m,a)=>m.Name=="GetLocalizedString"?a[0].ToString():Application.Current.Resources[a[0]]);
  Map[typeof(IAppToolkit)]=BoundaryProxy.Make<IAppToolkit>((m,a)=>{App.Log("app-toolkit",new{method=m.Name});return m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;});
  var builder=RichasyKernel.Kernel.CreateBuilder();foreach(var entry in Map)builder.Services.AddSingleton(entry.Key,entry.Value);
  builder.Services.AddKeyedSingleton<IChatService>(ChatProviderType.OpenAI.ToString(),(IChatService)Map[typeof(IChatService)]);
  builder.Services.AddSingleton<IFontToolkit,SharedFontToolkit>();
  var kernel=builder.Build();kernel.InitializeShareKernel(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);kernel.InitializeAIKernel();
 }
 public static StreamPlan Plan(){var p=new StreamPlan();Plans.Enqueue(p);return p;}
 sealed class Client:IChatClient {
  public object GetService(Type t,object key=null)=>null;
  public void Dispose(){}
  public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,ChatOptions options=null,CancellationToken cancellationToken=default){var p=Start(messages);await p.End.Task;p.Disposed.TrySetResult();return new ChatResponse(new ChatMessage(ChatRole.Assistant,p.NonStreaming));}
  static StreamPlan Start(IEnumerable<ChatMessage> messages){var p=Plans.Dequeue();Prompts.Add(string.Join("\n",messages.Select(m=>m.Text)));p.Entered.TrySetResult();return p;}
  public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,ChatOptions options=null,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken cancellationToken=default){
   var p=Start(messages);
   try {await foreach(var item in p.Items.Reader.ReadAllAsync()) {if(item.Error!=null)throw item.Error;if(item.Text==null)break;yield return new ChatResponseUpdate(ChatRole.Assistant,item.Text);item.Accepted.TrySetResult();}}
   finally {p.Disposed.TrySetResult();}
  }
 }
}
public sealed class Chunk(string text,Exception error=null){public string Text=text;public Exception Error=error;public TaskCompletionSource Accepted=new(TaskCreationOptions.RunContinuationsAsynchronously);}
public sealed class StreamPlan {
 public Channel<Chunk> Items=Channel.CreateUnbounded<Chunk>();
 public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously), Disposed=new(TaskCreationOptions.RunContinuationsAsynchronously), End=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public string NonStreaming;
 public async Task Send(string s){var c=new Chunk(s);Items.Writer.TryWrite(c);await c.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));}
 public void Finish()=>Items.Writer.TryWrite(new(null));
 public void Late(string s)=>Items.Writer.TryWrite(new(s));
 public void Fail()=>Items.Writer.TryWrite(new(null,new InvalidOperationException("SYNTHETIC-LATE-ERROR")));
}
