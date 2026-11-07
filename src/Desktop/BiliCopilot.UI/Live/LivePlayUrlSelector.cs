// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 一条可交给 mpv 的直播播放地址。
/// </summary>
/// <param name="Url">绝对播放地址.</param>
/// <param name="CodecName">解码名（如 avc / hevc）.</param>
/// <param name="Quality">当前清晰度标识.</param>
/// <param name="Protocol">协议名（http_hls / http_stream）.</param>
/// <param name="IsMcdn">是否来自 mcdn 节点.</param>
public sealed record LivePlaybackTarget(string Url, string? CodecName, int Quality, string Protocol, bool IsMcdn);

/// <summary>
/// 直播播放地址选择：把内核给出的线路拼成绝对地址，并做基础候选排序。
/// </summary>
/// <remarks>
/// 当前只实现 #193 需要的"能播"排序：HLS 优先于 FLV、avc 优先于其他编码、mcdn 节点最后、
/// 重复地址只保留第一条。完整的协议/容器/编码排序与失败换线属于 #194，直接扩展
/// <see cref="Order"/> 的排序键即可。
/// </remarks>
public static class LivePlayUrlSelector
{
    /// <summary>
    /// 选择一条最可能可播的直播地址。
    /// </summary>
    /// <param name="lines">内核返回的直播线路.</param>
    /// <returns>播放目标；没有可用地址时为 <c>null</c>.</returns>
    public static LivePlaybackTarget? Select(IEnumerable<LiveLineInformation>? lines)
        => Order(lines).FirstOrDefault();

    /// <summary>
    /// 按基础优先级排序列出全部候选；重复地址只保留第一次出现。
    /// </summary>
    /// <param name="lines">内核返回的直播线路.</param>
    /// <returns>去重并排序后的候选.</returns>
    public static IReadOnlyList<LivePlaybackTarget> Order(IEnumerable<LiveLineInformation>? lines)
    {
        if (lines is null)
        {
            return [];
        }

        var candidates = new List<(int Index, LivePlaybackTarget Target, LiveLineInformation Line)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var line in lines)
        {
            if (line?.Urls is null)
            {
                continue;
            }

            foreach (var url in line.Urls)
            {
                var composed = Compose(url);
                if (string.IsNullOrEmpty(composed) || !seen.Add(composed))
                {
                    index++;
                    continue;
                }

                var isMcdn = (url.Host ?? string.Empty).Contains("mcdn", StringComparison.OrdinalIgnoreCase);
                candidates.Add((index, new LivePlaybackTarget(composed, line.Name, line.Quality, url.Protocol ?? string.Empty, isMcdn), line));
                index++;
            }
        }

        return candidates
            .OrderBy(p => p.Target.IsMcdn)
            .ThenBy(p => ProtocolRank(p.Target.Protocol))
            .ThenBy(p => CodecRank(p.Target.CodecName))
            .ThenBy(p => p.Index)
            .Select(p => p.Target)
            .ToList();
    }

    private static string? Compose(LivePlayUrl? url)
    {
        if (url is null)
        {
            return default;
        }

        var host = url.Host ?? string.Empty;
        var route = url.Route ?? string.Empty;
        var query = url.Query ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) && string.IsNullOrWhiteSpace(route))
        {
            return default;
        }

        var baseUrl = host.StartsWith("//", StringComparison.Ordinal)
            ? "https:" + host
            : host.Contains("://", StringComparison.Ordinal) ? host : "https://" + host.TrimStart('/');
        baseUrl = baseUrl.TrimEnd('/');
        if (!string.IsNullOrEmpty(route) && !route.StartsWith('/'))
        {
            route = "/" + route;
        }

        if (!string.IsNullOrEmpty(query) && !query.StartsWith('?') && !query.StartsWith('&'))
        {
            query = "?" + query;
        }

        return baseUrl + route + query;
    }

    private static int ProtocolRank(string? protocol)
        => protocol switch
        {
            "http_hls" => 0,
            "http_stream" => 1,
            _ => 2,
        };

    private static int CodecRank(string? codec)
        => codec?.ToLowerInvariant() switch
        {
            "avc" => 0,
            "hevc" => 1,
            _ => 2,
        };
}
