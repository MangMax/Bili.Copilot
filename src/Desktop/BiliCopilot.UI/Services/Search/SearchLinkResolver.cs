// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Services.Search;

internal sealed class SearchLinkResolver : IDisposable
{
    private readonly HttpClient _client;

    public SearchLinkResolver()
        : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }))
    {
    }

    internal SearchLinkResolver(HttpClient client) => _client = client;

    public async Task<BiliSearchLink?> ResolveAsync(BiliSearchLink link, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        for (var redirects = 0; redirects < 5 && link.Kind == BiliSearchLinkKind.Short; redirects++)
        {
            var uri = new UriBuilder(link.Uri) { Scheme = "https", Port = -1 }.Uri;
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();

            if ((int)response.StatusCode is < 300 or >= 400
                || response.Headers.Location is not { } location
                || !Uri.TryCreate(uri, location, out var destination)
                || BiliSearchLink.Parse(destination.AbsoluteUri) is not { } target)
            {
                return null;
            }

            link = target;
        }

        return link.Kind == BiliSearchLinkKind.Short ? null : link;
    }

    public void Dispose() => _client.Dispose();
}
