// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;
using System.Text.RegularExpressions;

namespace BiliCopilot.UI.Toolkits;

internal enum BiliSearchLinkKind
{
    Video,
    Episode,
    Season,
    Article,
    Live,
    Short,
}

internal sealed partial record BiliSearchLink(BiliSearchLinkKind Kind, string Id, Uri Uri)
{
    private static readonly (string Prefix, BiliSearchLinkKind Kind)[] NumericPaths =
    [
        ("/video/av", BiliSearchLinkKind.Video),
        ("/bangumi/play/ep", BiliSearchLinkKind.Episode),
        ("/bangumi/play/ss", BiliSearchLinkKind.Season),
        ("/read/cv", BiliSearchLinkKind.Article),
    ];

    public static BiliSearchLink? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        if (text.Any(char.IsWhiteSpace) || text.Contains('\\'))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = text.StartsWith("//", StringComparison.Ordinal) ? "https:" + text : "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0)
        {
            return null;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length == 0)
        {
            return null;
        }

        if (uri.Host == "b23.tv" && ShortPath().IsMatch(path))
        {
            return new(BiliSearchLinkKind.Short, path[1..], uri);
        }

        if (uri.Host == "live.bilibili.com")
        {
            var id = path.StartsWith("/blanc/", StringComparison.Ordinal) ? path[7..] : path[1..];
            return IsPositiveId(id) ? new(BiliSearchLinkKind.Live, id, uri) : null;
        }

        if (uri.Host is not ("www.bilibili.com" or "m.bilibili.com" or "bilibili.com"))
        {
            return null;
        }

        if (VideoPath().Match(path) is { Success: true } video)
        {
            return new(BiliSearchLinkKind.Video, video.Groups[1].Value, uri);
        }

        foreach (var (prefix, kind) in NumericPaths)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal) && IsPositiveId(path[prefix.Length..]))
            {
                return new(kind, path[prefix.Length..], uri);
            }
        }

        return null;
    }

    private static bool IsPositiveId(string id)
        => long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;

    [GeneratedRegex(@"^/video/(BV[0-9A-Za-z]{10})$")]
    private static partial Regex VideoPath();

    [GeneratedRegex(@"^/[0-9A-Za-z]+$")]
    private static partial Regex ShortPath();
}
