// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Text;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockServiceTests
{
    private const string ValidJson = """
        [
          {
            "cid": "168885122",
            "category": "sponsor",
            "actionType": "skip",
            "segment": [300.019, 600.014],
            "UUID": "d0f1e2a3",
            "videoDuration": 1801
          }
        ]
        """;

    [Fact]
    public async Task GetSegmentsAsync_SendsOnlyBvidAndCid()
    {
        Uri? requestedUri = null;
        List<string>? requestHeaderNames = null;
        var handler = new RecordingHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            requestHeaderNames = [.. request.Headers.Select(header => header.Key)];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidJson, Encoding.UTF8, "application/json"),
            });
        });
        using var client = new HttpClient(handler);
        var service = new SponsorBlockService(client);

        _ = await service.GetSegmentsAsync("BV1xx411c7mD", "168885122");

        Assert.NotNull(requestedUri);
        Assert.Equal("https", requestedUri.Scheme);
        Assert.Equal("bsbsb.top", requestedUri.Host);
        Assert.Equal("/api/skipSegments", requestedUri.AbsolutePath);

        var query = ParseQuery(requestedUri.Query);
        Assert.Equal(2, query.Count);
        Assert.Equal("BV1xx411c7mD", query["videoID"]);
        Assert.Equal("168885122", query["cid"]);

        Assert.NotNull(requestHeaderNames);
        Assert.DoesNotContain("Cookie", requestHeaderNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", requestHeaderNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSegmentsAsync_ParsesResponse()
    {
        using var client = CreateClient(HttpStatusCode.OK, ValidJson);
        var service = new SponsorBlockService(client);

        var segments = await service.GetSegmentsAsync("BV1xx", "168885122");

        var segment = Assert.Single(segments);
        Assert.Equal(SponsorBlockCategory.Sponsor, segment.Category);
        Assert.Equal(300.019, segment.Start, 3);
    }

    [Fact]
    public async Task GetSegmentsAsync_NotFound_ReturnsEmpty()
    {
        using var client = CreateClient(HttpStatusCode.NotFound, "not found");
        var service = new SponsorBlockService(client);

        Assert.Empty(await service.GetSegmentsAsync("BV1xx", "1"));
    }

    [Fact]
    public async Task GetSegmentsAsync_ServerError_ReturnsEmpty()
    {
        using var client = CreateClient(HttpStatusCode.InternalServerError, "boom");
        var service = new SponsorBlockService(client);

        Assert.Empty(await service.GetSegmentsAsync("BV1xx", "1"));
    }

    [Fact]
    public async Task GetSegmentsAsync_MalformedJson_ReturnsEmpty()
    {
        using var client = CreateClient(HttpStatusCode.OK, "{ not json");
        var service = new SponsorBlockService(client);

        Assert.Empty(await service.GetSegmentsAsync("BV1xx", "1"));
    }

    [Fact]
    public async Task GetSegmentsAsync_Timeout_ReturnsEmpty()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler);
        var service = new SponsorBlockService(client, requestTimeout: TimeSpan.FromMilliseconds(50));

        Assert.Empty(await service.GetSegmentsAsync("BV1xx", "1"));
    }

    [Fact]
    public async Task GetSegmentsAsync_SecondCallWithinCachePeriod_DoesNotRequestAgain()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(ValidJson)));
        using var client = new HttpClient(handler);
        var service = new SponsorBlockService(client);

        _ = await service.GetSegmentsAsync("BV1xx", "168885122");
        _ = await service.GetSegmentsAsync("BV1xx", "168885122");

        Assert.Single(handler.RequestCounts);
    }

    [Fact]
    public async Task GetSegmentsAsync_DifferentCid_RequestsAgain()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(ValidJson)));
        using var client = new HttpClient(handler);
        var service = new SponsorBlockService(client);

        _ = await service.GetSegmentsAsync("BV1xx", "111");
        _ = await service.GetSegmentsAsync("BV1xx", "222");

        Assert.Equal(2, handler.RequestCounts.Count);
    }

    [Fact]
    public async Task GetSegmentsAsync_EmptyResponse_IsNotCached()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse("[]")));
        using var client = new HttpClient(handler);
        var service = new SponsorBlockService(client);

        _ = await service.GetSegmentsAsync("BV1xx", "1");
        _ = await service.GetSegmentsAsync("BV1xx", "1");

        Assert.Equal(2, handler.RequestCounts.Count);
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string content)
        => new(new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        })));

    private static HttpResponseMessage JsonResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return result;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> RequestCounts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCounts.Add(request);
            return handler(request, cancellationToken);
        }
    }
}
