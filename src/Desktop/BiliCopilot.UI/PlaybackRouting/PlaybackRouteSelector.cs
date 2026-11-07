// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.PlaybackRouting;

internal static class PlaybackRouteSelector
{
    public static string? Select(string? baseUrl, IEnumerable<string>? backupUrls,
        PlaybackRoutePreference preference = PlaybackRoutePreference.Auto, bool withoutP2P = false)
    {
        var candidates = new[] { baseUrl }.Concat(backupUrls ?? [])
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.Ordinal)
            .Select(url => new Candidate(url!, new Uri(url!).Host.TrimEnd('.'), url == baseUrl))
            .ToList();
        var lowerRisk = candidates.Where(candidate => !IsRiskyHost(candidate.Host)).ToList();
        if (withoutP2P && lowerRisk.Count > 0)
        {
            candidates = lowerRisk;
        }

        var preferred = candidates.Where(candidate => preference switch
        {
            PlaybackRoutePreference.Primary => candidate.IsPrimary,
            PlaybackRoutePreference.Backup => !candidate.IsPrimary,
            _ => MatchesProvider(candidate.Host, preference),
        }).ToList();
        return (preferred.Count > 0 ? preferred : candidates)
            .OrderBy(candidate => IsRiskyHost(candidate.Host))
            .FirstOrDefault()?.Url;
    }

    private sealed record Candidate(string Url, string Host, bool IsPrimary);

    private static bool IsRiskyHost(string host)
        => Uri.CheckHostName(host) is UriHostNameType.IPv4 or UriHostNameType.IPv6
            || IsDomain(host, "szbdyd.com")
            || IsDomain(host, "mountaintoys.cn")
            || IsDomain(host, "qrstuvwxyzab.com")
            || IsDomain(host, "zhenidc.cn")
            || IsDomain(host, "mcdn.bilivideo.cn")
            || IsDomain(host, "mcdn.bilivideo.com")
            || IsDomain(host, "pcdn.bilivideo.cn")
            || IsDomain(host, "pcdn.bilivideo.com")
            || IsDomain(host, "gotcha.bilivideo.cn")
            || IsDomain(host, "gotcha.bilivideo.com");

    // Known service hosts from BiliPai 20017b1. Exact matches deliberately avoid
    // assigning providers to arbitrary lookalike hosts. No URL rewriting.
    private static bool MatchesProvider(string host, PlaybackRoutePreference preference)
    {
        host = host.ToLowerInvariant();
        return preference switch
        {
            PlaybackRoutePreference.Ali => host is "upos-sz-mirrorali.bilivideo.com"
                or "upos-sz-mirroralib.bilivideo.com" or "upos-sz-mirroralio1.bilivideo.com"
                or "upos-sz-mirroraliov.bilivideo.com",
            PlaybackRoutePreference.Tencent => host is "upos-sz-mirrorcos.bilivideo.com"
                or "upos-sz-mirrorcosb.bilivideo.com" or "upos-sz-mirrorcoso1.bilivideo.com"
                or "upos-tf-all-tx.bilivideo.com" or "upos-sz-mirrorcosov.bilivideo.com",
            PlaybackRoutePreference.Huawei => host is "upos-sz-mirrorhw.bilivideo.com"
                or "upos-sz-mirrorhwb.bilivideo.com" or "upos-sz-mirrorhwo1.bilivideo.com"
                or "upos-sz-mirror08c.bilivideo.com" or "upos-sz-mirror08h.bilivideo.com"
                or "upos-sz-mirror08ct.bilivideo.com" or "upos-tf-all-hw.bilivideo.com"
                or "upos-sz-mirrorhwov.bilivideo.com",
            PlaybackRoutePreference.Overseas => host is "upos-hz-mirrorakam.akamaized.net"
                or "upos-sz-mirroraliov.bilivideo.com" or "upos-sz-mirrorcosov.bilivideo.com"
                or "upos-sz-mirrorhwov.bilivideo.com" or "cn-hk-eq-bcache-01.bilivideo.com",
            _ => false,
        };
    }

    private static bool IsDomain(string host, string domain)
        => host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}

// Stored numeric values are stable; append new choices rather than reordering.
internal enum PlaybackRoutePreference
{
    Auto = 0,
    Primary = 1,
    Backup = 2,
    Ali = 3,
    Tencent = 4,
    Huawei = 5,
    Overseas = 6,
}
