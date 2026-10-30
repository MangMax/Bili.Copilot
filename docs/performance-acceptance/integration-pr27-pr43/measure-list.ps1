param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
if(Test-Path $Destination){throw 'Use a fresh destination'}
New-Item -ItemType Directory $Destination | Out-Null
$source=[Security.SecurityElement]::Escape("$root/src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs")
"<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><RuntimeIdentifier>win-x64</RuntimeIdentifier><SelfContained>true</SelfContained><RuntimeFrameworkVersion>9.0.22</RuntimeFrameworkVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include=`"$source`" Link=`"RecommendListDiff.cs`"/></ItemGroup></Project>" | Set-Content "$Destination/Measure.csproj"
@'
using System.Diagnostics;
using System.Text.Json;
using BiliCopilot.UI.Toolkits;
var rows = new List<object>();
foreach(var size in new[]{100,1000,5000}) {
    var old=Enumerable.Range(0,size).Select(i=>i.ToString()).ToArray();
    var next=new[]{"new-a","new-b"}.Concat(old).ToArray();
    for(var run=-3;run<10;run++) {
        var items=old.ToList();
        var bytes=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
        RecommendListDiff.Apply(items,next,x=>x);RecommendListDiff.Apply(items,old,x=>x);
        clock.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        if(!items.SequenceEqual(old))throw new Exception("Restoration failed");
        if(run>=0)rows.Add(new{size,run,milliseconds=clock.Elapsed.TotalMilliseconds,allocatedBytes=bytes});
    }
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,kind="pure list prepend 2 + undo, no UI, 3 warmups per size",rows},new JsonSerializerOptions{WriteIndented=true}));
'@ | Set-Content "$Destination/Program.cs"
dotnet run --project "$Destination/Measure.csproj" -c Release -- "$([IO.Path]::GetFullPath("$Destination/raw.json"))"
if($LASTEXITCODE -ne 0){throw 'Measurement failed'}
