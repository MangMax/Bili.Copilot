// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播流容器种类；枚举顺序即优先级（fmp4 &gt; ts &gt; flv &gt; 未知）。
/// </summary>
public enum LiveContainerKind
{
    /// <summary>
    /// fMP4（HLS 的分片 MP4 变体，或直接的 mp4/m4s 分片）.
    /// </summary>
    Fmp4,

    /// <summary>
    /// MPEG-TS（HLS 的默认切片格式，或直接的 .ts 地址）.
    /// </summary>
    Ts,

    /// <summary>
    /// HTTP-FLV.
    /// </summary>
    Flv,

    /// <summary>
    /// 无法从地址判断.
    /// </summary>
    Unknown,
}

/// <summary>
/// 一条可交给 mpv 的直播播放地址。
/// </summary>
/// <param name="Url">绝对播放地址.</param>
/// <param name="CodecName">解码名（如 avc / hevc）.</param>
/// <param name="Quality">当前清晰度标识.</param>
/// <param name="Protocol">协议名（http_hls / http_stream）.</param>
/// <param name="IsMcdn">是否来自 mcdn 节点.</param>
/// <param name="Container">由地址推导出的容器.</param>
public sealed record LivePlaybackTarget(
    string Url,
    string? CodecName,
    int Quality,
    string Protocol,
    bool IsMcdn,
    LiveContainerKind Container = LiveContainerKind.Unknown)
{
    /// <summary>
    /// Gets 地址的域名；用于"换一条线路"时在刷新后重新定位同一条线路。
    /// </summary>
    public string? Host { get; } = TryGetHost(Url);

    private static string? TryGetHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : default;
}

/// <summary>
/// 一个直播候选：一条内核线路（同一编码/清晰度/协议）及其按原顺序排列的多条地址。
/// </summary>
/// <param name="CodecName">解码名.</param>
/// <param name="Quality">清晰度标识.</param>
/// <param name="Protocol">协议名.</param>
/// <param name="Container">容器.</param>
/// <param name="IsMcdn">该候选是否包含 mcdn 地址.</param>
/// <param name="Targets">同一候选内按原顺序尝试的地址.</param>
public sealed record LivePlaybackCandidate(
    string? CodecName,
    int Quality,
    string Protocol,
    LiveContainerKind Container,
    bool IsMcdn,
    IReadOnlyList<LivePlaybackTarget> Targets)
{
    /// <summary>
    /// Gets 用于在刷新后恢复线路偏好的稳定标识（首选地址域名）.
    /// </summary>
    public string Key => $"{CodecName ?? "unknown"}|{Targets[0].Host ?? Protocol}";

    /// <summary>
    /// Gets 候选内第一条地址.
    /// </summary>
    public LivePlaybackTarget Primary => Targets[0];
}

