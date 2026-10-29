// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Windows.Management.Deployment;
using Windows.Storage;

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

    /// <summary>下载安装包时用的内置镜像（实测均支持 Range）.</summary>
    private static readonly string[] DownloadMirrors =
    [
        "https://gh-proxy.com/",
        "https://gh.llkk.cc/",
        "https://fastgit.cc/",
        "https://ghfast.top/",
    ];

    /// <summary>探测时的采样字节数.</summary>
    private const int ProbeBytes = 64 * 1024;

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

    /// <summary>
    /// 下载安装包到本地更新目录，并校验摘要.
    /// </summary>
    /// <param name="asset">要下载的资产.</param>
    /// <param name="progress">进度回调，取值 0-100.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>下载完成的本地路径；失败时返回 null.</returns>
    public async Task<string?> DownloadAsync(
        UpdateAsset asset,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);

        try
        {
            var absoluteUrl = "https://github.com" + asset.DownloadPath;
            var candidates = UpdateSourcePolicy.BuildCandidates(absoluteUrl, DownloadMirrors.Concat(ParseUserMirrors()));
            var probes = await ProbeAsync(candidates, cancellationToken).ConfigureAwait(false);
            var remembered = SettingsToolkit.ReadLocalSetting(SettingNames.UpdateLastSource, string.Empty);
            var source = UpdateSourcePolicy.Select(probes, remembered) ?? absoluteUrl;
            SettingsToolkit.WriteLocalSetting(SettingNames.UpdateLastSource, source);

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
            return asset.IsZip ? ExtractMsix(path) : path;
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

    /// <summary>
    /// 安装已下载的更新包.
    /// </summary>
    /// <param name="packagePath">本地 msix 路径.</param>
    /// <param name="sha256">期望的 sha256，可为 null.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>安装是否成功.</returns>
    public async Task<bool> InstallAsync(string packagePath, string? sha256, CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            _logger.LogError("更新包不存在：{Path}", packagePath);
            return false;
        }

        try
        {
            // Visor 是包内的独立进程，不退出会一直占用包文件，导致注册被推迟.
            if (System.Diagnostics.Process.GetProcessesByName("BiliCopilot.Visor").Length > 0)
            {
                _ = await VisorClient.ShutdownAsync().ConfigureAwait(false);
            }

            var uri = new Uri(packagePath);
            var options = new AddPackageOptions
            {
                // 应用自身正在运行：延迟注册，等进程退出后再完成，避免安装中途被杀死.
                DeferRegistrationWhenPackagesAreInUse = true,
            };

            if (!string.IsNullOrWhiteSpace(sha256))
            {
                options.ExpectedDigests.Add(uri, sha256.ToUpperInvariant());
            }

            var manager = new PackageManager();
            var result = await manager.AddPackageByUriAsync(uri, options)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (result.ExtendedErrorCode is not null)
            {
                _logger.LogError(
                    "安装更新失败：0x{HResult:X8} {Message}",
                    result.ExtendedErrorCode.HResult,
                    result.ErrorText);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装更新失败");
            return false;
        }
    }

    private IEnumerable<string> ParseUserMirrors()
    {
        var raw = SettingsToolkit.ReadLocalSetting(SettingNames.UpdateMirrorPrefixes, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, ProbeBytes - 1);
            using var response = await SharedHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            var succeeded = response.IsSuccessStatusCode;
            var supportsRange = response.StatusCode == HttpStatusCode.PartialContent;
            var total = supportsRange
                ? response.Content.Headers.ContentRange?.Length ?? 0
                : response.Content.Headers.ContentLength ?? 0;

            if (succeeded)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                var buffer = new byte[8192];
                _ = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
            }

            return new UpdateSourceProbe(url, succeeded, stopwatch.ElapsedMilliseconds, supportsRange, total);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateSourceProbe(url, false, stopwatch.ElapsedMilliseconds, false, 0);
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

        long existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
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
            bufferSize: 81920,
            useAsync: true))
        {
            var buffer = new byte[81920];
            var written = existing;
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

    private static UpdateCheckResult Failed(string message)
        => new(UpdateCheckStatus.Failed, null, default, message);
}
