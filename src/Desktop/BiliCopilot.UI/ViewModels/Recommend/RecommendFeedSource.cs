// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Recommend;

/// <summary>
/// 推荐流来源.
/// </summary>
/// <remarks>
/// 页面只关心「给我下一批」，游标推进、跨页拼接与失败降级由来源自己负责；
/// 这样换来源不需要改页面，合并来源也能自己维护多路游标.
/// </remarks>
public interface IRecommendFeedSource
{
    /// <summary>
    /// 来源种类.
    /// </summary>
    RecommendFeedSource Kind { get; }

    /// <summary>
    /// 取一批推荐.
    /// </summary>
    /// <param name="offset">游标，首次传 0.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>本批视频与下一批游标.</returns>
    Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> FetchAsync(long offset, CancellationToken cancellationToken = default);
}

/// <summary>
/// 手机端推荐流.
/// </summary>
/// <param name="service">视频发现服务.</param>
public sealed class MobileRecommendFeedSource(IVideoDiscoveryService service) : IRecommendFeedSource
{
    /// <inheritdoc/>
    public RecommendFeedSource Kind => RecommendFeedSource.Mobile;

    /// <inheritdoc/>
    public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> FetchAsync(long offset, CancellationToken cancellationToken = default)
        // 直接返回服务任务：手机端不做任何加工，加载路径上不多出一层异步续体。
        => service.GetRecommendVideoListAsync(offset, cancellationToken: cancellationToken);
}

/// <summary>
/// 网页端推荐流.
/// </summary>
public sealed class WebRecommendFeedSource(IVideoDiscoveryService service, int pageSize = 20) : IRecommendFeedSource
{
    /// <inheritdoc/>
    public RecommendFeedSource Kind => RecommendFeedSource.Web;

    /// <inheritdoc/>
    public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> FetchAsync(long offset, CancellationToken cancellationToken = default)
        // 网页端按刷新批次推进：offset 就是 fresh_idx。
        => service.GetCuratedFeedAsync(offset, pageSize, cancellationToken);
}

/// <summary>
/// 合并推荐流：同时取手机端与网页端，交错去重.
/// </summary>
/// <remarks>
/// 两路游标各自推进：外部传入的游标只代表手机端那一路，网页端的批次由本实例自己保存，
/// 因此页面只需要维护一个游标，合并模式仍然可以正常翻页.
/// </remarks>
public sealed class MergedRecommendFeedSource(IRecommendFeedSource mobile, IRecommendFeedSource web) : IRecommendFeedSource
{
    private long _webOffset;

    /// <inheritdoc/>
    public RecommendFeedSource Kind => RecommendFeedSource.Merged;

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> FetchAsync(long offset, CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(
            TryFetchAsync(mobile, offset, cancellationToken),
            TryFetchAsync(web, _webOffset, cancellationToken)).ConfigureAwait(false);
        var mobilePage = results[0];
        var webPage = results[1];
        if (mobilePage is null && webPage is null)
        {
            throw new InvalidOperationException("手机端与网页端推荐都不可用");
        }

        if (webPage is { } page)
        {
            _webOffset = page.Offset;
        }

        var merged = RecommendFeedMerger.Merge(
            mobilePage?.Videos,
            webPage?.Videos,
            p => p.Identifier.Id);
        if (merged.Count == 0)
        {
            throw new InvalidOperationException("合并推荐为空");
        }

        // 单路失败时静默用另一路继续，不需要额外提示。
        return (merged, mobilePage?.Offset ?? offset);
    }

    private static async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)?> TryFetchAsync(
        IRecommendFeedSource source,
        long offset,
        CancellationToken cancellationToken)
    {
        try
        {
            var (videos, next) = await source.FetchAsync(offset, cancellationToken).ConfigureAwait(false);
            return (videos, next);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// 按设置创建推荐流来源.
/// </summary>
public static class RecommendFeedSourceFactory
{
    /// <summary>
    /// 创建来源.
    /// </summary>
    /// <param name="service">视频发现服务.</param>
    /// <param name="kind">来源种类.</param>
    /// <returns>推荐流来源.</returns>
    public static IRecommendFeedSource Create(IVideoDiscoveryService service, RecommendFeedSource kind)
        => kind switch
        {
            RecommendFeedSource.Web => new WebRecommendFeedSource(service),
            RecommendFeedSource.Merged => new MergedRecommendFeedSource(
                new MobileRecommendFeedSource(service),
                new WebRecommendFeedSource(service)),
            _ => new MobileRecommendFeedSource(service),
        };
}
/// <summary>
/// 推荐流来源设置的读写.
/// </summary>
/// <remarks>
/// 早期版本把枚举直接写进了应用设置，而设置存储只支持基础类型，
/// 现在统一按下标（int）读写；读取时兼容旧值，避免升级后整个推荐流因类型不匹配而加载失败.
/// </remarks>
public static class RecommendFeedSourceSetting
{
    /// <summary>
    /// 读取推荐流来源.
    /// </summary>
    /// <returns>当前设置的来源，无法识别时回退到手机端.</returns>
    public static RecommendFeedSource Read()
    {
        try
        {
            var index = SettingsToolkit.ReadLocalSetting(SettingNames.RecommendFeedSource, (int)RecommendFeedSource.Mobile);
            return Enum.IsDefined(typeof(RecommendFeedSource), index)
                ? (RecommendFeedSource)index
                : RecommendFeedSource.Mobile;
        }
        catch (InvalidCastException)
        {
            // 旧版本存的是枚举本身；读出来后顺手迁移成下标。
            try
            {
                var legacy = SettingsToolkit.ReadLocalSetting(SettingNames.RecommendFeedSource, RecommendFeedSource.Mobile);
                Write(legacy);
                return legacy;
            }
            catch (InvalidCastException)
            {
                return RecommendFeedSource.Mobile;
            }
        }
    }

    /// <summary>
    /// 写入推荐流来源.
    /// </summary>
    /// <param name="source">要保存的来源.</param>
    public static void Write(RecommendFeedSource source)
        => SettingsToolkit.WriteLocalSetting(SettingNames.RecommendFeedSource, (int)source);
}