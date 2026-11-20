// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Update;

/// <summary>
/// 更新功能共用的常量.
/// </summary>
internal static class UpdateConstants
{
    /// <summary>
    /// 本仓库地址.
    /// </summary>
    public const string GitHubOrigin = "https://github.com";

    public const string RepositoryPath = "/MangMax/Bili.Copilot";

    public const string RepositoryUrl = GitHubOrigin + RepositoryPath;

    public const string RuntimePackageName = "Microsoft.WindowsAppRuntime.2";

    public const string ReleasePackageName = "Mang.BiliCopilot";

    /// <summary>
    /// Releases Atom Feed 地址.
    /// </summary>
    public const string FeedUrl = RepositoryUrl + "/releases.atom";
}
