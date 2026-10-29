// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInstaller"/> class.
    /// </summary>
    /// <param name="logger">日志.</param>
    public UpdateInstaller(ILogger logger) => _logger = logger;

    /// <summary>
    /// 安装已下载的更新包.
    /// </summary>
    /// <param name="packagePath">本地 msix 路径.</param>
    /// <param name="sha256">期望的 sha256，可为 null.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>安装是否已成功登记.</returns>
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
            if (Process.GetProcessesByName("BiliCopilot.Visor").Length > 0)
            {
                _ = await VisorClient.ShutdownAsync().ConfigureAwait(false);
            }

            var uri = new Uri(packagePath);
            var options = new AddPackageOptions
            {
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
}
