// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Richasy.BiliKernel.Http;
using System.Security.Authentication;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// Proves that <see cref="BiliHttpClient"/> enforces TLS certificate validation by default,
/// while still preserving the cancellation paths of <see cref="BiliHttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>.
/// </summary>
public sealed class BiliHttpClientTlsTests
{
    [Fact]
    public async Task DefaultClient_RejectsUntrustedCertificate_WithoutCompletingHandshake()
    {
        using var server = LocalTlsServer.Start();
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        var exception = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => client.SendAsync(request));

        AssertCertificateFailure(exception);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public async Task DefaultClient_AcceptsTrustedCertificate()
    {
        using var server = LocalTlsServer.Start();
        using var trust = CurrentUserCertificateTrust.Install(server.Certificate);
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        // GetStringAsync keeps the transport response without running the Bilibili JSON body check.
        var body = await client.GetStringAsync(request);

        Assert.Equal("tls-probe", body);
        Assert.Equal(1, server.RequestCount);
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

        Assert.Equal(0, server.RequestCount);
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

        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public async Task DiResolvedClient_RejectsUntrustedCertificate()
    {
        // Mirrors KernelExtensions.AddBiliClient: only the client is registered, no HttpMessageHandler,
        // so the container must pick the parameterless constructor.
        var services = new ServiceCollection();
        services.AddSingleton<BiliHttpClient>();
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(HttpMessageHandler));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<BiliHttpClient>();
        using var server = LocalTlsServer.Start();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.GetUri());

        var exception = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => client.SendAsync(request));

        AssertCertificateFailure(exception);
        Assert.Equal(0, server.RequestCount);
    }

    private static void AssertCertificateFailure(Exception exception)
    {
        var chain = GetExceptionChain(exception).ToList();
        var description = string.Join(" -> ", chain.Select(item => item.GetType().Name + ": " + item.Message));

        var namesCertificate = chain.Any(item =>
            item is AuthenticationException ||
            item.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("证书", StringComparison.Ordinal));

        Assert.True(namesCertificate, "Expected a certificate/SSL failure in the exception chain, got: " + description);
        Assert.Contains(chain, item => item is HttpRequestException || item is AuthenticationException);
    }

    private static IEnumerable<Exception> GetExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
