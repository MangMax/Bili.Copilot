using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Models.Appearance;
namespace Acceptance56;
public static class Trace {
 static readonly Stopwatch clock=Stopwatch.StartNew();
 public static readonly string Folder=Path.Combine(AppContext.BaseDirectory,"experiment");
 public static void Write(string kind,object data=null){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"records.jsonl"),JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,ms=clock.Elapsed.TotalMilliseconds,kind,data})+"\n");}
}
// SYNTHETIC service responses, not captured business traffic. No account or network API.
public class Fixture:DispatchProxy {
 public int Count,Responses;
 protected override object Invoke(MethodInfo method,object[] args){
  Count++;var name=method.Name;long offset=args.FirstOrDefault() is long n?n:0;
  Trace.Write("request",new{name,offset,Count});
  if(name=="GetVideoPartitionsAsync")return Task.FromResult<IReadOnlyList<Partition>>([]);
  if(name=="GetRecommendVideoListAsync"||name=="GetHotVideoListAsync")return Load(name=="GetRecommendVideoListAsync"?"A":"B",offset);
  if(name=="GetGlobalRankingListAsync")return Rank();
  throw new NotSupportedException(name);
 }
 async Task<(IReadOnlyList<VideoInformation>,long)> Load(string section,long offset){
  await Task.Delay(60);
  var data=offset<15?Videos(section,(int)offset*30,30):[];Responses++;
  Trace.Write("response",new{section,offset,count=data.Length,ids=data.Select(v=>v.Identifier.Id).ToArray()});
  return(data,offset+(data.Length>0?1:0));
 }
 async Task<IReadOnlyList<VideoInformation>> Rank(){await Task.Delay(60);Responses++;return Videos("C",0,60);}
 static VideoInformation[] Videos(string section,int start,int count)=>Enumerable.Range(start,count).Select(i=>{
  // First 10 IDs intentionally shared across A/B/C; instances are always fresh.
  string id=i<10?$"shared-{i:D4}":$"{section}-{i:D4}";
  var cover=new BiliImage(new Uri("https://fixture.invalid/cover.png"));
  var user=new UserProfile("offline-author","Synthetic author",new BiliImage(new Uri("https://fixture.invalid/avatar.png")));
  return new VideoInformation(new MediaIdentifier(id,$"SYNTHETIC {section} item {i:D4}",cover),new PublisherProfile(user),180,$"synthetic-{section}-{i:D4}",DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
 }).ToArray();
}
