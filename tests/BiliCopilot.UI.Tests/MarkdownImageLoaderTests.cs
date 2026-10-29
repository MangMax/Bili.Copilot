using System.Net;
using System.Net.Sockets;
using System.Text;
using BiliCopilot.UI.Controls.Markdown;

namespace BiliCopilot.UI.Tests;

public class MarkdownImageLoaderTests
{
    private static readonly Uri ImageUri = new("https://images.example/test.png");

    [Theory]
    [InlineData(4000, 2000, 300, 0, 800, 2, 600, 300, 300, 150)]
    [InlineData(4000, 2000, 0, 100, 800, 1.5, 300, 150, 200, 100)]
    [InlineData(4000, 2000, 600, 200, 800, 1, 400, 200, 400, 200)]
    [InlineData(4000, 2000, 0, 0, 500, 2, 1000, 500, 500, 250)]
    [InlineData(100, 50, 300, 0, 800, 2, 100, 50, 300, 150)]
    [InlineData(10000, 10000, 10000, 10000, 10000, 2, 2048, 2048, 10000, 10000)]
    [InlineData(100000, 100, 100000, 0, 100000, 1, 4096, 4, 100000, 100)]
    public void DecodeFitsDisplayDpiAndPixelBudget(int sourceWidth, int sourceHeight, double width, double height, double available, double dpi,
        int expectedWidth, int expectedHeight, double displayWidth, double displayHeight)
    {
        var result = MarkdownImageLoader.GetDecodeSize((uint)sourceWidth, (uint)sourceHeight, width, height, available, dpi);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.Equal(displayWidth, result.DisplayWidth, 5);
        Assert.Equal(displayHeight, result.DisplayHeight, 5);
    }

