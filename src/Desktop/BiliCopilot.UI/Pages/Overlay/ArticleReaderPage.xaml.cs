// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.View;
using Richasy.BiliKernel.Models.Article;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 文章阅读页面.
/// </summary>
public sealed partial class ArticleReaderPage : ArticleReaderPageBase, IParameterPage
{
    private int _loadGeneration = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderPage"/> class.
    /// </summary>
    public ArticleReaderPage() => InitializeComponent();

    public void SetParameter(object? parameter)
    {
        if (parameter is ArticleIdentifier article)
        {
            // 叠层按类型缓存页面，同一页面可能再次收到新的文章参数。
            // 先解除旧订阅再挂载，保证一次加载只对应一份处理器；先订阅再执行，
            // 避免内容同步完成时 ArticleInitialized 在订阅之前触发而漏掉注入。
            Reader.Initialized -= OnInitializedAsync;
            ViewModel.ArticleInitialized -= OnInitializedAsync;
            Reader.Initialized += OnInitializedAsync;
            ViewModel.ArticleInitialized += OnInitializedAsync;
            ViewModel.InitializeCommand.Execute(article);

            // 记录本次页面发起的加载代次，卸载时只取消这一代，不影响新页面.
            _loadGeneration = ViewModel.CurrentLoadGeneration;
        }
    }

    protected override void OnPageUnloaded()
    {
        ViewModel.CancelLoad(_loadGeneration);
        Reader.Initialized -= OnInitializedAsync;
        ViewModel.ArticleInitialized -= OnInitializedAsync;
        Reader.ClearContent();
    }

    private async void OnInitializedAsync(object? sender, EventArgs e)
    {
        if (ViewModel.Content is not null && Reader.IsInitialized)
        {
            await Reader.LoadContentAsync(ViewModel.Content);
        }
    }
}

/// <summary>
/// 文章阅读页面基类.
/// </summary>
public abstract class ArticleReaderPageBase : LayoutPageBase<ArticleReaderPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderPageBase"/> class.
    /// </summary>
    protected ArticleReaderPageBase() => ViewModel = this.Get<ArticleReaderPageViewModel>();
}
