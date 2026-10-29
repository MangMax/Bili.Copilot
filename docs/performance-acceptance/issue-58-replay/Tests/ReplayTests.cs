using System.Net;
using System.Text;
using System.Text.Json;
using Bilibili.Community.Service.Dm.V1;
using Google.Protobuf;
using Richasy.BiliKernel.Http;
using RichasyKernel;

namespace Issue58.Tests;

public sealed class ReplayTests
{
    private static Fixture Text(string text) => new("contract", "application/json", Encoding.UTF8.GetBytes(text), 0);
    private static HttpRequestMessage Request() => new(HttpMethod.Get, "https://replay.invalid/contract");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Success_ReturnsBusinessResult_OneSendAndOneBodySerialization(bool grpc)
    {
        var fixture = grpc ? Fixtures.Grpc(20) : Fixtures.Json(20);
        foreach (var optimized in new[] { false, true })
        {
            var handler = new ReplayHandler(fixture);
            using var baseline = optimized ? null : new BiliHttpClient(handler);
            using var candidate = optimized ? new CandidateHttpClient(handler) : null;
            Assert.Equal(20, await Pipeline.Run(baseline, candidate, fixture));
            Assert.Equal(1, handler.Sends);
            Assert.Equal(1, handler.LastContent!.Serializations);
            Assert.True(handler.LastContent.Disposed);
        }
    }

