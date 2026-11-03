// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// Explicit, test-local certificate trust for the loopback TLS server.
/// It pins the server's thumbprint in a handler callback; it never reads or writes an OS
/// certificate store. This mirrors the production seam (an injected <see cref="HttpMessageHandler"/>)
/// rather than changing machine/user trust.
/// </summary>
internal static class LoopbackCertificateTrust
{
    /// <summary>
    /// Creates a handler that trusts only the certificate presented by <paramref name="server"/>.
    /// </summary>
    /// <param name="server">Loopback server whose thumbprint is pinned.</param>
    /// <returns>A handler with an explicit, thumbprint-scoped validation callback.</returns>
    public static HttpClientHandler CreateHandler(LocalTlsServer server)
    {
        var thumbprint = server.Certificate.Thumbprint;
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null
                && string.Equals(certificate.GetCertHashString(), thumbprint, StringComparison.OrdinalIgnoreCase),
        };
    }
}
