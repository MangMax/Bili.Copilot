// Copyright (c) Bili Copilot. All rights reserved.

using System.Security.Authentication;
using System.Text;
using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Tests.Support;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 封面图客户端必须走正常的 TLS 证书校验：合法证书可下载、错误证书失败、取消令牌仍可中止请求。
/// 使用本机回环 HTTPS 服务器与临时信任的证书，不访问公网。
/// </summary>
[Collection(CurrentUserCertificateTrustCollection.Name)]
public sealed class ImageClientTlsTests
{
    [Fact]
    public async Task ImageClientDownloadsBodyWhenCertificateIsTrusted()
    {
        using var server = LocalTlsServer.Start();
        using var trust = CurrentUserCertificateTrust.Install(server.Certificate);

        var bytes = await InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg"));

        Assert.Equal("tls-probe", Encoding.ASCII.GetString(bytes));
        Assert.Equal(1, server.CompletedHandshakes);
        Assert.Equal(1, server.CompletedRequests);
    }

    [Fact]
    public async Task ImageClientRejectsUntrustedCertificate()
    {
        using var server = LocalTlsServer.Start();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg")));

        Assert.IsAssignableFrom<AuthenticationException>(exception.InnerException);

        // 客户端拒绝证书：服务器看得到握手尝试，但没有一次完成.
        // 客户端在验证服务端证书时失败；服务端侧的握手可能已经完成，但没有请求被处理过.
        Assert.True(await server.WaitForHandshakeAttemptAsync(), "服务器应观察到客户端发起的 TLS 握手");
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task ImageClientHonorsPreCanceledToken()
    {
        using var server = LocalTlsServer.Start();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // 已取消的令牌必须直接抛出取消异常，不会发出请求，也不会有握手.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg"), cancellation.Token));

        Assert.Equal(0, server.HandshakeAttempts);
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task ImageClientHonorsCancellationWhileHandshakeIsPending()
    {
        using var server = LocalTlsServer.Start(TimeSpan.FromSeconds(20));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg"), cancellation.Token));

        // 取消发生在延迟的握手中：握手已被尝试，但从未完成，也没有请求被处理.
        Assert.True(await server.WaitForHandshakeAttemptAsync(), "取消必须发生在握手阶段，而不是连接建立之前");
        Assert.Equal(0, server.CompletedHandshakes);
        Assert.Equal(0, server.CompletedRequests);
    }
}
