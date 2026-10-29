// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Windows.Storage;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 下载完成的结果.
/// </summary>
/// <param name="PackagePath">可直接安装的 msix 路径.</param>
/// <param name="Sha256">与 <paramref name="PackagePath"/> 对应的期望摘要；没有时为 null.</param>
public sealed record UpdateDownloadResult(string PackagePath, string? Sha256);

/// <summary>
/// 下载更新安装包.
/// </summary>
/// <remarks>
/// 借鉴 fluxdown 的 github-accel 插件：下载前对本次文件在所有候选源上并行做小样本探测，
/// 一次探测同时得到可用性与速度；优先沿用上次的源（保证续传不换源），否则选最快。
/// 全部失败时回退直连——加速失败绝不阻断。
/// </remarks>
public sealed class UpdateDownloader
{
    private const int ProbeBytes = 64 * 1024;
    private const int BufferSize = 81920;

    /// <summary>记住的源的有效期.</summary>
    private static readonly TimeSpan SourceMemoryTtl = TimeSpan.FromHours(12);

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDownloader"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public UpdateDownloader(ILogger logger) => _logger = logger;

    /// <summary>
    /// 下载安装包到本地更新目录，校验摘要，必要时从 zip 解出 msix.
    /// </summary>
    /// <param name="asset">要下载的资产.</param>
    /// <param name="progress">进度回调，取值 0-100；无法确定总大小时为 null.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>可直接安装的本地 msix 路径；失败时返回 null.</returns>
    public async Task<UpdateDownloadResult?> DownloadAsync(
        UpdateAsset asset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            var absoluteUrl = UpdateConstants.RepositoryUrl + asset.DownloadPath;
            var mirrors = UpdateMirrorSettings.DownloadMirrors.Concat(UpdateMirrorSettings.ReadUserPrefixes());
            var candidates = UpdateSourcePolicy.BuildCandidates(absoluteUrl, mirrors);
            var probes = await ProbeAsync(candidates, cancellationToken).ConfigureAwait(false);

            var remembered = UpdateMirrorSettings.ReadFreshSource(SourceMemoryTtl);
            var source = UpdateSourcePolicy.Select(probes, remembered) ?? absoluteUrl;
            UpdateMirrorSettings.RememberSource(source);

            // 只有沿用上次的源时才续传；换源后内容可能不一致，必须从头下载.
            var allowResume = !string.IsNullOrEmpty(remembered)
                && string.Equals(source, remembered, StringComparison.Ordinal);
            var path = await DownloadWithResumeAsync(source, asset, progress, allowResume, cancellationToken)
                .ConfigureAwait(false);
            if (path is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(asset.Sha256))
            {
                var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("更新包摘要不匹配，已丢弃：期望 {Expected}，实际 {Actual}", asset.Sha256, actual);
                    TryDelete(path);
                    return null;
                }
            }

            // 发布里没有裸 msix 时只能下 zip，这里解出其中的安装包.
            var packagePath = asset.IsZip ? ExtractMsix(path) : path;
            return packagePath is null
                ? null
                : new UpdateDownloadResult(packagePath, asset.IsMsix ? asset.Sha256 : null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载更新失败");
            return null;
        }
    }

    private async Task<IReadOnlyList<UpdateSourceProbe>> ProbeAsync(
        IReadOnlyList<string> candidates,
        CancellationToken cancellationToken)
    {
        var tasks = candidates.Select(url => ProbeOneAsync(url, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<UpdateSourceProbe> ProbeOneAsync(string url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, ProbeBytes - 1);
            using var response = await SharedHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            var succeeded = response.IsSuccessStatusCode;
            var supportsRange = response.StatusCode == HttpStatusCode.PartialContent;
            var total = supportsRange
                ? response.Content.Headers.ContentRange?.Length ?? 0
                : response.Content.Headers.ContentLength ?? 0;

            // 真正读完采样区间，耗时才是吞吐而不是首字节时间.
            if (succeeded)
            {
                await ReadSampleAsync(response, timeout.Token).ConfigureAwait(false);
            }

            return new UpdateSourceProbe(url, succeeded, stopwatch.ElapsedMilliseconds, supportsRange, total);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new UpdateSourceProbe(url, false, stopwatch.ElapsedMilliseconds, false, 0);
        }
    }

    private static async Task ReadSampleAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        var remaining = ProbeBytes;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            remaining -= read;
        }
    }

    private async Task<string?> DownloadWithResumeAsync(
        string url,
        UpdateAsset asset,
        IProgress<double>? progress,
        bool allowResume,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Updates");
        Directory.CreateDirectory(directory);

        var finalPath = Path.Combine(directory, asset.Name);
        var partialPath = finalPath + ".part";
        if (!allowResume && File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        var existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var response = await SharedHttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("下载源 {Url} 返回 {Status}", url, (int)response.StatusCode);
            return null;
        }

        // 只有服务端确实续传时才追加，否则从头写.
        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append)
        {
            existing = 0;
        }

        var total = response.Content.Headers.ContentRange?.Length
            ?? response.Content.Headers.ContentLength
            ?? 0;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (var output = new FileStream(
            partialPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            useAsync: true))
        {
            var buffer = new byte[BufferSize];
            var written = existing;
            var progressUnknown = false;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                if (total > 0)
                {
                    progress?.Report(Math.Clamp(written * 100d / total, 0d, 100d));
                }
                else if (written > 0 && !progressUnknown)
                {
                    // 总大小未知：先报一次 -1，让界面切成不确定环.
                    progressUnknown = true;
                    progress?.Report(-1d);
                }
            }
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(partialPath, finalPath);
        progress?.Report(100d);
        return finalPath;
    }

    private string? ExtractMsix(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.FirstOrDefault(
                e => e.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                _logger.LogError("更新压缩包里没有 msix：{Path}", zipPath);
                return null;
            }

            var target = Path.Combine(Path.GetDirectoryName(zipPath)!, entry.Name);
            entry.ExtractToFile(target, overwrite: true);
            return target;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解压更新包失败：{Path}", zipPath);
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "无法删除损坏的更新包：{Path}", path);
        }
    }
}
