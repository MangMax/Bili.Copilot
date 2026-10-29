// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 一次加速源探测的结果.
/// </summary>
/// <param name="Url">被探测的地址.</param>
/// <param name="Succeeded">是否可用（HTTP 200/206 且拿到数据）.</param>
/// <param name="ElapsedMs">耗时，单位毫秒.</param>
/// <param name="SupportsRange">是否支持 Range 分段.</param>
/// <param name="TotalBytes">探测到的文件总大小；未知时为 0.</param>
public sealed record UpdateSourceProbe(
    string Url,
    bool Succeeded,
    long ElapsedMs,
    bool SupportsRange,
    long TotalBytes);

/// <summary>
/// 加速源候选链与选择策略.
/// </summary>
/// <remarks>
/// <para>
/// 不同端点（检测页 / 资产页 / 下载）可用的镜像并不一致，因此候选链由调用方按端点给出前缀集合。
/// </para>
/// <para>
/// 选择策略：上次成功且本次仍可用者优先（保证续传不换源），否则选实测最快；
/// 全部失败时返回 null，由调用方回退直连——加速失败绝不阻断。
/// </para>
/// </remarks>
public static class UpdateSourcePolicy
{
    /// <summary>
    /// 构造候选地址链：直连在前，镜像按给定顺序依次拼接.
    /// </summary>
    /// <param name="originalUrl">GitHub 原始地址.</param>
    /// <param name="mirrorPrefixes">镜像前缀，gh-proxy 风格.</param>
    /// <returns>去重后的候选地址；原地址非法时返回空列表.</returns>
    public static IReadOnlyList<string> BuildCandidates(string? originalUrl, IEnumerable<string?>? mirrorPrefixes)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return [];
        }

        var candidates = new List<string> { originalUrl };
        if (mirrorPrefixes is not null)
        {
            foreach (var prefix in mirrorPrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    continue;
                }

                var normalized = prefix.Trim();
                if (!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!normalized.EndsWith('/'))
                {
                    normalized += "/";
                }

                var candidate = normalized + originalUrl;
                if (!candidates.Contains(candidate, StringComparer.Ordinal))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// 在探测结果中选出本次要用的源.
    /// </summary>
    /// <param name="probes">并行探测结果.</param>
    /// <param name="rememberedUrl">上次成功使用的地址，可为 null.</param>
    /// <returns>选中的地址；全部失败时返回 null.</returns>
    public static string? Select(IReadOnlyList<UpdateSourceProbe>? probes, string? rememberedUrl)
    {
        if (probes is null || probes.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(rememberedUrl))
        {
            foreach (var probe in probes)
            {
                if (probe.Succeeded && string.Equals(probe.Url, rememberedUrl, StringComparison.Ordinal))
                {
                    return probe.Url;
                }
            }
        }

        UpdateSourceProbe? fastest = null;
        foreach (var probe in probes)
        {
            if (!probe.Succeeded)
            {
                continue;
            }

            if (fastest is null || probe.ElapsedMs < fastest.ElapsedMs)
            {
                fastest = probe;
            }
        }

        return fastest?.Url;
    }
}
