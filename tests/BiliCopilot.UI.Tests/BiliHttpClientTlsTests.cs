// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Http;
using RichasyKernel;
using System.Security.Authentication;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// Proves that <see cref="BiliHttpClient"/> enforces TLS certificate validation by default,
/// while still preserving the cancellation paths of <see cref="BiliHttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>.
/// 正路径只通过公开的 <see cref="BiliHttpClient(HttpMessageHandler)"/> 构造注入显式信任；
/// 测试不读写任何系统证书存储.
/// </summary>
public sealed class BiliHttpClientTlsTests
{
    [Fact]
    public async Task ExplicitlyTrustedHandler_AcceptsLoopbackCertificate()
    {
        using var server = LocalTlsServer.Start();

        // 显式传入手柄是本用例唯一信任来源：只固定本机回环测试证书的指纹.
        using var client = new BiliHttpClient(LoopbackCertificateTrust.CreateHandler(server));
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        // GetStringAsync keeps the transport response without running the Bilibili JSON body check.
        var body = await client.GetStringAsync(request);

        Assert.Equal("tls-probe", body);
        Assert.Equal(1, server.CompletedHandshakes);
        Assert.Equal(1, server.CompletedRequests);
    }

    [Fact]
    public async Task DefaultClient_RejectsUntrustedCertificate_WithoutCompletingHandshake()
    {
        using var server = LocalTlsServer.Start();
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        var exception = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => client.SendAsync(request));

        AssertCertificateFailure(exception);

        // 客户端在验证服务端证书时失败；服务端侧的握手可能已经完成，但没有请求被处理过.
        Assert.True(await server.WaitForHandshakeAttemptAsync(), "服务器应观察到客户端发起的 TLS 握手");
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public void DefaultClientHasNoCertificateCallback()
    {
        // 结构守卫：默认构造的客户端（真实注册路径使用的构造函数）不允许设置证书回调.
        using var client = new BiliHttpClient();
        var handler = HttpClientHandlerInspector.GetHandler(HttpClientHandlerInspector.GetInnerClient(client));

        Assert.True(
            handler.ServerCertificateCustomValidationCallback is null,
            "BiliHttpClient 默认构造函数不应设置 ServerCertificateCustomValidationCallback：" +
            "证书校验必须由平台完成，测试需要信任时只能通过 BiliHttpClient(HttpMessageHandler) 显式注入.");
    }

    [Fact]
    public async Task SendAsync_PreCanceledToken_PreservesCancellation()
    {
        using var server = LocalTlsServer.Start();
        using var client = new BiliHttpClient();
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(request, source.Token));

        // 已取消的令牌在建立连接前就生效.
        Assert.Equal(0, server.HandshakeAttempts);
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task SendAsync_CanceledDuringHandshake_PreservesCancellation()
    {
        using var server = LocalTlsServer.Start(TimeSpan.FromSeconds(20));
        using var client = new BiliHttpClient();
        using var source = new CancellationTokenSource();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        source.CancelAfter(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(request, source.Token));

        // 取消发生在延迟的握手中：握手已被尝试，但从未完成，也没有请求被处理.
        Assert.True(await server.WaitForHandshakeAttemptAsync(), "取消必须发生在握手阶段，而不是连接建立之前");
        Assert.Equal(0, server.CompletedHandshakes);
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task DiResolvedClient_RejectsUntrustedCertificate()
    {
        // 走真实注册路径：应用在 GlobalDependencies.cs:46 调用 KernelExtensions.AddBiliClient（KernelExtensions.cs:23），
        // 那里只注册 BiliHttpClient 单例，不注入任何 HttpMessageHandler，因此容器必须选中校验证书的默认构造函数.
        var builder = Kernel.CreateBuilder().AddBiliClient();
        using var provider = builder.Services.BuildServiceProvider();
        var client = provider.GetRequiredService<BiliHttpClient>();
        using var server = LocalTlsServer.Start();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        var exception = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => client.SendAsync(request));

        AssertCertificateFailure(exception);

        // 客户端在验证服务端证书时失败；服务端侧的握手可能已经完成，但没有请求被处理过.
        Assert.True(await server.WaitForHandshakeAttemptAsync(), "服务器应观察到客户端发起的 TLS 握手");
        Assert.Equal(0, server.CompletedRequests);
    }

    private static void AssertCertificateFailure(Exception exception)
    {
        // 不依赖本地化文本：证书失败的真实证据是异常链里存在 AuthenticationException（由 SslStream 抛出）.
        var chain = GetExceptionChain(exception).ToList();
        var description = string.Join(" -> ", chain.Select(item => item.GetType().Name + ": " + item.Message));

        Assert.True(
            chain.Any(item => item is AuthenticationException),
            "Expected an AuthenticationException in the exception chain, got: " + description);
    }

    private static IEnumerable<Exception> GetExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
