// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Markdown;

/// <summary>Owns the bounded lifetime of a remote Markdown image, including decoding.</summary>
internal sealed class MarkdownImageLoader(HttpClient client)
{
    internal static async Task<string> ReadSvgAsync(Stream stream, string? contentType, CancellationToken token)
    {
        var charset = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType!).CharSet;
        var encoding = string.IsNullOrWhiteSpace(charset)
            ? System.Text.Encoding.UTF8
            : System.Text.Encoding.GetEncoding(charset.Trim('"'));
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(token);
    }

    internal const int MaxResponseBytes = 32 * 1024 * 1024;
    // Separate from the authenticated Bili client: arbitrary image origins get no cookies or credentials.
    public static MarkdownImageLoader Shared { get; } = new(new HttpClient(new SocketsHttpHandler
    {
        UseCookies = false,
        Credentials = null,
        MaxConnectionsPerServer = 4,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    }));

    private readonly SemaphoreSlim _slots = new(4);

    public static (int Width, int Height, double DisplayWidth, double DisplayHeight) GetDecodeSize(
        uint pixelWidth, uint pixelHeight, double width, double height, double availableWidth, double dpi)
    {
        if (pixelWidth == 0 || pixelHeight == 0)
        {
            throw new InvalidDataException("Image has no pixels.");
        }

        width = double.IsFinite(width) && width > 0 ? width : 0;
        height = double.IsFinite(height) && height > 0 ? height : 0;
        availableWidth = double.IsFinite(availableWidth) && availableWidth > 0 ? availableWidth : 800;
        dpi = double.IsFinite(dpi) && dpi > 0 ? dpi : 1;
        var displayScale = Math.Min(availableWidth / pixelWidth, width > 0 ? width / pixelWidth : double.PositiveInfinity);
        displayScale = Math.Min(displayScale, height > 0 ? height / pixelHeight : double.PositiveInfinity);
        if (width == 0 && height == 0)
        {
            displayScale = Math.Min(1, displayScale);
        }

        // At most 4 megapixels (16 MiB BGRA), with neither edge above 4096.
        var decodeScale = Math.Min(1, displayScale * dpi);
        decodeScale = Math.Min(decodeScale, 4096d / Math.Max(pixelWidth, pixelHeight));
        decodeScale = Math.Min(decodeScale, Math.Sqrt(4194304d / ((double)pixelWidth * pixelHeight)));
        return (Math.Max(1, (int)(pixelWidth * decodeScale)), Math.Max(1, (int)(pixelHeight * decodeScale)),
            pixelWidth * displayScale, pixelHeight * displayScale);
    }

    public async Task LoadAsync(Uri uri, Func<Stream, string?, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only HTTP(S) images are supported by the default loader.", nameof(uri));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        await _slots.WaitAsync(token);
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw new InvalidDataException("Markdown image exceeds 32 MiB.");
            }

            using var source = await response.Content.ReadAsStreamAsync(token);
            // A seekable stream is required by WinRT. Do not make a second full-size byte array.
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            while (true)
            {
                var read = await source.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxResponseBytes - buffer.Length + 1)), token);
                token.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw new InvalidDataException("Markdown image exceeds 32 MiB.");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), token);
            }

            token.ThrowIfCancellationRequested();
            buffer.Position = 0;
            await consume(buffer, response.Content.Headers.ContentType?.ToString(), token);
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            _slots.Release();
        }
    }
}
