// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Security.Authentication;
using System.Text;
using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Tests.Support;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 封面图客户端必须走正常的 TLS 证书校验：合法证书可下载、错误证书失败、取消令牌仍可中止请求。
/// 使用本机回环 HTTPS 服务器与临时信任的证书，不访问公网。
/// </summary>
public sealed class ImageClientTlsTests
{
    [Fact]
    public async Task ImageClientDownloadsBodyWhenCertificateIsTrusted()
    {
        using var server = LocalTlsServer.Start();
        using var trust = CurrentUserCertificateTrust.Install(server.Certificate);

        var bytes = await InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg"));

        Assert.Equal("tls-probe", Encoding.ASCII.GetString(bytes));
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task ImageClientRejectsUntrustedCertificate()
    {
        using var server = LocalTlsServer.Start();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg")));

        Assert.IsAssignableFrom<AuthenticationException>(exception.InnerException);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public async Task ImageClientHonorsCancellationWhileHandshakeIsPending()
    {
        using var server = LocalTlsServer.Start(TimeSpan.FromSeconds(20));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InternalHttpExtensions.ImageClient.GetByteArrayAsync(server.GetUri("cover.jpg"), cancellation.Token));

        Assert.Equal(0, server.RequestCount);
    }
}