    [Fact]
    public async Task OversizedDeclaredResponseIsRejectedBeforeConsumption()
    {
        using var content = new ByteArrayContent([1]);
        content.Headers.ContentLength = 32 * 1024 * 1024 + 1;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content })));
        var loader = new MarkdownImageLoader(client);
        var consumed = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadAsync(ImageUri, (_, _, _) =>
        {
            consumed = true;
            return Task.CompletedTask;
        }, default));
        Assert.False(consumed);
    }

    [Fact]
    public async Task FourDecodesHoldSlotsAndCancelledWaiterNeverSendsRequest()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            Interlocked.Increment(ref requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        }));
        var loader = new MarkdownImageLoader(client);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodes = 0;
        var active = Enumerable.Range(0, 4).Select(_ => loader.LoadAsync(ImageUri, async (_, _, token) =>
        {
            if (Interlocked.Increment(ref decodes) == 4) entered.SetResult();
            await release.Task.WaitAsync(token);
        }, default)).ToArray();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var queued = loader.LoadAsync(ImageUri, (_, _, _) => Task.CompletedTask, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            Assert.Equal(4, requests);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(active);
        }
    }

    [Fact]
    public async Task HttpFailureDoesNotReachDecoderAndReleasesResponse()
    {
        var body = new TrackingStream([1]);
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StreamContent(body) })));
        var loader = new MarkdownImageLoader(client);
        var consumed = false;
        await Assert.ThrowsAsync<HttpRequestException>(() => loader.LoadAsync(ImageUri, (_, _, _) =>
        {
            consumed = true;
            return Task.CompletedTask;
        }, default));
        Assert.False(consumed);
        Assert.True(body.Disposed);
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    [Theory]
    [InlineData(33554431, true)]
    [InlineData(33554432, true)]
    [InlineData(33554433, false)]
    public async Task UnknownLengthBodyEnforcesBoundaryAndDisposesBuffers(int bytes, bool accepted)
    {
        var source = new GeneratedStream(bytes);
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) })));
        var loader = new MarkdownImageLoader(client);
        Stream? delivered = null;
        var task = loader.LoadAsync(ImageUri, (stream, contentType, _) =>
        {
            delivered = stream;
            Assert.Null(contentType);
            Assert.True(stream.CanSeek);
            Assert.Equal(0, stream.Position);
            Assert.Equal(bytes, stream.Length);
            Assert.Equal(42, stream.ReadByte());
            return Task.CompletedTask;
        }, default);
        if (accepted)
        {
            await task;
            Assert.NotNull(delivered);
            Assert.False(delivered.CanRead);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => task);
            Assert.Null(delivered);
        }

        Assert.True(source.Disposed);
        Assert.InRange(source.BytesRead, 1, 33554433);
    }

    [Fact]
    public async Task CancelledBodyCannotReachDecoderEvenWhenStreamIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new DelayedStream();
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) })));
        var loader = new MarkdownImageLoader(client);
        var consumed = false;
        var load = loader.LoadAsync(ImageUri, (_, _, _) => { consumed = true; return Task.CompletedTask; }, cancellation.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.True(source.Token.IsCancellationRequested);
        source.Release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.False(consumed);
        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task CancellationReachesHttpAndDecoderAndReleasesTheirSlots()
    {
        var sent = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new Handler(async (_, token) =>
        {
            if (sent.TrySetResult(token))
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
        }));
        var loader = new MarkdownImageLoader(client);
        using var cancelledHttp = new CancellationTokenSource();
        var first = loader.LoadAsync(ImageUri, (_, _, _) => throw new InvalidOperationException("Cancelled HTTP reached decoder"), cancelledHttp.Token);
        var transportToken = await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancelledHttp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.True(transportToken.IsCancellationRequested);

        // More than four cancelled decodes must still allow subsequent work.
        for (var i = 0; i < 5; i++)
        {
            using var cancelledDecode = new CancellationTokenSource();
            Stream? buffer = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loader.LoadAsync(ImageUri, async (stream, _, token) =>
            {
                buffer = stream;
                cancelledDecode.Cancel();
                await Task.Delay(Timeout.Infinite, token);
            }, cancelledDecode.Token));
            Assert.NotNull(buffer);
            Assert.False(buffer.CanRead);
        }

        await loader.LoadAsync(ImageUri, (_, _, _) => Task.CompletedTask, default);
    }

    [Theory]
    [InlineData("file:///C:/image.png")]
    [InlineData("ftp://images.example/image.png")]
    [InlineData("relative.png")]
    public async Task UnsupportedUriNeverReachesHttp(string address)
    {
        using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Unexpected HTTP")));
        var loader = new MarkdownImageLoader(client);
        await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync(new Uri(address, UriKind.RelativeOrAbsolute), (_, _, _) => Task.CompletedTask, default));
    }

    [Fact]
    public async Task DecoderFailureReleasesResponseBufferAndSlot()
    {
        var bodies = new List<TrackingStream>();
        using var client = new HttpClient(new Handler((_, _) =>
        {
            var body = new TrackingStream([1, 2, 3]);
            bodies.Add(body);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        }));
        var loader = new MarkdownImageLoader(client);
        for (var i = 0; i < 5; i++)
        {
            Stream? delivered = null;
            await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadAsync(ImageUri, (stream, _, _) =>
            {
                delivered = stream;
                throw new InvalidDataException("Bad image");
            }, default));
            Assert.NotNull(delivered);
            Assert.False(delivered.CanRead);
            Assert.True(bodies[i].Disposed);
        }
    }

    [Fact]
    public async Task SharedClientReusesConnectionWithoutReplayingCookiesOrCredentials()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var uri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/image");
        var requests = new List<string>();
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(deadline.Token);
            using var network = socket.GetStream();
            using var reader = new StreamReader(network, Encoding.ASCII, leaveOpen: true);
            // Both requests must arrive on this same TCP connection.
            for (var i = 0; i < 2; i++)
            {
                var headers = new StringBuilder();
                while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 } line)
                {
                    headers.AppendLine(line);
                }

                requests.Add(headers.ToString());
                await network.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 3\r\nContent-Type: image/svg+xml\r\nSet-Cookie: account=secret; Path=/\r\nConnection: keep-alive\r\n\r\nsvg"), deadline.Token);
            }
        }, deadline.Token);
        try
        {
            for (var i = 0; i < 2; i++)
            {
                await MarkdownImageLoader.Shared.LoadAsync(uri, async (stream, contentType, token) =>
                {
                    Assert.Equal("image/svg+xml", contentType);
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    Assert.Equal("svg", await reader.ReadToEndAsync(token));
                }, deadline.Token);
            }

            await server;
            Assert.Equal(2, requests.Count);
            Assert.All(requests, request =>
            {
                Assert.StartsWith("GET /image HTTP/1.1", request);
                Assert.DoesNotContain("Cookie:", request, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Authorization:", request, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            try { await server; } catch (OperationCanceledException) { }
        }
    }

    private class GeneratedStream(int length) : Stream
    {
        public bool Disposed { get; private set; }
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, length - BytesRead);
            buffer.AsSpan(offset, read).Fill(42);
            BytesRead += read;
            return read;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..read].Fill(42);
            BytesRead += read;
            return ValueTask.FromResult(read);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DelayedStream() : GeneratedStream(1)
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Entered.TrySetResult();
            await Release.Task;
            return await base.ReadAsync(buffer, default);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
