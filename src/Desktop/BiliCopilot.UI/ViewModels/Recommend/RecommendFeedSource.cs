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
    /// 最近一次取数是否发生了降级，非 null 表示可以提示给用户.
    /// </summary>
    string? Notice { get; }

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
    public string? Notice => null;

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
    public string? Notice => null;

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
    public string? Notice { get; private set; }

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

        Notice = mobilePage is null
            ? "手机端推荐不可用，当前仅使用网页端推荐"
            : webPage is null
                ? "网页端推荐不可用，当前仅使用手机端推荐"
                : null;
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
