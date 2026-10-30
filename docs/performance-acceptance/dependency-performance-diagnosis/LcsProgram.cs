using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

var variant = args[0];
var rows = new List<object>();
var tests = new List<object>();
foreach (var n in new[] { 256, 1024, 2048 })
foreach (var kind in new[] { "replacement", "rotation", "reversal", "duplicate-reversal", "same-references" })
{
    var current = Enumerable.Range(0,n).Select(i => new Item(kind == "duplicate-reversal" ? $"k{i % 13}" : $"k{i}", i)).ToArray();
    var target = current.Select(x => new Item(x.Key, x.Id + n)).ToArray();
    var start = n / 4;
    var length = n / 2;
    if (kind == "replacement")
        for (var i=start; i<start+length; i++) target[i] = new Item($"new{i}",i+n);
    else if (kind == "rotation")
    {
        var middle = target.Skip(start).Take(length).ToArray();
        for (var i=0; i<length; i++) target[start+i] = middle[(i+length/3)%length];
    }
    else
    {
        if (kind == "same-references") target = current.ToArray();
        Array.Reverse(target,start,length);
    }
    var trimmedStart=0;
    while(trimmedStart<n && current[trimmedStart].Key==target[trimmedStart].Key) trimmedStart++;
    var trimmedEnd=n;
    while(trimmedEnd>trimmedStart && current[trimmedEnd-1].Key==target[trimmedEnd-1].Key) trimmedEnd--;
    if(trimmedEnd<=trimmedStart) throw new Exception("Benchmark missed nonempty branch");
    Validate($"{kind}-{n}", current, target);
    // Five identical warmups, then twelve retained measurements. No forced GC.
    for (var iteration=-5; iteration<12; iteration++)
    {
        var list = new List<Item>(current);
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var ticks = Stopwatch.GetTimestamp();
        Apply(variant,list,target);
        var elapsed = Stopwatch.GetElapsedTime(ticks).TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread()-bytes;
        if (!list.Select(x=>x.Key).SequenceEqual(target.Select(x=>x.Key))) throw new Exception("Wrong result");
        if (iteration>=0) rows.Add(new {kind,n,middle=trimmedEnd-trimmedStart,iteration,milliseconds=elapsed,allocatedBytes=bytes});
    }
}
File.WriteAllText(args[1],JsonSerializer.Serialize(new {variant,pid=Environment.ProcessId,
    utc=DateTimeOffset.UtcNow,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    warmups=5,repetitions=12,tests,rows},new JsonSerializerOptions {WriteIndented=true}));
Console.WriteLine($"{variant}: {tests.Count} behavior cases PASS; {rows.Count} measurements");

void Validate(string name, Item[] current, Item[] target)
{
    var runs = new List<(Item[] Items, string[] Events)>();
    foreach(var v in new[] {"Rectangular","Jagged","Shortcut"})
    {
        var list = new ObservableCollection<Item>(current);
        var events = new List<string>();
        list.CollectionChanged += (_,e) => events.Add($"{e.Action}:{e.OldStartingIndex}:{e.NewStartingIndex}:" +
            string.Join(',', e.OldItems?.Cast<Item>().Select(x=>x.Id) ?? []) + ":" +
            string.Join(',', e.NewItems?.Cast<Item>().Select(x=>x.Id) ?? []));
        Apply(v,list,target);
        if (!list.Select(x=>x.Key).SequenceEqual(target.Select(x=>x.Key))) throw new Exception(name+" keys");
        if(events.Any(x=>x.StartsWith("Reset"))) throw new Exception(name+" reset");
        runs.Add((list.ToArray(),events.ToArray()));
    }
    foreach(var run in runs.Skip(1))
    {
        if(!run.Events.SequenceEqual(runs[0].Events)) throw new Exception(name+" event divergence");
        for(var i=0;i<run.Items.Length;i++)
            if(!ReferenceEquals(run.Items[i],runs[0].Items[i])) throw new Exception(name+" reference divergence");
    }
    // Independent anchors: unchanged prefix/suffix must retain original objects.
    var quarter = current.Length/4;
    for(var i=0;i<quarter;i++)
        if(!ReferenceEquals(runs[0].Items[i],current[i]) ||
           !ReferenceEquals(runs[0].Items[^(i+1)],current[^(i+1)])) throw new Exception(name+" retained anchors");
    tests.Add(new {name,status="PASS",events=runs[0].Events.Length,
        retained=runs[0].Items.Count(x=>current.Any(y=>ReferenceEquals(x,y)))});
}
static void Apply(string variant,IList<Item> list,Item[] target)
{
    switch(variant)
    {
        case "Rectangular": Rectangular.RecommendListDiff.Apply(list,target,x=>x.Key); break;
        case "Jagged": Jagged.RecommendListDiff.Apply(list,target,x=>x.Key); break;
        case "Shortcut": Shortcut.RecommendListDiff.Apply(list,target,x=>x.Key); break;
        default: throw new ArgumentException(variant);
    }
}
record Item(string Key,int Id);
