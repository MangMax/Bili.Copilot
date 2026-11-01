// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters for the article tests. The view models under test
// (ArticlePartitionDetailViewModel / ArticleReaderPageViewModel) and the
// discovery service interface are production code. Only the immutable card
// item, the unrelated comment/AI modules and the OS surface are shimmed.

using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.ViewModels.Items
{
    /// <summary>
    /// Immutable article card source model used by the partition view model.
    /// Identity is carried by <see cref="Data"/>, as in production.
    /// </summary>
    public sealed class ArticleItemViewModel(ArticleInformation data, ArticleCardStyle style)
    {
        public ArticleInformation Data { get; } = data;

        public ArticleCardStyle Style { get; } = style;
    }
}

namespace BiliCopilot.UI.ViewModels.Components
{
    /// <summary>
    /// The reader page only resets the comment source; comment loading is not
    /// part of the article content assertions.
    /// </summary>
    public sealed class CommentMainViewModel
    {
        public List<object> Comments { get; } = new();

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand LoadItemsCommand { get; }
            = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);

        public string? LastSourceId { get; private set; }

        public void Initialize(string sourceId, CommentTargetType targetType, CommentSortType sortType)
            => LastSourceId = sourceId;
    }
}

namespace BiliCopilot.UI.ViewModels.Core
{
    /// <summary>
    /// AI prompts are unrelated to article content loading; record the injection
    /// so a stale response can be detected if it ever reaches this module.
    /// </summary>
    public sealed class AIViewModel
    {
        public List<ArticleDetail> Injected { get; } = new();

        public CommunityToolkit.Mvvm.Input.IRelayCommand InitializeCommand { get; }
            = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });

        public CommunityToolkit.Mvvm.Input.IRelayCommand ClearCommand { get; }
            = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });

        public void InjectArticleAsync(ArticleDetail article) => Injected.Add(article);
    }
}
