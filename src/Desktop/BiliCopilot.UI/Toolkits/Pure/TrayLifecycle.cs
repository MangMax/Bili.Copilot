// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 在 UI 线程协调托盘常驻；窗口、托盘和设置存储由系统边界提供。
/// </summary>
internal sealed class TrayLifecycle : IDisposable
{
    private readonly Action<bool> _writeSetting;
    private readonly Func<IDisposable> _createTray;
    private readonly Action _hideWindow;
    private readonly Action _restoreWindow;
    private readonly Action<Exception> _reportFailure;
    private IDisposable? _tray;
    private bool _enabled;
    private bool _disposed;

    public TrayLifecycle(
        Func<bool> readSetting,
        Action<bool> writeSetting,
        Func<IDisposable> createTray,
        Action hideWindow,
        Action restoreWindow,
        Action<Exception> reportFailure)
    {
        _writeSetting = writeSetting;
        _createTray = createTray;
        _hideWindow = hideWindow;
        _restoreWindow = restoreWindow;
        _reportFailure = reportFailure;
        _enabled = readSetting();
        if (_enabled)
        {
            EnsureTray();
        }
    }

    /// <summary>
    /// 是否由用户关闭主窗口而进入托盘，区别于播放引起的隐藏。
    /// </summary>
    public bool IsResident { get; private set; }

    /// <summary>
    /// 保存选择并立即启停托盘；禁用时先恢复主动驻留的窗口。
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (_disposed)
        {
            return;
        }

        _writeSetting(enabled);
        _enabled = enabled;
        if (enabled)
        {
            EnsureTray();
        }
        else
        {
            if (IsResident)
            {
                Restore();
            }

            ReleaseTray();
        }
    }

    /// <summary>
    /// 托盘点击、菜单和第二实例激活共用的恢复入口。
    /// </summary>
    public void Restore()
    {
        if (_disposed)
        {
            return;
        }

        IsResident = false;
        _restoreWindow();
    }

    /// <summary>
    /// 处理主窗口关闭。返回 true 时调用方须取消原生关闭；托盘失败也取消关闭，保留窗口。
    /// 返回 false 时继续原有非托盘关闭流程。
    /// </summary>
    public bool HandleClose()
    {
        if (_disposed || !_enabled)
        {
            return false;
        }

        if (!EnsureTray())
        {
            Restore();
            return true;
        }

        IsResident = true;
        _hideWindow();
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        IsResident = false;
        ReleaseTray();
    }

    private bool EnsureTray()
    {
        try
        {
            _tray ??= _createTray();
            return true;
        }
        catch (Exception ex)
        {
            _reportFailure(ex);
            return false;
        }
    }

    private void ReleaseTray()
    {
        _tray?.Dispose();
        _tray = null;
    }
}
