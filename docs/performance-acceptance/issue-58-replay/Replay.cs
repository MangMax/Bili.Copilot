using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bilibili.Community.Service.Dm.V1;
using Google.Protobuf;
using Richasy.BiliKernel.Content;
using Richasy.BiliKernel.Http;

namespace Issue58;

public sealed record Fixture(string Name, string ContentType, byte[] Bytes, int Count);
public sealed class Item
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
}
[JsonSerializable(typeof(BiliDataResponse<List<Item>>))]
public partial class ReplayJsonContext : JsonSerializerContext { }

public static class Fixtures
{
    public static Fixture Json(int count) => new($"json-{count}", "application/json",
        JsonSerializer.SerializeToUtf8Bytes(new BiliDataResponse<List<Item>>
        {
            Code = 0, Message = "0", Data = Enumerable.Range(0, count)
                .Select(i => new Item { Id = i, Title = $"synthetic item {i:D6} 测试" }).ToList()
        }, ReplayJsonContext.Default.BiliDataResponseListItem), count);

    public static Fixture Grpc(int count)
    {
        var reply = new DmSegMobileReply();
        for (var i = 0; i < count; i++)
            reply.Elems.Add(new DanmakuElem { Id = i + 1, Content = $"synthetic 弹幕 {i:D6}",
                Progress = i * 100, Mode = 1, Color = (DmColorfulType)0xffffff, Fontsize = 25 });
        var body = reply.ToByteArray();
        var framed = new byte[body.Length + 5];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(framed.AsSpan(1, 4), body.Length);
        body.CopyTo(framed, 5);
        return new($"grpc-{count}", "application/grpc", framed, count);
    }
}

