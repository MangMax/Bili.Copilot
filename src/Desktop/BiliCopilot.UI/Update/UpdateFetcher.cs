// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 带镜像降级的文本抓取.
/// </summary>
/// <remarks>
/// 镜像只当搬运工：内容正确性最终由 MSIX 签名与官方 sha256 保证，
/// 因此这里只负责「换源重试」，不做内容校验。
/// </remarks>
public sealed class UpdateFetcher
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly ILogger<UpdateFetcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFetcher"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public UpdateFetcher(ILogger<UpdateFetcher> logger) => _logger = logger;

    /// <summary>
    /// 依次尝试候选地址，返回第一个成功响应的正文.
    /// </summary>
    /// <param name="url">原始地址.</param>
    /// <param name="mirrors">镜像前缀.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>正文；全部失败时返回 null.</returns>
    public async Task<string?> FetchFirstAvailableAsync(
        string url,
        IEnumerable<string> mirrors,
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
}
