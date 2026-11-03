// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// Loopback HTTPS server presenting a self-signed certificate.
/// Tests use it to prove that TLS certificate validation actually runs: a request to an
/// untrusted certificate must fail, and only an explicitly trusted certificate may succeed.
/// </summary>
internal sealed class LocalTlsServer : IDisposable
{
    private const string ResponseBody = "tls-probe";

    // 自签名证书（RSA 2048 密钥生成）整轮测试只生成一次：既只支付一次密钥生成的时间，
    // 也避免受信任证书测试在两次服务器之间留下半安装的当前用户根证书条目.
    private static readonly Lazy<X509Certificate2> SharedCertificate =
        new(CreateSelfSignedCertificate, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly TcpListener _listener;
    private readonly X509Certificate2 _certificate;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _acceptLoop;
    private readonly TimeSpan _handshakeDelay;
    private readonly ConcurrentDictionary<long, Task> _connections = new();
    private long _connectionSequence;
    private int _handshakeAttempts;
    private int _completedHandshakes;
    private int _completedRequests;

    private LocalTlsServer(X509Certificate2 certificate, TimeSpan handshakeDelay)
    {
        _certificate = certificate;
        _handshakeDelay = handshakeDelay;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        BaseAddress = new Uri($"https://127.0.0.1:{endpoint.Port}/");
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>
    /// Certificate presented to clients. Never trusted by the machine unless
    /// <see cref="CurrentUserCertificateTrust.Install"/> is called. It is generated once per test run
    /// and owned by the process-wide holder, so a server never disposes it.
    /// </summary>
    public X509Certificate2 Certificate => _certificate;

    /// <summary>
    /// HTTPS root address of the listener, using the loopback IP covered by the certificate.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// 进入 TLS 握手阶段的连接数：连接建立后立刻计数，<see cref="Start"/> 的延迟用来模拟卡住的握手.
    /// 与 <see cref="CompletedHandshakes"/> 对照，才能区分“握手被拒绝/被取消”和“连接根本没建立”.
    /// </summary>
    public int HandshakeAttempts => Volatile.Read(ref _handshakeAttempts);

    /// <summary>
    /// 完成 TLS 握手的连接数.
    /// </summary>
    public int CompletedHandshakes => Volatile.Read(ref _completedHandshakes);

    /// <summary>
    /// 读到完整请求头并开始回应的请求数.
    /// </summary>
    public int CompletedRequests => Volatile.Read(ref _completedRequests);

    /// <summary>
    /// Starts the listener.
    /// </summary>
    /// <param name="handshakeDelay">Optional delay before the TLS handshake, to test cancellation.</param>
    public static LocalTlsServer Start(TimeSpan? handshakeDelay = null)
        => new(SharedCertificate.Value, handshakeDelay ?? TimeSpan.Zero);

    /// <summary>
    /// Builds an absolute URI below <see cref="BaseAddress"/>.
    /// </summary>
    public Uri GetUri(string relativePath = "") => new(BaseAddress, relativePath);

    /// <summary>
    /// 等待至少 <paramref name="minimum"/> 个连接进入 TLS 握手阶段.
    /// 客户端失败返回后服务器任务可能还没登记握手，这里用有上限的等待消除调度竞态；
    /// 超时返回 false，由调用方断言报告.
    /// </summary>
    public async Task<bool> WaitForHandshakeAttemptAsync(int minimum = 1, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (HandshakeAttempts < minimum)
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        return true;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
        }

        try
        {
            _acceptLoop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        // 连接任务还在后台运行，可能仍在使用证书与流；先给它们一小段时间退出，再释放共享资源.
        var pending = _connections.Values.ToArray();
        if (pending.Length > 0)
        {
            try
            {
                Task.WaitAll(pending, TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }
        }

        _shutdown.Dispose();

        // 证书由进程级共享持有者拥有，不在单个服务器释放，否则其它服务器的连接会拿到已释放的证书.
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));

        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), password: null, X509KeyStorageFlags.Exportable);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            var id = Interlocked.Increment(ref _connectionSequence);
            var connection = Task.Run(() => HandleConnectionAsync(client));
            _connections[id] = connection;
            _ = connection.ContinueWith(
                completed =>
                {
                    _connections.TryRemove(id, out var _);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                // 证书由共享持有者拥有，不会在连接使用期间被释放；先取出引用再使用.
                var certificate = Certificate;

                // 连接已建立，TLS 握手阶段开始（延迟用于模拟客户端等待 ServerHello 的卡住握手）.
                Interlocked.Increment(ref _handshakeAttempts);

                if (_handshakeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_handshakeDelay, timeout.Token).ConfigureAwait(false);
                }

                await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        ClientCertificateRequired = false,
                    },
                    timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _completedHandshakes);

                await ReadRequestHeadersAsync(ssl, timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _completedRequests);

                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " +
                    ResponseBody.Length +
                    "\r\nConnection: close\r\n\r\n" +
                    ResponseBody);
                await ssl.WriteAsync(response, timeout.Token).ConfigureAwait(false);
                await ssl.FlushAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The client rejected the certificate, gave up, or the server is shutting down.
            }
        }
    }

    private static async Task ReadRequestHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        // 请求头很小，直接读满固定缓冲区即可；读不下就说明不是正常请求，不再做回绕处理.
        var buffer = new byte[1024];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            filled += read;
            if (filled >= 4 && buffer.AsSpan(filled - 4, 4).SequenceEqual("\r\n\r\n"u8))
            {
                return;
            }
        }
    }
}

/// <summary>
/// Temporarily trusts a certificate for the current Windows user, so a test can prove the
/// positive path (a valid certificate is accepted) without touching a public CA.
/// </summary>
internal sealed class CurrentUserCertificateTrust : IDisposable
{
    private readonly X509Store _store;
    private readonly X509Certificate2 _certificate;
    private readonly bool _added;

    private CurrentUserCertificateTrust(X509Store store, X509Certificate2 certificate, bool added)
    {
        _store = store;
        _certificate = certificate;
        _added = added;
    }

    /// <summary>
    /// Adds <paramref name="certificate"/> to the current user's trusted root store until disposed.
    /// </summary>
    public static CurrentUserCertificateTrust Install(X509Certificate2 certificate)
    {
        var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        var alreadyPresent = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false).Count > 0;
        if (!alreadyPresent)
        {
            store.Add(publicOnly);
        }

        return new CurrentUserCertificateTrust(store, publicOnly, !alreadyPresent);
    }

    public void Dispose()
    {
        try
        {
            if (_added)
            {
                _store.Remove(_certificate);
            }
        }
        finally
        {
            _store.Dispose();
            _certificate.Dispose();
        }
    }
}
