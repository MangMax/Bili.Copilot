// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.View;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 搜索页面.
/// </summary>
public sealed partial class SearchPage : SearchPageBase, IParameterPage
{
    private object? _parameter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchPage"/> class.
    /// </summary>
    public SearchPage() => InitializeComponent();

    public void SetParameter(object? parameter)
    {
        if (parameter is not string && parameter is not SearchSubmission)
        {
            return;
        }

        _parameter = parameter;

        // OverlayFrame 会复用已存在的 SearchPage 实例，此时不会再次触发 OnPageLoaded，
        // 因此已加载的页面需要在收到新参数时立即执行搜索.
        if (IsLoaded)
        {
            ExecuteSearch();
        }
    }

    /// <inheritdoc/>
    protected override void OnPageLoaded()
        => ExecuteSearch();

    protected override void OnPageUnloaded()
    {
        _parameter = null;
        ViewModel.DeactivateCommand.Execute(default);
    }

    private void ExecuteSearch()
    {
        if (_parameter is SearchSubmission || _parameter is string { Length: > 0 })
        {
            ViewModel.SearchCommand.Execute(_parameter);
        }
    }
}

/// <summary>
/// 搜索页面基类.
/// </summary>
public abstract class SearchPageBase : LayoutPageBase<SearchPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchPageBase"/> class.
    /// </summary>
    protected SearchPageBase() => ViewModel = this.Get<SearchPageViewModel>();
}
