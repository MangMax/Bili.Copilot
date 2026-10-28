// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Search;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 应用搜索框.
/// </summary>
public sealed partial class AppSearchBox : AppSearchBoxBase
{
    // 选择联想项后，输入法组合结束/焦点回调可能再次触发建议加载；用短时窗口兜住这类事件.
    private const int SuggestionSuppressionMilliseconds = 600;

    private readonly DispatcherQueueTimer _suggestionSuppressionTimer;
    private bool _isSubmittingQuery;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSearchBox"/> class.
    /// </summary>
    public AppSearchBox()
    {
        InitializeComponent();
        _suggestionSuppressionTimer = DispatcherQueue.CreateTimer();
        _suggestionSuppressionTimer.Interval = TimeSpan.FromMilliseconds(SuggestionSuppressionMilliseconds);
        _suggestionSuppressionTimer.IsRepeating = false;
        _suggestionSuppressionTimer.Tick += OnSuggestionSuppressionTimerTick;
    }

    protected override void OnControlUnloaded()
    {
        _suggestionSuppressionTimer.Stop();
        _isSubmittingQuery = false;
        HotSearchView.ItemsSource = null;
    }

    private void OnKeywordChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            if (_isSubmittingQuery)
            {
                // 输入法在点击联想项后提交组合文本时，不再重新拉取建议，
                // 并在当前事件之后再次关闭，避免 AutoSuggestBox 展开一个空弹层.
                ViewModel.TryCancelSuggestCommand.Execute(default);
                CloseSuggestionList();
                CloseSuggestionListDeferred();
                return;
            }

            ViewModel.ReloadSuggestionsCommand.Execute(default);
        }
    }

    private async void OnBoxGettingFocus(UIElement sender, Microsoft.UI.Xaml.Input.GettingFocusEventArgs args)
    {
        if (_isSubmittingQuery || Box.IsSuggestionListOpen)
        {
            if (_isSubmittingQuery)
            {
                CloseSuggestionList();
            }

            return;
        }

        // 保留用户已输入的文字，只按当前文字刷新建议，并在有建议时展开列表。
        try
        {
            await ViewModel.ReloadSuggestionsCommand.ExecuteAsync(default);
        }
        catch
        {
            // 网络异常不应从 async void 事件冒泡到 UI 线程；视图模型已记录日志.
            CloseSuggestionList();
            return;
        }

        if (_isSubmittingQuery)
        {
            CloseSuggestionList();
        }
        else if (Box.FocusState != FocusState.Unfocused)
        {
            Box.IsSuggestionListOpen = ViewModel.Suggestion.Count > 0;
        }
    }

    private void CloseSuggestionList()
        => Box.IsSuggestionListOpen = false;

    private void CloseSuggestionListDeferred()
    {
        // 文本变更后 AutoSuggestBox 可能在本事件之后再次展开空列表；
        // 在高、低两个优先级各关一次，覆盖它内部的展开时机.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, CloseSuggestionList);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, CloseSuggestionList);
    }

    private void OnSuggestionSuppressionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _isSubmittingQuery = false;
        CloseSuggestionList();
    }

    private void OnHotSearchFlyoutOpened(object sender, object e)
        => ViewModel.LoadHotSearchCommand.Execute(default);

    private void OnHotSearchItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        var context = args.InvokedItem as HotSearchItem;
        if (context is not null)
        {
            ViewModel.Keyword = context.Keyword;
            var vm = new SearchSuggestItemViewModel(new SearchSuggestItem(context.Keyword, context.Keyword));
            ViewModel.SearchCommand.Execute(vm);
        }

        HotSearchFlyout.Hide();
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _isSubmittingQuery = true;
        CloseSuggestionList();
        CloseSuggestionListDeferred();
        _suggestionSuppressionTimer.Stop();
        _suggestionSuppressionTimer.Start();

        if (args.ChosenSuggestion is SearchSuggestItemViewModel item)
        {
            // UpdateTextOnSelect 已关闭，选择联想项时需要手动把展示文本写回输入框.
            if (!string.IsNullOrEmpty(item.SearchContent))
            {
                ViewModel.Keyword = item.SearchContent;
            }

            ViewModel.SearchCommand.Execute(item);
        }
        else if (!string.IsNullOrEmpty(args.QueryText))
        {
            var vm = new SearchSuggestItemViewModel(new SearchSuggestItem(args.QueryText, args.QueryText));
            ViewModel.SearchCommand.Execute(vm);
        }

        ViewModel.TryCancelSuggestCommand.Execute(default);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ViewModel.Suggestion.Clear);
    }
}

/// <summary>
/// 应用搜索框的基类.
/// </summary>
public abstract class AppSearchBoxBase : LayoutUserControlBase<SearchBoxViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppSearchBoxBase"/> class.
    /// </summary>
    protected AppSearchBoxBase() => ViewModel = this.Get<SearchBoxViewModel>();
}
