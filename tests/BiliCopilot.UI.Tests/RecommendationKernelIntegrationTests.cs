// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;

namespace BiliCopilot.UI.Tests;

public sealed class RecommendationKernelIntegrationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public async Task PageSizeAndCursorReachTransportAndCallerCancellationIsPreserved(int pageSize)
    {
        using var source = new CancellationTokenSource();
        using var handler = new Handler(source);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), null!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetRecommendVideoListAsync(42, pageSize, source.Token));
        Assert.Equal(1, handler.Attempts);
        Assert.Contains("idx=42", handler.Query);
        if (pageSize > 0) Assert.Contains("ps=30", handler.Query);
        else Assert.DoesNotContain("ps=", handler.Query);
    }

    private sealed class Handler(CancellationTokenSource source) : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        public string Query { get; private set; } = string.Empty;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            Query = request.RequestUri!.Query;
            source.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }
    }
}
