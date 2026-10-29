// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 一次更新检查的结果状态.
/// </summary>
internal enum UpdateCheckStatus
{
    /// <summary>
    /// 已是最新版本.
    /// </summary>
    UpToDate,

    /// <summary>
    /// 发现新版本，且拿到了本架构的安装包.
    /// </summary>
    Available,

    /// <summary>
    /// 发现新版本，但发布里没有本架构的安装包.
    /// </summary>
    NoAssetForArchitecture,

    /// <summary>
    /// 检查失败（网络、解析）。
    /// </summary>
    Failed,
}

/// <summary>
/// 更新检查结果.
/// </summary>
/// <param name="Status">结果状态.</param>
/// <param name="Release">检测到的发布；失败时为 null.</param>
/// <param name="Selection">资产筛选结果.</param>
/// <param name="Error">失败原因；成功时为 null.</param>
internal sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateReleaseInfo? Release,
    UpdateAssetSelection Selection,
    string? Error);

/// <summary>
/// 应用更新服务：检测新版本并定位安装包.
/// </summary>
/// <remarks>
/// 全程使用 github.com 的 Atom Feed 与资产页，不调用 REST API，
/// 因此不受 60 次/小时的配额限制。每个端点都带镜像降级。
/// </remarks>
internal sealed class AppUpdateService
{
    private const string RepositoryUrl = "https://github.com/MangMax/Bili.Copilot";

    /// <summary>Atom Feed 可用的镜像（实测 gh-proxy 能代理 atom，其余会 403/404）.</summary>
    private static readonly string[] FeedMirrors = ["https://gh-proxy.com/"];

    /// <summary>资产页可用的镜像（实测 fastgit/llkk/ghfast 可用，gh-proxy 会 403）.</summary>
    private static readonly string[] AssetMirrors =
    [
        "https://fastgit.cc/",
        "https://gh.llkk.cc/",
        "https://ghfast.top/",
    ];

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(ILogger<AppUpdateService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 当前安装的版本号，四段式.
    /// </summary>
    public static string CurrentVersion
    {
        get
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    /// <summary>
    /// 当前进程架构对应的资产后缀.
    /// </summary>
    public static string CurrentArchitecture
        => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    /// <summary>
    /// 检查是否有新版本.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>检查结果；任何异常都被收敛为 <see cref="UpdateCheckStatus.Failed"/>.</returns>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var feedUrl = $"{RepositoryUrl}/releases.atom";
            var feed = await FetchFirstAvailableAsync(feedUrl, FeedMirrors, cancellationToken);
            var release = UpdateFeedParser.ParseLatest(feed);
            if (release is null)
            {
                _logger.LogWarning("更新检查失败：无法解析 Atom Feed");
                return Failed("无法解析更新信息");
            }

            if (!UpdateVersionPolicy.IsRemoteNewer(CurrentVersion, release.Version))
            {
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, release, default, null);
            }

            var assetsUrl = $"{RepositoryUrl}/releases/expanded_assets/{release.Tag}";
            var html = await FetchFirstAvailableAsync(assetsUrl, AssetMirrors, cancellationToken);
            var assets = UpdateAssetParser.Parse(html);
            var selection = UpdateAssetParser.Select(assets, CurrentArchitecture);
            if (selection.Kind == UpdateAssetKind.None)
            {
                _logger.LogWarning("更新 {Version} 没有适用于 {Architecture} 的安装包", release.Version, CurrentArchitecture);
                return new UpdateCheckResult(UpdateCheckStatus.NoAssetForArchitecture, release, selection, null);
            }

            return new UpdateCheckResult(UpdateCheckStatus.Available, release, selection, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新检查失败");
            return Failed(ex.Message);
        }
    }

    private async Task<string?> FetchFirstAvailableAsync(
        string url,
        IReadOnlyList<string> mirrors,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in UpdateSourcePolicy.BuildCandidates(url, mirrors))
        {
            try
            {
                using var response = await SharedHttpClient.GetAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("更新源 {Url} 返回 {Status}", candidate, (int)response.StatusCode);
                    continue;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("更新源 {Url} 超时", candidate);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogDebug(ex, "更新源 {Url} 请求失败", candidate);
            }
        }

        return null;
    }

    private static UpdateCheckResult Failed(string message)
        => new(UpdateCheckStatus.Failed, null, default, message);
}
