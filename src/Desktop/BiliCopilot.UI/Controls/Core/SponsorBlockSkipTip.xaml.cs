// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 空降助手的跳过提示. 显示时不抢占键盘焦点，也不拦截播放器操作.
/// </summary>
public sealed partial class SponsorBlockSkipTip : PlayerControlBase
{
    private readonly DispatcherQueueTimer _timer;
    private DateTimeOffset? _visibleTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockSkipTip"/> class.
    /// </summary>
    public SponsorBlockSkipTip()
    {
        InitializeComponent();
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// 显示提示并开始计时.
    /// </summary>
    public void Show()
    {
        ProgressRect.Value = 0;
        RootPanel.Visibility = Visibility.Visible;
        _visibleTime = DateTimeOffset.Now;
        _timer.Start();
    }

    /// <summary>
    /// 隐藏提示.
    /// </summary>
    public void Hide()
    {
        ProgressRect.Value = 0;
        RootPanel.Visibility = Visibility.Collapsed;
        _visibleTime = null;
        _timer.Stop();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_visibleTime.HasValue)
        {
            return;
        }

        var elapsed = (DateTimeOffset.Now - _visibleTime.Value).TotalSeconds;
        if (elapsed >= SponsorBlockController.PromptTimeout.TotalSeconds)
        {
            // 交由视图模型标记该片段已处理，避免重新出现.
            ViewModel.DismissSponsorBlockPromptCommand.Execute(default);
            Hide();
            return;
        }

        ProgressRect.Value = elapsed;
    }
}