// A fresh response/content on every send, no sockets, credentials or authentication.
public sealed class ReplayHandler(Fixture fixture) : HttpMessageHandler
{
    public int Sends { get; private set; }
    public ReplayContent? LastContent { get; private set; }
    public bool Disposed { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public Action<CancellationToken>? BeforeResponse { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Sends++;
        BeforeResponse?.Invoke(token);
        LastContent = new ReplayContent(fixture.Bytes);
        LastContent.Headers.ContentType = new(fixture.ContentType);
        return Task.FromResult(new HttpResponseMessage(Status) { Content = LastContent });
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}

public sealed class ReplayContent(byte[] bytes) : HttpContent
{
    public int Serializations { get; private set; }
    public bool Disposed { get; private set; }
    protected override bool TryComputeLength(out long length) { length = bytes.Length; return true; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        Serializations++;
        return stream.WriteAsync(bytes).AsTask();
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}

public static class Pipeline
{
    public static async Task<int> Run(BiliHttpClient? baseline, CandidateHttpClient? candidate, Fixture fixture)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://replay.invalid/fixed");
        using var response = baseline != null ? await baseline.SendAsync(request) : await candidate!.SendAsync(request);
        if (fixture.ContentType == "application/json")
        {
            var result = baseline != null
                ? await BiliHttpClient.ParseAsync(response, ReplayJsonContext.Default.BiliDataResponseListItem)
                : await CandidateHttpClient.ParseAsync(response, ReplayJsonContext.Default.BiliDataResponseListItem);
            return result.Data!.Count;
        }
        var message = baseline != null
            ? await BiliHttpClient.ParseAsync(response, DmSegMobileReply.Parser)
            : await CandidateHttpClient.ParseAsync(response, DmSegMobileReply.Parser);
        return message.Elems.Count;
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var output = args.Length > 0 ? args[0] : "raw";
        Directory.CreateDirectory(output);
        if (args.Length > 1 && args[1] == "errors")
        {
            await MeasureErrors(output);
            return;
        }
        var fixtures = new[] { Fixtures.Json(20), Fixtures.Json(5000), Fixtures.Grpc(20), Fixtures.Grpc(5000) };
        File.WriteAllText(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow, measuredIntegrationSha = Environment.GetEnvironmentVariable("ISSUE58_INTEGRATION_SHA"),
            kernelSha = "2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d", runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            cpuCount = Environment.ProcessorCount, serverGc = System.Runtime.GCSettings.IsServerGC,
            protobuf = typeof(IMessage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            json = typeof(JsonSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            framework = typeof(HttpClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            fixtures = fixtures.Select(f => new { f.Name, f.Count, bytes = f.Bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(f.Bytes)) })
        }, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var f in fixtures) File.WriteAllBytes(Path.Combine(output, f.Name + ".bin"), f.Bytes);
        using var csv = new StreamWriter(Path.Combine(output, "measurements.csv"));
        csv.WriteLine("fixture,variant,round,iterations,bytes,wall_ms,cpu_ms,allocated_bytes,sends,checksum");
        using var process = Process.GetCurrentProcess();
        foreach (var fixture in fixtures)
        {
            // JSON candidate is intentionally unchanged: negative/control comparison.
            var iterations = fixture.Count < 100 ? 10000 : 500;
            using var bh = new ReplayHandler(fixture);
            using var ch = new ReplayHandler(fixture);
            using var baseline = new BiliHttpClient(bh);
            using var candidate = new CandidateHttpClient(ch);
            for (var i = 0; i < 200; i++)
            {
                await Pipeline.Run(baseline, null, fixture);
                await Pipeline.Run(null, candidate, fixture);
            }
            for (var round = 0; round < 7; round++)
            foreach (var optimized in round % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var handler = optimized ? ch : bh;
                var sends = handler.Sends;
                var before = GC.GetTotalAllocatedBytes(true);
                var cpu = process.TotalProcessorTime;
                var timer = Stopwatch.StartNew();
                var checksum = 0;
                for (var i = 0; i < iterations; i++)
                    checksum += await Pipeline.Run(optimized ? null : baseline, optimized ? candidate : null, fixture);
                timer.Stop();
                var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                var allocated = GC.GetTotalAllocatedBytes(true) - before;
                if (checksum != iterations * fixture.Count || handler.Sends - sends != iterations)
                    throw new InvalidOperationException("Replay result or send count mismatch");
                csv.WriteLine(FormattableString.Invariant($"{fixture.Name},{(optimized ? "offset" : "baseline")},{round},{iterations},{fixture.Bytes.Length},{timer.Elapsed.TotalMilliseconds:F4},{cpuMs:F4},{allocated},{handler.Sends - sends},{checksum}"));
                csv.Flush();
            }
            Console.WriteLine($"Completed {fixture.Name}");
        }
    }

    private static async Task MeasureErrors(string output)
    {
        using var csv = new StreamWriter(Path.Combine(output, "errors.csv"));
        csv.WriteLine("case,round,iterations,wall_ms,cpu_ms,allocated_bytes,sends,exceptions");
        using var process = Process.GetCurrentProcess();
        foreach (var name in new[] { "json-code", "json-empty", "grpc-short", "http-status", "caller-cancel", "transport-timeout" })
        {
            var fixture = name switch
            {
                "json-code" => new Fixture(name, "application/json", Encoding.UTF8.GetBytes("{\"code\":-101,\"message\":\"synthetic failure\"}"), 0),
                "grpc-short" => new Fixture(name, "application/grpc", new byte[4], 0),
                _ => new Fixture(name, "application/json", [], 0)
            };
            using var handler = new ReplayHandler(fixture);
            if (name == "http-status") handler.Status = HttpStatusCode.ServiceUnavailable;
            using var client = new BiliHttpClient(handler);
            async Task Execute()
            {
                using var source = new CancellationTokenSource();
                if (name == "caller-cancel") handler.BeforeResponse = token => { source.Cancel(); token.ThrowIfCancellationRequested(); };
                if (name == "transport-timeout") handler.BeforeResponse = _ => throw new OperationCanceledException("synthetic timeout");
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://replay.invalid/error");
                Exception? observed = null;
                try { using var response = await client.SendAsync(request, source.Token); }
                catch (Exception ex) { observed = ex; }
                // Failure responses are inaccessible to production callers; clean the
                // recorded content here to avoid benchmarking an accumulating leak.
                handler.LastContent?.Dispose();
                if (name == "caller-cancel")
                {
                    if (observed is not OperationCanceledException || !source.IsCancellationRequested)
                        throw new InvalidOperationException("Expected caller cancellation", observed);
                }
                else if (observed is not RichasyKernel.KernelException)
                    throw new InvalidOperationException("Expected wrapped failure", observed);
            }
            for (var i = 0; i < 100; i++) await Execute();
            const int count = 500;
            for (var round = 0; round < 7; round++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var sends = handler.Sends;
                var alloc = GC.GetTotalAllocatedBytes(true);
                var cpu = process.TotalProcessorTime;
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < count; i++) await Execute();
                timer.Stop();
                var elapsedCpu = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                var allocated = GC.GetTotalAllocatedBytes(true) - alloc;
                if (handler.Sends - sends != count) throw new InvalidOperationException("Unexpected send count");
                csv.WriteLine(FormattableString.Invariant($"{name},{round},{count},{timer.Elapsed.TotalMilliseconds:F4},{elapsedCpu:F4},{allocated},{handler.Sends - sends},{count}"));
                csv.Flush();
            }
        }
        File.WriteAllText(Path.Combine(output, "errors-environment.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow, measuredIntegrationSha = Environment.GetEnvironmentVariable("ISSUE58_INTEGRATION_SHA"),
            kernelSha = "2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d", runtime = RuntimeInformation.FrameworkDescription,
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            json = typeof(JsonSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            protobuf = typeof(IMessage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Completed six error cases");
    }
}
