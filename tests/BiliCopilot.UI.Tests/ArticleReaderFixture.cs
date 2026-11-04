// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

internal static class ArticleReaderFixture
{
    public static ArticleDetail Detail(ArticleIdentifier identifier, bool? liked = false, bool? favorited = false)
    {
        var id = identifier.Id!;
        var user = new UserProfile(id, $"Author {id}", new BiliImage(new Uri($"https://example.invalid/{id}.png")));
        var card = new UserCard(new UserDetailProfile(user), new UserCommunityInformation(id));
        return new ArticleDetail(
            identifier,
            card,
            new ArticleCommunityInformation(id),
            $"<p>{id}</p>",
            DateTimeOffset.UnixEpoch,
            [],
            liked,
            favorited);
    }

    public sealed class Operation : IArticleOperationService
    {
        public Task? Pending { get; set; }
        public List<ArticleIdentifier> Targets { get; } = [];

        public List<bool> Likes { get; } = [];
        public List<bool> Favorites { get; } = [];

        public Task ToggleLikeAsync(ArticleIdentifier article, bool isLike, CancellationToken cancellationToken = default)
        {
            Targets.Add(article);
            Likes.Add(isLike);
            return Pending ?? Task.CompletedTask;
        }

        public Task ToggleFavoriteAsync(ArticleIdentifier article, bool isFavorite, CancellationToken cancellationToken = default)
        {
            Targets.Add(article);
            Favorites.Add(isFavorite);
            return Pending ?? Task.CompletedTask;
        }
    }
}
