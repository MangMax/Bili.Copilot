// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段服务.
/// <para>
/// 只向第三方接口发送 BV 号与 cid，不附带 Cookie、token 或任何账号信息。
/// 网络、超时或解析异常均静默降级为空片段列表，不影响播放.
/// </para>
/// </summary>
public sealed class SponsorBlockService : IDisposable
{
    /// <summary>
    /// 默认接口地址.
    /// </summary>
    public const string DefaultBaseUrl = "https://bsbsb.top";

    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly SponsorBlockSegmentCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _requestTimeout;
    private readonly bool _ownsHttpClient;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockService"/> class.
    /// </summary>
    /// <param name="httpClient">可选的 HTTP 客户端，为空时由服务自行创建.</param>
    /// <param name="cache">可选的片段缓存.</param>
    /// <param name="timeProvider">可选的时钟，便于测试.</param>
    /// <param name="requestTimeout">单次请求超时时间，默认五秒.</param>
    public SponsorBlockService(
        HttpClient? httpClient = null,
        SponsorBlockSegmentCache? cache = null,
        TimeProvider? timeProvider = null,
        TimeSpan? requestTimeout = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _cache = cache ?? new SponsorBlockSegmentCache();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// 获取指定视频分 P 的可跳过片段.
    /// </summary>
    /// <param name="bvid">视频 BV 号.</param>
    /// <param name="cid">当前分 P 的 cid.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>片段列表；命中缓存时直接返回缓存，任何失败均返回空列表.</returns>
    public async Task<IReadOnlyList<SponsorBlockSegment>> GetSegmentsAsync(
        string? bvid,
        string? cid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bvid) || string.IsNullOrWhiteSpace(cid))
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGet(bvid, cid, now, out var cached))
        {
            return cached;
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_requestTimeout);

            var url = $"{DefaultBaseUrl}/api/skipSegments?videoID={Uri.EscapeDataString(bvid)}&cid={Uri.EscapeDataString(cid)}";
            using var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent
                || !response.IsSuccessStatusCode)
            {
                return Array.Empty<SponsorBlockSegment>();
            }

            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            var segments = SponsorBlockSegmentParser.Parse(json, cid);

            // 只缓存有片段的结果，避免把「暂无数据」长期锁住.
            if (segments.Count > 0)
            {
                _cache.Set(bvid, cid, segments, now);
            }

            return segments;
        }
        catch
        {
            // 超时、网络错误、取消或解析异常都静默降级，绝不阻塞播放.
            return Array.Empty<SponsorBlockSegment>();
        }
    }
}
