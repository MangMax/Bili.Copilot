// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Requests display-sized CDN images without changing the original gallery source.</summary>
internal static class BiliImagePreview
{
    public static Uri Create(Uri source, int width, int height, bool crop, bool modernFormat)
    {
        if (!source.IsAbsoluteUri || (source.Scheme != "http" && source.Scheme != "https")
            || !source.Host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase)
            || source.AbsolutePath.Contains('@') || source.Query.Length > 0 || source.Fragment.Length > 0)
        {
            return source;
        }

        var builder = new UriBuilder(source) { Scheme = "https", Port = -1 };
        builder.Path += $"@{width}w_{height}h{(crop ? "_1c" : string.Empty)}.{(modernFormat ? "avif" : "jpg")}";
        return builder.Uri;
    }
}
