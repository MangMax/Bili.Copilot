using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
namespace Acceptance5053;
public static class Trace {
 static readonly Stopwatch clock=Stopwatch.StartNew();
 static readonly string path=Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"trace.jsonl");
 public static void Write(string kind,object data=null){File.AppendAllText(path,JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,ms=clock.Elapsed.TotalMilliseconds,kind,data})+"\n");}
}
public class Fixture:DispatchProxy {
 public string Scenario; public int Count; public int Page; public bool Fail; public bool Hold; public int Batch=1; public int Limit=3;
 public TaskCompletionSource<IReadOnlyList<Partition>> Directory=new();
 public TaskCompletionSource<(IReadOnlyList<VideoInformation>,long)> Held;
 public void ReleaseDirectory(bool failure=false){Trace.Write("directory-release",new{Scenario,failure});if(failure)Directory.TrySetException(new Exception("Controlled directory failure"));else Directory.TrySetResult([new Partition("13","Anime",new(new Uri("ms-appx:///Assets/Logo.png")))]);}
 protected override object Invoke(MethodInfo method,object[] args){
  var name=method.Name;Count++;Trace.Write("request",new{Scenario,Count,name,offset=args.FirstOrDefault() is long n?n:-1,Page});
  if(name=="GetVideoPartitionsAsync")return Directory.Task;
  if(name=="GetRecommendVideoListAsync"||name=="GetHotVideoListAsync"){
   if(Hold){Hold=false;Held=new();return Held.Task;}
   return Load(name);
  }
  if(name=="GetGlobalRankingListAsync"||name=="GetPartitionRankingListAsync")return Task.FromResult<IReadOnlyList<VideoInformation>>(Videos(name,1));
  throw new NotSupportedException(name);
 }
 async Task<(IReadOnlyList<VideoInformation>,long)> Load(string name){await Task.Delay(60);var page=++Page;
  if(page>Limit && Fail){Trace.Write("response-error",new{Scenario,page});throw new Exception("Controlled page failure");}
  var data=page<=Limit?Videos(name,Batch):[];
  Trace.Write("response",new{Scenario,page,count=data.Length});return(data,page);
 }
 VideoInformation[] Videos(string name,int count)=>Enumerable.Range(0,count).Select(i=>new VideoInformation(default,null,bvId:$"{Scenario} {name.Replace("Get","").Replace("Async","")} p{Page} #{i}")).ToArray();
 public void ReleaseOld(){Trace.Write("old-release",Scenario);Held?.TrySetResult(([new VideoInformation(default,null,bvId:"STALE MUST NOT APPEAR")],999));}
}
