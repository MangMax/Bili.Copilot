// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 直播聊天侧栏：有界消息列表、滚动所有权（自动跟随 / 回到底部）与屏蔽入口。
/// </summary>
/// <remarks>
/// 滚动所有权沿用 <c>ChatSessionBody</c> 的既有范式：自动跟随由 40ms 稳定定时器推进，
/// 用户向上滚动时立即停止跟随并显示"回到底部"，新消息不会把用户拉回底部。
/// 折叠按钮与屏蔽输入都是普通控件，键盘可达；折叠只影响本侧栏，不触碰全屏/画中画。
/// </remarks>
public sealed partial class LiveChatSidebar : LiveChatControlBase
{
    private const int RetryIntervalMilliseconds = 40;
    private const string InteractionTag = "interaction";
    private const string BlockTag = "block";
    private const int MaxScrollTicks = 75;

    private readonly DispatcherQueueTimer _scrollTimer;
    private readonly LiveChatScrollOwnership _ownership = new();

    private int _scrollTicks;
    private double _lastScrollExtent = double.NaN;
    private double _lastScrollOffset = double.NaN;
    private int _lastCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatSidebar"/> class.
    /// </summary>
    public LiveChatSidebar()
    {
        InitializeComponent();

        _scrollTimer = DispatcherQueue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
        _scrollTimer.IsRepeating = true;
        _scrollTimer.Tick += OnScrollTimerTick;
        SectionTabs.SelectionChanged += OnSectionChanged;
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        InitializeSections();
        if (ChatView is not null)
        {
            ChatView.ViewChanged -= OnChatViewChanged;
            ChatView.ViewChanged += OnChatViewChanged;
        }

        if (ViewModel is not null)
        {
            ViewModel.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
            ViewModel.ChatMessages.CollectionChanged += OnChatMessagesChanged;
            ViewModel.StartChat();
            _lastCount = ViewModel.ChatMessages.Count;
            StartScrollToBottom();
        }
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ChatView is not null)
        {
            ChatView.ViewChanged -= OnChatViewChanged;
        }

        if (ViewModel is not null)
        {
            ViewModel.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
            ViewModel.StopChat();
        }

        _scrollTimer.Stop();
        _ownership.Reset();
        _lastCount = 0;
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(LiveConnectorViewModel? oldValue, LiveConnectorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
            oldValue.StopChat();
        }

        if (newValue is null)
        {
            return;
        }

        newValue.ChatMessages.CollectionChanged += OnChatMessagesChanged;
        if (IsLoaded)
        {
            newValue.StartChat();
            StartScrollToBottom();
        }
    }

    private void InitializeSections()
    {
        var items = new[]
        {
            new SelectorBarItem
            {
                Text = ResourceToolkit.GetLocalizedString(StringNames.LiveChatInteractionTitle),
                Tag = InteractionTag,
            },
            new SelectorBarItem
            {
                Text = ResourceToolkit.GetLocalizedString(StringNames.LiveChatBlockTitle),
                Tag = BlockTag,
            },
        };
        SectionTabs.SetItems(items, InteractionTag);
    }

    private void OnSectionChanged(object? sender, EventArgs args)
    {
        var showBlock = string.Equals(SectionTabs.SelectedItem as string, BlockTag, StringComparison.Ordinal);
        InteractionPanel.Visibility = showBlock ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        BlockPanel.Visibility = showBlock ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (!showBlock && _ownership.IsFollowing)
        {
            StartScrollToBottom();
        }
    }

    private void OnChatMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var count = ViewModel?.ChatMessages.Count ?? 0;
        if (count == _lastCount)
        {
            return;
        }

        var isAppend = e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
            && count == _lastCount + 1;
        _lastCount = count;

        // 只有"追加一条且仍在跟随"才自动滚到底；整表重置与用户回看都不抢滚动位置。
        if (isAppend && _ownership.ReportAppend())
        {
            StartScrollToBottom();
        }
    }

    private void OnChatViewChanged(ScrollView sender, object args)
    {
        if (ChatView is null)
        {
            return;
        }

        if (_scrollTimer.IsRunning)
        {
            // 自动跟随自己产生的位置变化不算用户回看。
            _ownership.ReportProgrammaticScroll(ChatView.VerticalOffset, ChatView.ExtentHeight, ChatView.ViewportHeight);
            return;
        }

        _ownership.ReportUserScroll(ChatView.VerticalOffset, ChatView.ExtentHeight, ChatView.ViewportHeight);
        UpdateBackToBottomVisibility(_ownership.ShouldShowBackToBottom);
    }

    private void OnBackToBottomClick(object sender, RoutedEventArgs e)
    {
        StartScrollToBottom();
        UpdateBackToBottomVisibility(false);
    }

    private void OnApplyBlockRulesClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SetChatBlockRules(
            ParseIds(BlockUserIdBox.Text),
            Split(BlockUserNameBox.Text),
            Split(BlockKeywordBox.Text));
    }

    private static IReadOnlyList<long> ParseIds(string? text)
    {
        var result = new List<long>();
        foreach (var part in Split(text))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> Split(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split([',', '，', ';', '；', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void StartScrollToBottom()
    {
        if (ChatView is null)
        {
            return;
        }

        _scrollTicks = 0;
        _lastScrollExtent = double.NaN;
        _lastScrollOffset = double.NaN;
        _ownership.RequestBackToBottom();
        _scrollTimer.Stop();
        _scrollTimer.Start();
    }

    private void OnScrollTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (ChatView is null)
        {
            StopScrollToBottom();
            return;
        }

        var extent = ChatView.ExtentHeight;
        var viewport = ChatView.ViewportHeight;
        var offset = ChatView.VerticalOffset;
        var target = Math.Max(0d, extent - viewport);
        var atBottom = Math.Abs(offset - target) < 1d;
        var stable = !double.IsNaN(_lastScrollExtent)
            && Math.Abs(extent - _lastScrollExtent) < 0.5
            && Math.Abs(offset - _lastScrollOffset) < 0.5;

        if (!atBottom)
        {
            ChatView.ScrollTo(0d, target, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
        }

        _lastScrollExtent = extent;
        _lastScrollOffset = offset;
        _scrollTicks++;

        if ((atBottom && stable) || _scrollTicks >= MaxScrollTicks)
        {
            StopScrollToBottom();
            UpdateBackToBottomVisibility(false);
        }
    }

    private void StopScrollToBottom() => _scrollTimer.Stop();

    private void UpdateBackToBottomVisibility(bool visible)
    {
        if (BackToBottomButton is null)
        {
            return;
        }

        BackToBottomButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}