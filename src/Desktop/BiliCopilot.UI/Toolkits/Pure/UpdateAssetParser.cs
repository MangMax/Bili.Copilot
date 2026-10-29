// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Text.RegularExpressions;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Release 资产页上的一个文件.
/// </summary>
/// <param name="Name">文件名.</param>
/// <param name="DownloadPath">相对下载路径，以 / 开头.</param>
/// <param name="Sha256">页面给出的 sha256；没有时为 null.</param>
internal sealed record UpdateAsset(string Name, string DownloadPath, string? Sha256)
{
    /// <summary>
    /// 是否为 MSIX 安装包.
    /// </summary>
    public bool IsMsix => Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 是否为 zip 压缩包.
    /// </summary>
    public bool IsZip => Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 资产类型.
/// </summary>
internal enum UpdateAssetKind
{
    /// <summary>
    /// 没有可用资产.
    /// </summary>
    None,

    /// <summary>
    /// 裸 MSIX 安装包.
    /// </summary>
    Msix,

    /// <summary>
    /// 含安装包的 zip.
    /// </summary>
    Zip,
}

/// <summary>
/// 资产筛选结果.
/// </summary>
/// <param name="Kind">选中的类型.</param>
/// <param name="Asset">选中的资产；<see cref="UpdateAssetKind.None"/> 时为 null.</param>
internal sealed record UpdateAssetSelection(UpdateAssetKind Kind, UpdateAsset? Asset);

/// <summary>
/// Releases 资产页解析与资产筛选.
/// </summary>
/// <remarks>
/// 资产页是 github.com 的 HTML 片段端点，不消耗 REST API 配额，
/// 且同时给出文件名与 sha256。
/// </remarks>
internal static partial class UpdateAssetParser
{
    /// <summary>
    /// 解析资产页 HTML.
    /// </summary>
    /// <param name="html">资产页原文.</param>
    /// <returns>页面上的文件列表；无法解析时返回空列表.</returns>
    public static IReadOnlyList<UpdateAsset> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var assets = new List<UpdateAsset>();
        foreach (Match row in ListItemRegex().Matches(html))
        {
            var href = HrefRegex().Match(row.Value);
            if (!href.Success)
            {
                continue;
            }

            var path = WebUtility.HtmlDecode(href.Groups["path"].Value);
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (name.Length == 0)
            {
                continue;
            }

            var digest = Sha256Regex().Match(row.Value);
            assets.Add(new UpdateAsset(
                WebUtility.HtmlDecode(name),
                path,
                digest.Success ? digest.Groups["digest"].Value : null));
        }

        return assets;
    }

    /// <summary>
    /// 按当前架构挑选安装包，优先裸 MSIX，其次 zip.
    /// </summary>
    /// <param name="assets">资产列表.</param>
    /// <param name="architecture">当前架构，x64 或 arm64.</param>
    /// <returns>筛选结果；没有匹配架构的包时返回 <see cref="UpdateAssetKind.None"/>.</returns>
    public static UpdateAssetSelection Select(IReadOnlyList<UpdateAsset>? assets, string? architecture)
    {
        if (assets is null || assets.Count == 0 || string.IsNullOrWhiteSpace(architecture))
        {
            return new UpdateAssetSelection(UpdateAssetKind.None, null);
        }

        var suffix = "_" + architecture.Trim();
        UpdateAsset? Find(Func<UpdateAsset, bool> predicate)
            => assets.FirstOrDefault(asset =>
                predicate(asset)
                && asset.Name.Contains(suffix, StringComparison.OrdinalIgnoreCase));

        var msix = Find(asset => asset.IsMsix);
        if (msix is not null)
        {
            return new UpdateAssetSelection(UpdateAssetKind.Msix, msix);
        }

        var zip = Find(asset => asset.IsZip);
        return zip is not null
            ? new UpdateAssetSelection(UpdateAssetKind.Zip, zip)
            : new UpdateAssetSelection(UpdateAssetKind.None, null);
    }

    [GeneratedRegex(@"<li\b[^>]*>(?<row>.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"href=""(?<path>/[^""]*/releases/download/[^""]*\.(?:msix|zip))""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"sha256:(?<digest>[0-9a-fA-F]{64})")]
    private static partial Regex Sha256Regex();
}
