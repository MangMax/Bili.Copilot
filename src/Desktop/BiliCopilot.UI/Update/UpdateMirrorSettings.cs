// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 内置与用户附加的加速源.
/// </summary>
/// <remarks>
/// 实测各镜像对三个端点的可用性并不一致，因此候选链按端点分别构造：
/// atom 只有 gh-proxy 能代理，资产页与下载则其余镜像可用。
/// </remarks>
internal static class UpdateMirrorSettings
{
    /// <summary>Atom Feed 可用的内置镜像.</summary>
    public static readonly string[] FeedMirrors = ["https://gh-proxy.com/"];

    /// <summary>资产页可用的内置镜像.</summary>
    public static readonly string[] AssetMirrors =
    [
        "https://fastgit.cc/",
        "https://gh.llkk.cc/",
        "https://ghfast.top/",
    ];

    /// <summary>下载安装包时用的内置镜像（实测均支持 Range）.</summary>
    public static readonly string[] DownloadMirrors =
    [
        "https://gh-proxy.com/",
        "https://gh.llkk.cc/",
        "https://fastgit.cc/",
        "https://ghfast.top/",
    ];

    /// <summary>
    /// 读取用户附加的加速源前缀.
    /// </summary>
    /// <returns>合法的 https 前缀；没有配置时返回空列表.</returns>
    public static IReadOnlyList<string> ReadUserPrefixes()
    {
        var raw = SettingsToolkit.ReadLocalSetting(SettingNames.UpdateMirrorPrefixes, string.Empty);
        return ParseUserPrefixes(raw);
    }

    /// <summary>
    /// 解析用户输入的加速源前缀，非法行被忽略.
    /// </summary>
    /// <param name="raw">每行一个前缀的文本.</param>
    /// <returns>合法的 https 前缀.</returns>
    public static IReadOnlyList<string> ParseUserPrefixes(string? raw)
        => UpdateMirrorPolicy.ParseUserPrefixes(raw);

    /// <summary>
    /// 读取上次成功使用的下载源.
    /// </summary>
    /// <returns>上次的源地址；没有记录时返回空字符串.</returns>
    public static string ReadRememberedSource()
        => SettingsToolkit.ReadLocalSetting(SettingNames.UpdateLastSource, string.Empty);

    /// <summary>
    /// 记住本次使用的下载源，并记录时间戳供过期判断.
    /// </summary>
    /// <param name="url">选中的源地址.</param>
    public static void RememberSource(string url)
    {
        SettingsToolkit.WriteLocalSetting(SettingNames.UpdateLastSource, url);
        SettingsToolkit.WriteLocalSetting(SettingNames.UpdateLastSourceTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    /// <summary>
    /// 读取仍然有效的上次下载源.
    /// </summary>
    /// <param name="maxAge">记忆的有效期.</param>
    /// <returns>未过期的上次源；过期或没有记录时返回空字符串.</returns>
    public static string ReadFreshSource(TimeSpan maxAge)
    {
        var url = ReadRememberedSource();
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        var ticks = SettingsToolkit.ReadLocalSetting(SettingNames.UpdateLastSourceTicks, 0L);
        return UpdateMirrorPolicy.IsSourceFresh(ticks, DateTimeOffset.UtcNow.UtcTicks, maxAge.Ticks)
            ? url
            : string.Empty;
    }
}
