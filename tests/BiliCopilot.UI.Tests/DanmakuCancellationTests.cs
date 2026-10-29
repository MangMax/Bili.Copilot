// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;

namespace BiliCopilot.UI.Tests;

public sealed class DanmakuCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Segment_CanceledDuringSend_DoesNotRetry(bool web)
    {
        using var source = new CancellationTokenSource();
        using var handler = new RecordingHandler(_ =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        });
        using var client = new BiliHttpClient(handler);
        var service = new DanmakuService(client, new BiliAuthenticator(null, null), null!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => web
            ? service.GetSegmentDanmakusWithWebAsync("1", "2", 1, source.Token)
            : service.GetSegmentDanmakusWithGrpcAsync("1", "2", 1, source.Token));
        Assert.Equal(1, handler.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Segment_OrdinaryFailure_RetriesThreeTimes(bool web)
    {
        using var handler = new RecordingHandler(_ => throw new HttpRequestException("transport failed"));
        using var client = new BiliHttpClient(handler);
        var service = new DanmakuService(client, new BiliAuthenticator(null, null), null!);
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => web
            ? service.GetSegmentDanmakusWithWebAsync("1", "2", 1)
            : service.GetSegmentDanmakusWithGrpcAsync("1", "2", 1));
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task HttpSend_CallerCanceled_PreservesCancellation()
    {
        using var client = new BiliHttpClient();
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(request, source.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Segment_AlreadyCanceled_DoesNotSend(bool web)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var handler = new RecordingHandler(_ => throw new InvalidOperationException("must not send"));
        using var client = new BiliHttpClient(handler);
        var service = new DanmakuService(client, new BiliAuthenticator(null, null), null!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => web
            ? service.GetSegmentDanmakusWithWebAsync("1", "2", 1, source.Token)
            : service.GetSegmentDanmakusWithGrpcAsync("1", "2", 1, source.Token));
        Assert.Equal(0, handler.Attempts);
    }

    [Fact]
    public async Task HttpSend_TimeoutWithoutCallerCancellation_KeepsWrappedFailure()
    {
        using var handler = new RecordingHandler(_ => throw new OperationCanceledException("transport timeout"));
        using var client = new BiliHttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => client.SendAsync(request));
        Assert.Equal(1, handler.Attempts);
    }

    private sealed class RecordingHandler(Func<CancellationToken, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(send(cancellationToken));
        }
    }
}