    [Theory]
    [InlineData(-101)] [InlineData(-400)] [InlineData(1)]
    public async Task Json_ErrorCode_PreservesMessageAndRawBody(int code)
    {
        var fixture = Text($"{{\"code\":{code},\"message\":\"synthetic failure\",\"data\":[]}}");
        using var handler = new ReplayHandler(fixture);
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        var ex = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));
        Assert.Equal("哔哩哔哩返回了一个异常响应: synthetic failure", ex.Message);
        Assert.Equal(Encoding.UTF8.GetString(fixture.Bytes), ex.InnerException!.Message);
        Assert.Equal(1, handler.Sends);
        // Current validator failure does not dispose the inaccessible response.
        Assert.False(handler.LastContent!.Disposed);
        handler.LastContent.Dispose();
    }

    [Theory]
    [InlineData("")] [InlineData(" \r\n")]
    public async Task Json_Empty_ThrowsKernelEmptyResponse(string text)
    {
        using var handler = new ReplayHandler(Text(text));
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        var ex = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));
        Assert.Equal("哔哩哔哩返回了一个空的响应", ex.Message);
        handler.LastContent!.Dispose();
    }

    [Theory]
    [InlineData("{")] [InlineData("null")]
    public async Task Json_InvalidOrNull_WrappedValidationFailure(string text)
    {
        using var handler = new ReplayHandler(Text(text));
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        var ex = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));
        Assert.StartsWith("未知错误：", ex.Message);
        Assert.IsType(text == "null" ? typeof(NullReferenceException) : typeof(JsonException), ex.InnerException);
        handler.LastContent!.Dispose();
    }

    [Fact]
    public async Task Json_MissingCode_DefaultsToSuccess_AndBusinessShapeFailureIsUnwrapped()
    {
        using var handler = new ReplayHandler(Text("{\"data\":\"wrong shape\"}"));
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        using var response = await client.SendAsync(request);
        await Assert.ThrowsAsync<JsonException>(() => BiliHttpClient.ParseAsync(response, ReplayJsonContext.Default.BiliDataResponseListItem));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(5)]
    public async Task Grpc_LengthBoundary_PreservesEmptyMessageAndRejection(int length)
    {
        foreach (var optimized in new[] { false, true })
        {
            var fixture = new Fixture("boundary", "application/grpc", new byte[length], 0);
            using var handler = new ReplayHandler(fixture);
            using var baseline = optimized ? null : new BiliHttpClient(handler);
            using var candidate = optimized ? new CandidateHttpClient(handler) : null;
            if (length < 5)
            {
                var ex = await Assert.ThrowsAsync<KernelException>(() => Pipeline.Run(baseline, candidate, fixture));
                Assert.Equal("哔哩哔哩返回了一个空的响应", ex.Message);
                handler.LastContent!.Dispose();
            }
            else Assert.Equal(0, await Pipeline.Run(baseline, candidate, fixture));
        }
    }

    [Fact]
    public async Task Grpc_OffsetCandidate_PreservesFieldsAndMalformedPayloadException()
    {
        var fixture = Fixtures.Grpc(20);
        using var h1 = new ReplayHandler(fixture);
        using var h2 = new ReplayHandler(fixture);
        using var baseline = new BiliHttpClient(h1);
        using var candidate = new CandidateHttpClient(h2);
        using var q1 = Request(); using var q2 = Request();
        using var r1 = await baseline.SendAsync(q1); using var r2 = await candidate.SendAsync(q2);
        var a = await BiliHttpClient.ParseAsync(r1, DmSegMobileReply.Parser);
        var b = await CandidateHttpClient.ParseAsync(r2, DmSegMobileReply.Parser);
        Assert.Equal(a, b);
        Assert.Equal("synthetic 弹幕 000019", b.Elems[19].Content);
        Assert.Equal(20L, b.Elems[19].Id);
        Assert.Equal(1900, b.Elems[19].Progress);
        using var bad = new HttpResponseMessage { Content = new ByteArrayContent([0, 0, 0, 0, 1, 255]) };
        await Assert.ThrowsAsync<InvalidProtocolBufferException>(() => BiliHttpClient.ParseAsync(bad, DmSegMobileReply.Parser));
        await Assert.ThrowsAsync<InvalidProtocolBufferException>(() => CandidateHttpClient.ParseAsync(bad, DmSegMobileReply.Parser));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)]
    public async Task Grpc_DirectParseShortBody_OffsetPreservesSkipSemantics(int length)
    {
        using var response = new HttpResponseMessage { Content = new ByteArrayContent(new byte[length]) };
        Assert.Empty((await BiliHttpClient.ParseAsync(response, DmSegMobileReply.Parser)).Elems);
        Assert.Empty((await CandidateHttpClient.ParseAsync(response, DmSegMobileReply.Parser)).Elems);
    }

    [Fact]
    public async Task HttpStatusFailure_IsWrappedBeforeJsonValidation()
    {
        using var handler = new ReplayHandler(Text("not json")) { Status = HttpStatusCode.ServiceUnavailable };
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        var ex = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));
        Assert.IsType<HttpRequestException>(ex.InnerException);
        handler.LastContent!.Dispose();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CallerCancellation_IsNotWrapped_AndNoExtraSend(bool beforeSend)
    {
        using var cts = new CancellationTokenSource();
        using var handler = new ReplayHandler(Fixtures.Json(20));
        handler.BeforeResponse = token => { cts.Cancel(); token.ThrowIfCancellationRequested(); };
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        if (beforeSend) cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(request, cts.Token));
        // HttpClient forwards even a pre-canceled token to a custom handler.
        // The service-layer #48 guard, not this HTTP wrapper, prevents scheduling.
        Assert.Equal(1, handler.Sends);
    }

    [Fact]
    public async Task TransportCancellationWithoutCallerCancellation_IsWrapped()
    {
        using var handler = new ReplayHandler(Fixtures.Json(20));
        handler.BeforeResponse = _ => throw new OperationCanceledException("synthetic timeout");
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        var ex = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));
        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
        Assert.Equal(1, handler.Sends);
    }

    [Fact]
    public async Task CancellationAfterSend_DoesNotCancelParse_AndCallerOwnsResponse()
    {
        using var cts = new CancellationTokenSource();
        var handler = new ReplayHandler(Fixtures.Json(20));
        var client = new BiliHttpClient(handler);
        using var request = Request();
        var response = await client.SendAsync(request, cts.Token);
        cts.Cancel();
        var result = await BiliHttpClient.ParseAsync(response, ReplayJsonContext.Default.BiliDataResponseListItem);
        Assert.Equal(20, result.Data!.Count);
        client.Dispose();
        Assert.True(handler.Disposed);
        Assert.False(handler.LastContent!.Disposed);
        response.Dispose();
        Assert.True(handler.LastContent.Disposed);
    }

    [Fact]
    public async Task RepeatedReads_UseOneBodyBuffer_ButReturnDistinctArraysAndStrings()
    {
        using var handler = new ReplayHandler(Fixtures.Json(20));
        using var client = new BiliHttpClient(handler);
        using var request = Request();
        using var response = await client.SendAsync(request);
        var a = await response.Content.ReadAsByteArrayAsync();
        var b = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(a, b); Assert.NotSame(a, b);
        a[0] = 0;
        Assert.Equal((byte)'{', b[0]);
        var x = await response.Content.ReadAsStringAsync();
        var y = await response.Content.ReadAsStringAsync();
        Assert.Equal(x, y); Assert.NotSame(x, y);
        Assert.Equal(1, handler.Sends);
        Assert.Equal(1, handler.LastContent!.Serializations);
    }
}
