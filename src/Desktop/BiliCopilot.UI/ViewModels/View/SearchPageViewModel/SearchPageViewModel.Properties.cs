// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 搜索页面视图模型.
/// </summary>
public sealed partial class SearchPageViewModel
{
    private readonly Guid _pageId = Guid.NewGuid();
    private long _searchGeneration;
    private readonly ISearchService _service;
    private readonly ILogger<SearchPageViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial string Keyword { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchError))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial string? SearchErrorMessage { get; set; }

    private bool CanRetry => HasSearchError && !IsSearching && !string.IsNullOrEmpty(Keyword);

    public bool HasSearchError => !string.IsNullOrEmpty(SearchErrorMessage);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial ISearchSectionDetailViewModel SelectedSection { get; set; }

    /// <summary>
    /// 详情分区列表.
    /// </summary>
    [ObservableProperty]
    public partial List<ISearchSectionDetailViewModel>? Sections { get; set; }

    /// <summary>
    /// 分区初始化完成.
    /// </summary>
    public event EventHandler SectionInitialized;
}
