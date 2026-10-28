// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Controls.Message;

/// <summary>
/// 聊天会话消息主体.
/// </summary>
public sealed partial class ChatSessionBody : ChatMessageControlBase
{
    private const int RetryIntervalMilliseconds = 40;
    private const int MaxScrollTicks = 75;
    private const int MaxAnchorTicks = 25;
    private const int AnchorSettleTicks = 5;
    private const double TopLoadThreshold = 80d;

    private readonly DispatcherQueueTimer _scrollTimer;
    private readonly DispatcherQueueTimer _anchorTimer;

    private int _scrollTicks;
    private int _anchorTicks;
    private double _lastScrollExtent = double.NaN;
    private double _lastScrollOffset = double.NaN;
    private double _lastAnchorExtent = double.NaN;
    private double _anchorOffset;
    private double _anchorExtent;
    private bool _isAutoScrolling;
    private bool _isRestoringAnchor;
    private bool _isLoadingOlderFromView;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSessionBody"/> class.
    /// </summary>
    public ChatSessionBody()
    {
        InitializeComponent();

        _scrollTimer = DispatcherQueue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
        _scrollTimer.IsRepeating = true;
        _scrollTimer.Tick += OnScrollTimerTick;

        _anchorTimer = DispatcherQueue.CreateTimer();
        _anchorTimer.Interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
        _anchorTimer.IsRepeating = true;
        _anchorTimer.Tick += OnAnchorTimerTick;
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        if (MessageView is not null)
        {
            MessageView.ViewChanged -= OnMessageViewChanged;
            MessageView.ViewChanged += OnMessageViewChanged;
        }

        if (ViewModel is not null)
        {
            // 控件可能经历卸载/重新加载，重新挂接滚动请求，避免事件丢失.
            ViewModel.RequestScrollToBottom -= OnRequestScrollToBottom;
            ViewModel.RequestScrollToBottom += OnRequestScrollToBottom;
            StartScrollToBottom();
        }
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (MessageView is not null)
        {
            MessageView.ViewChanged -= OnMessageViewChanged;
        }

        _scrollTimer.Stop();
        _anchorTimer.Stop();
        _isAutoScrolling = false;
        _isRestoringAnchor = false;

        if (ViewModel is not null)
        {
            ViewModel.RequestScrollToBottom -= OnRequestScrollToBottom;
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(ChatMessageSectionDetailViewModel? oldValue, ChatMessageSectionDetailViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.RequestScrollToBottom -= OnRequestScrollToBottom;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.RequestScrollToBottom += OnRequestScrollToBottom;
        if (IsLoaded)
        {
            StartScrollToBottom();
        }
    }

    private void OnRequestScrollToBottom(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, StartScrollToBottom);

    private void OnMessageViewChanged(ScrollView sender, object args)
        => _ = TryLoadOlderMessagesAsync();

    private void StartScrollToBottom()
    {
        if (MessageView is null)
        {
            return;
        }

        _anchorTimer.Stop();
        _isRestoringAnchor = false;
        _scrollTicks = 0;
        _lastScrollExtent = double.NaN;
        _lastScrollOffset = double.NaN;
        _isAutoScrolling = true;
        _scrollTimer.Stop();
        _scrollTimer.Start();
    }

    private void OnScrollTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (MessageView is null)
        {
            StopScrollToBottom();
            return;
        }

        var extent = MessageView.ExtentHeight;
        var viewport = MessageView.ViewportHeight;
        var offset = MessageView.VerticalOffset;
        var target = Math.Max(0d, extent - viewport);
        var atBottom = Math.Abs(offset - target) < 1d;
        var stable = !double.IsNaN(_lastScrollExtent)
            && Math.Abs(extent - _lastScrollExtent) < 0.5
            && Math.Abs(offset - _lastScrollOffset) < 0.5;

        if (!atBottom)
        {
            MessageView.ScrollTo(0d, target, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
        }

        _lastScrollExtent = extent;
        _lastScrollOffset = offset;
        _scrollTicks++;

        if ((atBottom && stable) || _scrollTicks >= MaxScrollTicks)
        {
            StopScrollToBottom();
        }
    }

    private void StopScrollToBottom()
    {
        _scrollTimer.Stop();
        _isAutoScrolling = false;
    }

    private async Task TryLoadOlderMessagesAsync()
    {
        if (_isAutoScrolling || _isRestoringAnchor || _isLoadingOlderFromView)
        {
            return;
        }

        if (ViewModel is null || !ViewModel.HasMoreMessages || ViewModel.IsLoading || ViewModel.IsLoadingOlder)
        {
            return;
        }

        if (MessageView.VerticalOffset > TopLoadThreshold)
        {
            return;
        }

        _isLoadingOlderFromView = true;
        try
        {
            var offset = MessageView.VerticalOffset;
            var extent = MessageView.ExtentHeight;
            await ViewModel.LoadOlderMessagesCommand.ExecuteAsync(default);
            StartAnchorRestore(offset, extent);
        }
        finally
        {
            _isLoadingOlderFromView = false;
        }
    }

    private void StartAnchorRestore(double offset, double extent)
    {
        _anchorOffset = offset;
        _anchorExtent = extent;
        _anchorTicks = 0;
        _lastAnchorExtent = double.NaN;
        _isRestoringAnchor = true;
        _anchorTimer.Stop();
        _anchorTimer.Start();
    }

    private void OnAnchorTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (MessageView is null)
        {
            StopAnchorRestore();
            return;
        }

        var extent = MessageView.ExtentHeight;
        var addedExtent = extent - _anchorExtent;
        var offset = MessageView.VerticalOffset;
        var target = Math.Max(0d, _anchorOffset + Math.Max(0d, addedExtent));
        var stable = !double.IsNaN(_lastAnchorExtent) && Math.Abs(extent - _lastAnchorExtent) < 0.5;

        if (addedExtent > 0.5 && Math.Abs(offset - target) >= 1d)
        {
            MessageView.ScrollTo(0d, target, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
        }

        _lastAnchorExtent = extent;
        _anchorTicks++;

        var settledWithNewContent = stable && addedExtent > 0.5 && Math.Abs(offset - target) < 1d;
        var settledWithoutNewContent = _anchorTicks >= AnchorSettleTicks && Math.Abs(addedExtent) <= 0.5;
        if (settledWithNewContent || settledWithoutNewContent || _anchorTicks >= MaxAnchorTicks)
        {
            StopAnchorRestore();
        }
    }

    private void StopAnchorRestore()
    {
        _anchorTimer.Stop();
        _isRestoringAnchor = false;
    }
}
