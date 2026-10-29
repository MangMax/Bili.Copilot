// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 一次更新检查的结果状态.
/// </summary>
public enum UpdateCheckStatus
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
    /// 检查失败（网络、解析）.
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
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateReleaseInfo? Release,
    UpdateAssetSelection Selection,
    string? Error);

/// <summary>
/// 应用更新服务：检测新版本并定位安装包.
/// </summary>
/// <remarks>
/// 检测全程使用 github.com 的 Atom Feed 与资产页，不调用 REST API，
/// 因此不受 60 次/小时的配额限制。下载与安装委托给专用组件。
/// </remarks>
public sealed class AppUpdateService
{
    private readonly ILogger<AppUpdateService> _logger;
    private readonly UpdateFetcher _fetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateService"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    /// <param name="fetcher">带镜像降级的抓取器.</param>
    public AppUpdateService(ILogger<AppUpdateService> logger, UpdateFetcher fetcher)
    {
        _logger = logger;
        _fetcher = fetcher;
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
            var userMirrors = UpdateMirrorSettings.ReadUserPrefixes();
            var feed = await _fetcher
                .FetchFirstAvailableAsync(
                    UpdateConstants.FeedUrl,
                    UpdateMirrorSettings.FeedMirrors.Concat(userMirrors),
                    cancellationToken)
                .ConfigureAwait(false);

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

            var assetsUrl = $"{UpdateConstants.RepositoryUrl}/releases/expanded_assets/{release.Tag}";
            var html = await _fetcher
                .FetchFirstAvailableAsync(
                    assetsUrl,
                    UpdateMirrorSettings.AssetMirrors.Concat(userMirrors),
                    cancellationToken)
                .ConfigureAwait(false);

            var assets = UpdateAssetParser.Parse(html);
            var selection = UpdateAssetParser.Select(assets, CurrentArchitecture);
            if (selection.Kind == UpdateAssetKind.None)
            {
                _logger.LogWarning(
                    "更新 {Version} 没有适用于 {Architecture} 的安装包",
                    release.Version,
                    CurrentArchitecture);
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

    private static UpdateCheckResult Failed(string message)
        => new(UpdateCheckStatus.Failed, null, default, message);
}
