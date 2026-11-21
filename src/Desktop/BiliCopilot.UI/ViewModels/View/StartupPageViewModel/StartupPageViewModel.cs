// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Richasy.BiliKernel.Bili;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 启动页视图模型.
/// </summary>
public sealed partial class StartupPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupPageViewModel"/> class.
    /// </summary>
    public StartupPageViewModel(
        ILogger<StartupPageViewModel> logger,
        DispatcherQueue dispatcherQueue)
    {
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
    }

    internal StartupWindow? SignInWindow { get; set; }

    /// <summary>
    /// 初始化视图模型.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task InitializeAsync(Image qrcodeImageControl)
    {
        Version = this.Get<IAppToolkit>().GetPackageVersion();
        QRCodeImage = qrcodeImageControl;
        await ReloadQRCodeAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ReloadQRCodeAsync()
    {
        var previous = _cancellationTokenSource;
        using var attempt = new CancellationTokenSource();
        _cancellationTokenSource = attempt;
        ErrorTip = null;
        IsQRCodeLoading = true;
        if (QRCodeImage is not null)
        {
            QRCodeImage.Source = null;
        }
        try
        {
            previous?.Cancel();
            var settings = new AuthorizeExecutionSettings
            {
                RenderQRCodeAsync = image => RenderQRCodeAsync(image, attempt.Token),
            };
            await GlobalDependencies.Accounts.SignInAsync(settings, SignInWindow?.ExpectedUid, attempt.Token,
                activateOnSuccess: SignInWindow?.ExpectedUid.HasValue == true);
            if (ReferenceEquals(_cancellationTokenSource, attempt))
            {
                await ExitAsync(true);
            }
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_cancellationTokenSource, attempt))
            {
                ErrorTip = ex.Message;
                _logger.LogWarning("二维码登录失败 ({ExceptionType})", ex.GetType().Name);
            }
        }
        finally
        {
            if (ReferenceEquals(_cancellationTokenSource, attempt))
            {
                _cancellationTokenSource = null;
                IsQRCodeLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task ExitAsync(bool completeSignIn)
    {
        var attempt = _cancellationTokenSource;
        _cancellationTokenSource = null;
        IsQRCodeLoading = false;
        if (attempt is not null)
        {
            await attempt.CancelAsync();
        }

        if (completeSignIn)
        {
            SignInWindow?.Close();
        }
    }

    internal Task RenderQRCodeAsync(byte[] imageData)
        => _cancellationTokenSource is { } attempt
            ? RenderQRCodeAsync(imageData, attempt.Token)
            : Task.CompletedTask;

    private Task RenderQRCodeAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new MemoryStream(imageData);
                using var source = stream.AsRandomAccessStream();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(source).AsTask(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (QRCodeImage is { } image)
                {
                    image.Source = bitmap;
                    IsQRCodeLoading = false;
                }

                completion.SetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("登录窗口已关闭"));
        }

        return completion.Task;
    }
}
