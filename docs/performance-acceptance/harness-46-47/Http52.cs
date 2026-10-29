using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace Acceptance;

// Synthetic HTTP/1.1 server, no credentials, cookies or external network.
internal sealed class Http52 : IDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stop = new();
    readonly ConcurrentBag<TcpClient> clients = new();
    public readonly ConcurrentQueue<object> Events = new();
    public TaskCompletionSource SlowStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseSlow = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Connections, Requests, Active, Peak;
    public string Url { get; }
    readonly Dictionary<string, byte[]> fixtures = new();
    public Http52()
    {
        fixtures["landscape"] = Bmp(2400, 1200);
        fixtures["portrait"] = Bmp(1200, 2400);
        fixtures["square"] = Bmp(2000, 2000);
        fixtures["svg"] = Encoding.Latin1.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"240\" height=\"100\"><rect width=\"240\" height=\"100\" fill=\"teal\"/><text x=\"20\" y=\"60\" font-size=\"40\" fill=\"white\">café</text></svg>");
        listener.Start(); Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
        _ = Accept();
    }
    public object Manifest => fixtures.Select(x => new { name=x.Key, bytes=x.Value.Length, sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(x.Value)) }).ToArray();
    async Task Accept()
    {
        try { while (!stop.IsCancellationRequested) { var c = await listener.AcceptTcpClientAsync(stop.Token); clients.Add(c); var id=Interlocked.Increment(ref Connections); _=Serve(c,id); } }
        catch (OperationCanceledException) { }
    }
    async Task Serve(TcpClient client, int id)
    {
        using (client)
        try
        {
            using var stream=client.GetStream();
            using var reader=new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            while (!stop.IsCancellationRequested)
            {
                var line=await reader.ReadLineAsync(stop.Token); if (line==null) break;
                var path=line.Split(' ')[1].TrimStart('/').Split('?')[0];
                var headers=new List<string>(); string h;
                while (!string.IsNullOrEmpty(h=await reader.ReadLineAsync(stop.Token))) headers.Add(h);
                var request=Interlocked.Increment(ref Requests); var active=Interlocked.Increment(ref Active);
                lock (fixtures) Peak=Math.Max(Peak,active);
                Events.Enqueue(new { kind="request", id, request, path, active, headers, utc=DateTimeOffset.UtcNow });
                try
                {
                    var body=fixtures[path=="slow" ? "portrait" : path];
                    var type=path=="svg" ? "image/svg+xml; charset=iso-8859-1" : "image/bmp";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: keep-alive\r\n\r\n"),stop.Token);
                    if (path=="slow") { await stream.WriteAsync(body.AsMemory(0,64),stop.Token); SlowStarted.TrySetResult(); await ReleaseSlow.Task.WaitAsync(stop.Token); await stream.WriteAsync(body.AsMemory(64),stop.Token); }
                    else await stream.WriteAsync(body,stop.Token);
                    Events.Enqueue(new { kind="body-sent",id,request,path,utc=DateTimeOffset.UtcNow });
                }
                finally { Interlocked.Decrement(ref Active); }
            }
        }
        catch (Exception e) { Events.Enqueue(new { kind="connection-end",id,error=e.GetType().Name,utc=DateTimeOffset.UtcNow }); }
    }
    static byte[] Bmp(int width,int height)
    {
        var stride=(width*3+3)&~3; var data=new byte[54+stride*height];
        using var writer=new BinaryWriter(new MemoryStream(data));
        writer.Write((ushort)0x4d42); writer.Write(data.Length); writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0); writer.Write(stride*height); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        for (var y=0;y<height;y++) for (var x=0;x<width;x++) { var p=54+y*stride+x*3; data[p]=(byte)(x*255/width); data[p+1]=(byte)(y*255/height); data[p+2]=(byte)((x/100+y/100)%2==0?210:40); }
        return data;
    }
    public void Dispose() { stop.Cancel(); listener.Stop(); foreach(var c in clients) c.Dispose(); }
}
