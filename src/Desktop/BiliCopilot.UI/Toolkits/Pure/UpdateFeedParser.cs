// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Releases Atom Feed 的解析结果.
/// </summary>
/// <param name="Tag">原始 tag，例如 v2.2610.3.0.</param>
/// <param name="Version">归一后的版本号，例如 2.2610.3.0.</param>
/// <param name="Notes">发布说明的纯文本.</param>
/// <param name="PublishedAt">发布时间；无法解析时为 null.</param>
public sealed record UpdateReleaseInfo(string Tag, string Version, string Notes, DateTimeOffset? PublishedAt);

/// <summary>
/// GitHub Releases Atom Feed 解析.
/// </summary>
/// <remarks>
/// 使用 Atom Feed 而不是 REST API，是因为 Feed 没有 60 次/小时的配额限制，
/// 并且同样包含 pre-release。
/// </remarks>
public static partial class UpdateFeedParser
{
    private const string TagMarker = "/releases/tag/";

    /// <summary>
    /// 从 Atom Feed 中取最新一条发布.
    /// </summary>
    /// <param name="xml">Feed 原文.</param>
    /// <returns>最新发布；无法解析或没有条目时返回 null.</returns>
    public static UpdateReleaseInfo? ParseLatest(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var entry = document.Root?.Element(ns + "entry");
        if (entry is null)
        {
            return null;
        }

        var tag = ExtractTag(entry, ns);
        var version = UpdateVersionPolicy.Normalize(tag);
        if (version.Length == 0)
        {
            return null;
        }

        var content = entry.Element(ns + "content")?.Value;
        var updated = entry.Element(ns + "updated")?.Value;
        return new UpdateReleaseInfo(tag ?? string.Empty, version, ToPlainText(content), ParseTimestamp(updated));
    }

    private static string? ExtractTag(XElement entry, XNamespace ns)
    {
        foreach (var link in entry.Elements(ns + "link"))
        {
            var href = link.Attribute("href")?.Value;
            if (href is null)
            {
                continue;
            }

            var index = href.IndexOf(TagMarker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return Uri.UnescapeDataString(href[(index + TagMarker.Length)..].TrimEnd('/'));
            }
        }

        return null;
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// 把 release 正文的 HTML 转成便于展示的纯文本.
    /// </summary>
    /// <param name="html">HTML 片段.</param>
    /// <returns>纯文本；输入为空时返回空字符串.</returns>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = BreakTagRegex().Replace(html, "\n");
        text = ListItemRegex().Replace(text, "\n- ");
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = HorizontalWhitespaceRegex().Replace(text, " ");
        text = BlankLineRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"</?(p|div|br|h[1-6]|ul|ol|blockquote|tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLineRegex();
}
