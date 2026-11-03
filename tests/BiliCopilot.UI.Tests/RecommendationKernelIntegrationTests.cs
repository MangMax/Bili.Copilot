using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;

namespace BiliCopilot.UI.Tests;

public sealed class RecommendationKernelIntegrationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task ColumnReachesTransportAndPageSizeIsNeverSent(int column)
    {
        using var source = new CancellationTokenSource();
        using var handler = new Handler(source);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), null!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetRecommendVideoListAsync(42, column, source.Token));
        Assert.Equal(1, handler.Attempts);
        Assert.Contains("idx=42", handler.Query);
        // 推荐流不认 ps，旧实现传的页大小既无效又误导，必须彻底消失。
        Assert.DoesNotContain("ps=", handler.Query);
        Assert.Contains(column > 0 ? "column=8" : "column=7", handler.Query);
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
