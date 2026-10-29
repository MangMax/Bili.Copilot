using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Richasy.BiliKernel.Models.Search;
using Microsoft.Extensions.Logging;
namespace Acceptance51;
public static class Trace {
    static readonly Stopwatch Clock=Stopwatch.StartNew();
    static readonly object Gate=new();
    public static void Write(string kind,object? data=null) { lock(Gate) File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"events.jsonl"),JsonSerializer.Serialize(new {ms=Clock.Elapsed.TotalMilliseconds,kind,data})+Environment.NewLine); }
}
// Dispatch only the real ISearchService suggestion/recommendation boundary; all other calls fail closed.
public class Fixture:DispatchProxy {
    public sealed record Request(int Id,string Keyword,CancellationToken Token,object Completion);
    public readonly List<Request> Requests=[];
    public event Action? Changed;
    protected override object? Invoke(MethodInfo? method,object?[]? args) {
        var recommend=method!.Name=="GetSearchRecommendsAsync";
        if(!recommend && method.Name!="GetSearchSuggestsAsync") throw new NotSupportedException(method.Name);
        var keyword=recommend?"<recommend>":(string)args![0]!;
        var token=(CancellationToken)args![recommend?0:1]!;
        object completion=recommend?new TaskCompletionSource<IReadOnlyList<SearchRecommendItem>>(TaskCreationOptions.RunContinuationsAsynchronously):new TaskCompletionSource<IReadOnlyList<SearchSuggestItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request=new Request(Requests.Count+1,keyword,token,completion); Requests.Add(request);
        Trace.Write("request",new {request.Id,keyword,token.IsCancellationRequested});
        token.Register(()=>Trace.Write("cancel",new{request.Id,keyword})); Changed?.Invoke();
        return recommend?((TaskCompletionSource<IReadOnlyList<SearchRecommendItem>>)completion).Task:((TaskCompletionSource<IReadOnlyList<SearchSuggestItem>>)completion).Task;
    }
    public void Complete(bool oldest) {
        var pending=Requests.Where(r=>r.Completion is TaskCompletionSource<IReadOnlyList<SearchSuggestItem>> s?!s.Task.IsCompleted:!((TaskCompletionSource<IReadOnlyList<SearchRecommendItem>>)r.Completion).Task.IsCompleted).ToArray();
        if(pending.Length==0){Trace.Write("no-pending");return;}
        var r=oldest?pending[0]:pending[^1];
        Trace.Write("complete",new{r.Id,r.Keyword,cancelled=r.Token.IsCancellationRequested});
        if(r.Completion is TaskCompletionSource<IReadOnlyList<SearchSuggestItem>> s) s.SetResult(new SearchSuggestItem[]{new(r.Keyword+" result",r.Keyword)});
        else ((TaskCompletionSource<IReadOnlyList<SearchRecommendItem>>)r.Completion).SetResult(new SearchRecommendItem[]{new("local",0,"fixture recommendation","fixture recommendation")});
    }
}
public sealed class FixtureLogger<T>:ILogger<T> {
    public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
    public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter)=>Trace.Write("log",new{level=level.ToString(),message=formatter(state,error),error=error?.ToString()});
}
