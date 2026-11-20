// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using Windows.Management.Deployment;

namespace BiliCopilot.UI.Update;

/// <summary>
/// 用 MSIX 部署 API 原地安装更新.
/// </summary>
/// <remarks>
/// 应用自身正在运行，因此采用延迟注册：部署栈在包不再被占用时才完成注册，
/// 调用方随后重启应用，即可在新进程里跑新版本。
/// </remarks>
public sealed class UpdateInstaller
{
    private const int DependencyResolutionError = unchecked((int)0x80073CF3);
    private const string RuntimePublisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
    private static readonly XNamespace Appx = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private readonly ILogger<UpdateInstaller> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInstaller"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public UpdateInstaller(ILogger<UpdateInstaller> logger) => _logger = logger;

    /// <summary>
    /// 安装已下载的更新包.
    /// </summary>
    /// <param name="download">已下载的应用和可选运行时包.</param>
    /// <param name="expectedVersion">用户确认的目标版本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>安装是否已成功登记.</returns>
    public async Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download, string expectedVersion, CancellationToken cancellationToken)
    {
        var packagePath = download.PackagePath!;
        if (!File.Exists(packagePath))
        {
            _logger.LogError("更新包不存在：{Path}", packagePath);
            return new UpdateInstallResult(false, ResourceToolkit.GetLocalizedString(StringNames.UpdatePackageMissing), RequiresNewDownload: true);
        }

        try
        {
            using var package = File.OpenRead(packagePath);
            if (!string.IsNullOrWhiteSpace(download.Sha256))
            {
                var hash = await SHA256.HashDataAsync(package, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Convert.ToHexString(hash), download.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdateInstallResult(
                        false,
                        ResourceToolkit.GetLocalizedString(StringNames.UpdateDigestMismatch),
                        RequiresNewDownload: true);
                }

                package.Position = 0;
            }

            var manifest = ReadManifest(package);
            if (!MatchesInstalledPackage(manifest, expectedVersion))
            {
                return new UpdateInstallResult(
                    false,
                    ResourceToolkit.GetLocalizedString(StringNames.UpdatePackageIdentityMismatch),
                    RequiresNewDownload: true);
            }

            // Visor 是包内的独立进程，不退出会一直占用包文件，导致注册被推迟.
            var visors = Process.GetProcessesByName("BiliCopilot.Visor");
            try
            {
                var installedPath = Path.TrimEndingDirectorySeparator(Package.Current.InstalledPath) + Path.DirectorySeparatorChar;
                var installedVisors = visors.Where(visor => IsFromPackage(visor, installedPath)).ToArray();
                if (installedVisors.Length > 0)
                {
                    _ = await VisorClient.ShutdownAsync().ConfigureAwait(false);
                    using var exitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    exitTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        await Task.WhenAll(installedVisors.Select(visor => visor.WaitForExitAsync(exitTimeout.Token))).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        return new UpdateInstallResult(false, ResourceToolkit.GetLocalizedString(StringNames.UpdateVisorBusy));
                    }
                }
            }
            finally
            {
                foreach (var visor in visors)
                {
                    visor.Dispose();
                }
            }

            var uri = new Uri(packagePath);
            var options = new AddPackageOptions
            {
                DeferRegistrationWhenPackagesAreInUse = true,
            };

            if (download.RuntimePath is { } runtimePath && IsRequiredRuntime(manifest, runtimePath))
            {
                options.DependencyPackageUris.Add(new Uri(runtimePath));
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
                return DeploymentFailed(result.ExtendedErrorCode.HResult, result.ErrorText);
            }

            return new UpdateInstallResult(true, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            _logger.LogWarning(ex, "无法读取更新包的清单");
            return new UpdateInstallResult(
                false,
                ResourceToolkit.GetLocalizedString(StringNames.UpdatePackageIdentityMismatch),
                RequiresNewDownload: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装更新失败");
            return DeploymentFailed(ex.HResult, ex.Message);
        }
    }

    private static bool IsFromPackage(Process process, string installedPath)
    {
        try
        {
            return process.MainModule?.FileName.StartsWith(installedPath, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static UpdateInstallResult DeploymentFailed(int code, string? message)
        => new(false, $"0x{code:X8}: {message}", DependencyResolutionFailed: code == DependencyResolutionError);

    private static XElement? ReadManifest(Stream package)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = archive.GetEntry("AppxManifest.xml");
        if (manifest is null || manifest.Length > 1024 * 1024)
        {
            return null;
        }

        using var manifestStream = manifest.Open();
        return XDocument.Load(manifestStream).Root;
    }

    private static bool MatchesInstalledPackage(XElement? manifest, string expectedVersion)
    {
        var identity = manifest?.Element(Appx + "Identity");
        var installed = Package.Current.Id;
        return (string?)identity?.Attribute("Name") == installed.Name
            && (string?)identity?.Attribute("Publisher") == installed.Publisher
            && (string?)identity?.Attribute("Version") == expectedVersion
            && (string?)identity?.Attribute("ProcessorArchitecture") == AppUpdateService.CurrentArchitecture;
    }

    private static bool IsRequiredRuntime(XElement? manifest, string runtimePath)
    {
        var dependency = manifest?.Element(Appx + "Dependencies")?.Elements(Appx + "PackageDependency")
            .FirstOrDefault(element => (string?)element.Attribute("Name") == UpdateConstants.RuntimePackageName
                && (string?)element.Attribute("Publisher") == RuntimePublisher);
        if (!Version.TryParse((string?)dependency?.Attribute("MinVersion"), out var minimum))
        {
            return false;
        }

        if (Package.Current.Dependencies.Any(package => package.Id.Name == UpdateConstants.RuntimePackageName
            && package.Id.Publisher == RuntimePublisher
            && IsRuntimeVersionAtLeast(package.Id.Version, minimum)))
        {
            return false;
        }

        using var runtime = File.OpenRead(runtimePath);
        var identity = ReadManifest(runtime)?.Element(Appx + "Identity");
        return (string?)identity?.Attribute("Name") == UpdateConstants.RuntimePackageName
            && (string?)identity?.Attribute("Publisher") == RuntimePublisher
            && (string?)identity?.Attribute("ProcessorArchitecture") == AppUpdateService.CurrentArchitecture
            && Version.TryParse((string?)identity?.Attribute("Version"), out var provided)
            && provided >= minimum;
    }

    private static bool IsRuntimeVersionAtLeast(PackageVersion version, Version minimum)
        => new Version(version.Major, version.Minor, version.Build, version.Revision) >= minimum;
}

/// <summary>
/// 安装操作结果.
/// </summary>
/// <param name="Succeeded">是否成功.</param>
/// <param name="Error">失败原因.</param>
/// <param name="RequiresNewDownload">是否应重新下载安装包.</param>
/// <param name="DependencyResolutionFailed">是否未能解析 MSIX 依赖.</param>
public sealed record UpdateInstallResult(
    bool Succeeded,
    string? Error,
    bool RequiresNewDownload = false,
    bool DependencyResolutionFailed = false);
