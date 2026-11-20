// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
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
/// <param name="Error">失败原因.</param>
/// <param name="RuntimePath">ZIP 内随附的运行时包路径.</param>
public sealed record UpdateDownloadResult(string? PackagePath, string? Sha256, string? Error = null, string? RuntimePath = null);

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

    private static readonly TimeSpan NetworkIdleTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient SharedHttpClient = new();

    private readonly ILogger<UpdateDownloader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDownloader"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public UpdateDownloader(ILogger<UpdateDownloader> logger) => _logger = logger;

    /// <summary>
    /// 下载安装包到本地更新目录，校验摘要，必要时从 zip 解出 msix.
    /// </summary>
    /// <param name="asset">要下载的资产.</param>
    /// <param name="progress">进度回调，取值 0-100；无法确定总大小时为 -1.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>可直接安装的本地 msix 路径；失败时返回原因.</returns>
    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateAsset asset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            var absoluteUrl = UpdateConstants.GitHubOrigin + asset.DownloadPath;
            var mirrors = UpdateMirrorSettings.DownloadMirrors.Concat(UpdateMirrorSettings.ReadUserPrefixes());
            var mode = UpdateMirrorSettings.DownloadMode;
            var candidates = UpdateSourcePolicy.GetDownloadCandidates(absoluteUrl, mirrors, mode);
            var probes = mode == UpdateDownloadMode.GitHub
                ? Array.Empty<UpdateSourceProbe>()
                : await ProbeAsync(candidates, cancellationToken).ConfigureAwait(false);

            var remembered = UpdateMirrorSettings.ReadFreshSource(SourceMemoryTtl);
            var directory = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Updates");
            Directory.CreateDirectory(directory);
            var finalPath = Path.Combine(directory, asset.Name);
            var usableSources = probes.Where(probe => probe.Succeeded).Select(probe => probe.Url).ToHashSet(StringComparer.Ordinal);
            var sources = UpdateSourcePolicy.GetDownloadOrder(probes, remembered, mode == UpdateDownloadMode.Mirrors ? null : absoluteUrl)
                .OrderByDescending(source => usableSources.Contains(source) && File.Exists(GetPartialPath(finalPath, source)));
            string? error = null;
            foreach (var source in sources)
            {
                try
                {
                    progress?.Report(-1d);
                    var path = await DownloadWithResumeAsync(source, finalPath, progress, cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(asset.Sha256))
                    {
                        var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
                        if (!string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            TryDelete(path);
                            throw new InvalidDataException(ResourceToolkit.GetLocalizedString(StringNames.UpdateDigestMismatch));
                        }
                    }

                    var result = asset.IsZip ? ExtractPackages(path) : new UpdateDownloadResult(path, asset.Sha256);
                    UpdateMirrorSettings.RememberSource(source);
                    foreach (var partial in Directory.EnumerateFiles(directory, asset.Name + ".*.part"))
                    {
                        TryDelete(partial);
                    }

                    if (asset.IsZip)
                    {
                        TryDelete(path);
                    }

                    return result;
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    error = ResourceToolkit.GetLocalizedString(StringNames.UpdateSourceTimedOut);
                    _logger.LogWarning(ex, "更新源 {Url} 下载超时，尝试其他源", source);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error = ex.Message;
                    _logger.LogWarning(ex, "更新源 {Url} 下载失败，尝试其他源", source);
                }
            }

            return new UpdateDownloadResult(null, null, error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载更新失败");
            return new UpdateDownloadResult(null, null, ex.Message);
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
                succeeded = await ReadSampleAsync(response, timeout.Token).ConfigureAwait(false);
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

    private static async Task<bool> ReadSampleAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

        return remaining < ProbeBytes;
    }

    private async Task<string> DownloadWithResumeAsync(
        string url,
        string finalPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var partialPath = GetPartialPath(finalPath, url);
        var existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(NetworkIdleTimeout);
        using var response = await SharedHttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idle.Token)
            .ConfigureAwait(false);

        if (existing > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            response.Dispose();
            File.Delete(partialPath);
            return await DownloadWithResumeAsync(url, finalPath, progress, cancellationToken).ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();

        var range = response.Content.Headers.ContentRange;
        var append = response.StatusCode == HttpStatusCode.PartialContent;
        if (append && !UpdateSourcePolicy.IsCompleteResumeRange(existing, range))
        {
            throw new InvalidDataException(ResourceToolkit.GetLocalizedString(StringNames.UpdateInvalidDownloadRange));
        }

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
            var lastProgress = -1;
            progress?.Report(total > 0 ? Math.Clamp(existing * 100d / total, 0d, 100d) : -1d);
            while (true)
            {
                idle.CancelAfter(NetworkIdleTimeout);
                var read = await input.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                var percent = total > 0 ? (int)Math.Min(written * 100d / total, 100d) : -1;
                if (percent != lastProgress)
                {
                    progress?.Report(percent);
                    lastProgress = percent;
                }
            }

            if (written == 0 || (total > 0 && written != total))
            {
                throw new InvalidDataException(ResourceToolkit.GetLocalizedString(StringNames.UpdateIncompleteDownload));
            }
        }

        File.Move(partialPath, finalPath, overwrite: true);
        return finalPath;
    }

    private static string GetPartialPath(string finalPath, string url)
    {
        var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return $"{finalPath}.{sourceKey}.part";
    }

    private static UpdateDownloadResult ExtractPackages(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(
            e => e.Name.StartsWith("BiliCopilot.UI_", StringComparison.OrdinalIgnoreCase)
                && e.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new InvalidDataException(ResourceToolkit.GetLocalizedString(StringNames.UpdateArchiveMissingPackage));
        }

        var target = Path.ChangeExtension(zipPath, ".msix");
        entry.ExtractToFile(target, overwrite: true);

        var runtime = archive.GetEntry(UpdateConstants.RuntimePackageName + ".msix");
        if (runtime is null)
        {
            return new UpdateDownloadResult(target, null);
        }

        var runtimePath = Path.ChangeExtension(zipPath, ".runtime.msix");
        runtime.ExtractToFile(runtimePath, overwrite: true);
        return new UpdateDownloadResult(target, null, RuntimePath: runtimePath);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    internal void RemoveDownloadedFiles(UpdateDownloadResult download)
    {
        if (download.PackagePath is { } packagePath)
        {
            TryDelete(packagePath);
        }

        if (download.RuntimePath is { } runtimePath)
        {
            TryDelete(runtimePath);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "无法删除损坏的更新包：{Path}", path);
        }
    }
}
