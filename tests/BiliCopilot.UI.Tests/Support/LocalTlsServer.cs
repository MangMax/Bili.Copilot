// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// Loopback HTTPS server presenting a freshly generated self-signed certificate.
/// Tests use it to prove that TLS certificate validation actually runs: a request to an
/// untrusted certificate must fail, and only an explicitly trusted certificate may succeed.
/// </summary>
internal sealed class LocalTlsServer : IDisposable
{
    private const string ResponseBody = "tls-probe";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _acceptLoop;
    private readonly TimeSpan _handshakeDelay;
    private int _requestCount;

    private LocalTlsServer(X509Certificate2 certificate, TimeSpan handshakeDelay)
    {
        Certificate = certificate;
        _handshakeDelay = handshakeDelay;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        BaseAddress = new Uri($"https://127.0.0.1:{endpoint.Port}/");
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>
    /// Certificate presented to clients. Never trusted by the machine unless
    /// <see cref="CurrentUserCertificateTrust.Install"/> is called.
    /// </summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>
    /// HTTPS root address of the listener, using the loopback IP covered by the certificate.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Number of connections that completed the TLS handshake.
    /// </summary>
    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>
    /// Starts the listener.
    /// </summary>
    /// <param name="handshakeDelay">Optional delay before the TLS handshake, to test cancellation.</param>
    public static LocalTlsServer Start(TimeSpan? handshakeDelay = null)
        => new(CreateSelfSignedCertificate(), handshakeDelay ?? TimeSpan.Zero);

    /// <summary>
    /// Builds an absolute URI below <see cref="BaseAddress"/>.
    /// </summary>
    public Uri GetUri(string relativePath = "") => new(BaseAddress, relativePath);

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

        _shutdown.Dispose();
        Certificate.Dispose();
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

            _ = Task.Run(() => HandleConnectionAsync(client));
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
                if (_handshakeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_handshakeDelay, timeout.Token).ConfigureAwait(false);
                }

                await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate = Certificate,
                        ClientCertificateRequired = false,
                    },
                    timeout.Token).ConfigureAwait(false);

                await ReadRequestHeadersAsync(ssl, timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _requestCount);

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
        var buffer = new byte[1024];
        var tail = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(tail), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            tail += read;
            if (tail >= 4 && buffer.AsSpan(tail - 4, 4).SequenceEqual("\r\n\r\n"u8))
            {
                return;
            }

            if (tail == buffer.Length)
            {
                tail = 0;
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