/// <summary>
/// 直播播放地址选择：把内核给出的线路拼成绝对地址，并按固定优先级排序。
/// </summary>
/// <remarks>
/// <para>排序规则（#194）：http_hls 优先于 http_stream；容器 fmp4 优先于 ts 优先于 flv；
/// avc 优先于其他编码；含 mcdn 的候选最后；同一候选内多条地址保持内核给出的顺序；
/// 完全相同的地址只保留第一次出现。</para>
/// <para>容器推导：优先使用内核 <see cref="LiveLineInformation.FormatName"/>（接口的
/// <c>format_name</c>：ts/fmp4/flv），它比地址更可靠；为空时回退到从拼好的地址推导：
/// 地址中出现 <c>fmp4</c> 标记、或以 <c>.m4s</c>/<c>.mp4</c> 结尾 → fmp4；
/// 以 <c>.ts</c> 结尾 → ts；以 <c>.flv</c> 结尾 → flv；
/// 以 <c>.m3u8</c> 结尾且没有 fmp4 标记 → 按 B 站 HLS 默认切片视为 ts；
/// 其余为未知（排在已知容器之后）。</para>
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
    /// 按候选优先级列出全部地址；同一候选内保持内核给出的地址顺序。
    /// </summary>
    /// <param name="lines">内核返回的直播线路.</param>
    /// <returns>去重并排序后的地址.</returns>
    public static IReadOnlyList<LivePlaybackTarget> Order(IEnumerable<LiveLineInformation>? lines)
        => OrderCandidates(lines).SelectMany(candidate => candidate.Targets).ToList();

    /// <summary>
    /// 按候选优先级列出去重后的候选；这是"换一条线路"与"候选耗尽后重新取址"的排序入口。
    /// </summary>
    /// <param name="lines">内核返回的直播线路.</param>
    /// <returns>去重并排序后的候选.</returns>
    public static IReadOnlyList<LivePlaybackCandidate> OrderCandidates(IEnumerable<LiveLineInformation>? lines)
    {
        if (lines is null)
        {
            return [];
        }

        var candidates = new List<(int Index, LivePlaybackCandidate Candidate)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var line in lines)
        {
            if (line?.Urls is null)
            {
                continue;
            }

            var targets = new List<LivePlaybackTarget>();
            foreach (var url in line.Urls)
            {
                var composed = Compose(url);
                if (string.IsNullOrEmpty(composed) || !seen.Add(composed))
                {
                    continue;
                }

                targets.Add(new LivePlaybackTarget(
                    composed,
                    line.Name,
                    line.Quality,
                    url.Protocol ?? string.Empty,
                    IsMcdnHost(url.Host),
                    DetectContainer(composed, line.FormatName)));
            }

            if (targets.Count == 0)
            {
                index++;
                continue;
            }

            var first = targets[0];
            candidates.Add((index++, new LivePlaybackCandidate(
                line.Name,
                line.Quality,
                first.Protocol,
                first.Container,
                targets.Exists(target => target.IsMcdn),
                targets)));
        }

        return candidates
            .OrderBy(p => p.Candidate.IsMcdn)
            .ThenBy(p => ProtocolRank(p.Candidate.Protocol))
            .ThenBy(p => (int)p.Candidate.Container)
            .ThenBy(p => CodecRank(p.Candidate.CodecName))
            .ThenBy(p => p.Index)
            .Select(p => p.Candidate)
            .ToList();
    }

    /// <summary>
    /// 判定容器：优先使用内核返回的 <c>format_name</c>，没有时回退到地址推导。
    /// </summary>
    /// <param name="url">绝对播放地址.</param>
    /// <param name="formatName">内核返回的容器格式名（如 <c>ts</c>/<c>fmp4</c>）.</param>
    /// <returns>容器种类.</returns>
    public static LiveContainerKind DetectContainer(string? url, string? formatName = null)
    {
        var declared = ParseFormatName(formatName);
        return declared != LiveContainerKind.Unknown ? declared : DetectContainerFromUrl(url);
    }

    /// <summary>
    /// 从绝对地址推导容器；推导规则见类型说明。
    /// </summary>
    /// <param name="url">绝对播放地址.</param>
    /// <returns>容器种类.</returns>
    public static LiveContainerKind DetectContainerFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return LiveContainerKind.Unknown;
        }

        if (url.Contains("fmp4", StringComparison.OrdinalIgnoreCase))
        {
            return LiveContainerKind.Fmp4;
        }

        var path = url.Split('?', '#')[0];
        if (path.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return LiveContainerKind.Fmp4;
        }

        if (path.EndsWith(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return LiveContainerKind.Flv;
        }

        if (path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            return LiveContainerKind.Ts;
        }

        if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            // 内核丢弃 format_name，只能按 B 站 HLS 的默认切片格式（ts）处理无标记的 m3u8。
            return LiveContainerKind.Ts;
        }

        return LiveContainerKind.Unknown;
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

        // HLS 的 route 自带结尾 '?'，extra 又可能以 '?' 开头；直接拼接会得到 "??expires"，
        // 部分 CDN（腾讯系 smtcdns）会按错误签名返回 403.
        query = query.TrimStart('?');
        if (!string.IsNullOrEmpty(query))
        {
            query = route.EndsWith('?') ? query : "?" + query;
        }

        return baseUrl + route + query;
    }

    private static LiveContainerKind ParseFormatName(string? formatName)
        => formatName?.Trim().ToLowerInvariant() switch
        {
            "fmp4" or "m4s" or "mp4" => LiveContainerKind.Fmp4,
            "ts" => LiveContainerKind.Ts,
            "flv" => LiveContainerKind.Flv,
            _ => LiveContainerKind.Unknown,
        };

    private static bool IsMcdnHost(string? host)
        => !string.IsNullOrEmpty(host) && host.Contains("mcdn", StringComparison.OrdinalIgnoreCase);

    private static int ProtocolRank(string? protocol)
        => protocol?.ToLowerInvariant() switch
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