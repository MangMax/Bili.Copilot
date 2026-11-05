// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using WinRT;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 视频项视图模型.
/// </summary>
[GeneratedBindableCustomProperty]
public sealed partial class VideoItemViewModel : ViewModelBase<VideoInformation>
{
    private PublisherFollowingState? _followingState;
    private bool _hasCardMetadata;
    private readonly Action<VideoItemViewModel>? _removeAction;
    private readonly VideoFavoriteFolder? _favFolder;
    private readonly Action<VideoItemViewModel>? _playAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoItemViewModel"/> class.
    /// </summary>
    public VideoItemViewModel(
        VideoInformation info,
        VideoCardStyle style,
        Action<VideoItemViewModel> removeAction = default,
        VideoFavoriteFolder? favFolder = default,
        Action<VideoItemViewModel>? playAction = default)
        : base(info)
    {
        _removeAction = removeAction;
        _favFolder = favFolder;
        _playAction = playAction;
        var primaryLan = ApplicationLanguages.Languages[0];
        Style = style;
        Title = HtmlText.Decode(info.Identifier.Title);
        Cover = info.Identifier.Cover?.Uri;
        Author = info.Publisher?.User?.Name
            ?? (style == VideoCardStyle.Partition ? info.GetExtensionIfNotNull<string?>(VideoExtensionDataId.Subtitle) : null);
        Avatar = info.Publisher?.User?.Avatar?.Uri;
        Duration = AppToolkit.FormatDuration(TimeSpan.FromSeconds(info.Duration ?? 0));
        PublishRelativeTime = info.PublishTime is { Year: > 1970 } published
            ? published.Humanize(culture: new CultureInfo(primaryLan)) : null;
        PlayCount = info.CommunityInformation?.PlayCount;
        DanmakuCount = info.CommunityInformation?.DanmakuCount;
        LikeCount = info.CommunityInformation?.LikeCount;
        TagName = info.GetExtensionIfNotNull<string?>(VideoExtensionDataId.TagName);
        RecommendReason = info.GetExtensionIfNotNull<string?>(VideoExtensionDataId.RecommendReason);
        Subtitle = info.GetExtensionIfNotNull<string?>(VideoExtensionDataId.Subtitle);
        if (IsFeedCard && !string.IsNullOrWhiteSpace(info.Publisher?.User?.Id))
        {
            var following = info.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.PublisherIsFollowed)
                ?? (IsServerFollowingReason ? true : null);
            _followingState = new(this.Get<UserRelationBatcher>(), this.Get<AccountViewModel>().MyProfile?.User.Id,
                info.Publisher?.User?.Id ?? string.Empty, following);
            IsAuthorFollowed = _followingState.Current;
        }
        var collected = info.GetExtensionIfNotNull<DateTimeOffset>(VideoExtensionDataId.CollectTime);
        CollectTime = collected.Year > 1970 ? collected.Humanize(default, new CultureInfo("zh-CN")) : null;
        var initialMetadata = VideoCardMetadata.FromVideo(info);
        CreatorCount = initialMetadata.CreatorCount;
        IsCollaborativeSubmission = initialMetadata.IsCollaborativeSubmission;
        IsChargingExclusive = initialMetadata.IsChargingExclusive;
        IsUserValid = info.Publisher?.User is not null;
        var progress = info.GetExtensionIfNotNull<int?>(VideoExtensionDataId.Progress);
        if (progress is not null)
        {
            ProgressText = AppToolkit.FormatDuration(TimeSpan.FromSeconds(progress.Value));
        }
    }

    /// <summary>Consumes the latest shared relationship while preserving this card's successful state.</summary>
    public async Task LoadPopularFollowingAsync(CancellationToken cancellationToken)
    {
        if (_followingState is null) return;
        var account = this.Get<AccountViewModel>();
        var accountId = account.MyProfile?.User.Id;
        var loading = _followingState.LoadAsync(accountId, cancellationToken);
        IsAuthorFollowed = _followingState.Current;
        var following = await loading;
        cancellationToken.ThrowIfCancellationRequested();
        if (account.MyProfile?.User.Id == accountId) IsAuthorFollowed = following;
    }

    /// <summary>Applies missing public card fields from the shared metadata provider.</summary>
    public async Task LoadPopularMetadataAsync(CancellationToken cancellationToken)
    {
        if (!IsFeedCard || _hasCardMetadata) return;
        var provider = this.Get<VideoCardMetadataProvider>();
        var userId = Data.Publisher?.User?.Id;
        if (userId is not null) Avatar ??= provider.GetKnownAvatar(userId);
        var metadata = await provider.GetAsync(Data.Identifier, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        CreatorCount = metadata.CreatorCount;
        IsCollaborativeSubmission = metadata.IsCollaborativeSubmission;
        IsChargingExclusive = metadata.IsChargingExclusive;
        _hasCardMetadata = true;
        // The provider verifies the video identity; partition results may omit the publisher ID.
        if (string.IsNullOrWhiteSpace(userId) || metadata.PublisherId == userId) Avatar ??= metadata.Avatar;
        if (metadata.PublishTime is { Year: > 1970 } published)
            PublishRelativeTime ??= published.Humanize(culture: new CultureInfo(ApplicationLanguages.Languages[0]));
        if (string.IsNullOrWhiteSpace(TagName)) TagName = metadata.TagName;
        if (string.IsNullOrWhiteSpace(userId) && metadata.PublisherId is { Length: > 0 } publisherId)
        {
            _followingState ??= new(this.Get<UserRelationBatcher>(), this.Get<AccountViewModel>().MyProfile?.User.Id,
                publisherId, null);
            await LoadPopularFollowingAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private void Play()
    {
        if (_playAction is not null)
        {
            _playAction(this);
            return;
        }

        this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(Data));
    }

    [RelayCommand]
    private void PlayInPrivate()
    {
        if (_playAction is not null)
        {
            _playAction(this);
            return;
        }

        this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(Data, true));
    }

    [RelayCommand]
    private async Task ShowUserSpaceAsync()
    {
        var user = Data.Publisher?.User;
        if (user is null && Style == VideoCardStyle.Partition && !string.IsNullOrWhiteSpace(Subtitle))
        {
            // 分区列表只返回作者名称，没有用户 ID，点开时再从视频详情补全。
            try
            {
                var detail = await this.Get<IPlayerService>().GetVideoPageDetailAsync(Data.Identifier);
                user = detail.Information.Publisher?.User;
            }
            catch (Exception ex)
            {
                this.Get<ILogger<VideoItemViewModel>>().LogError(ex, "获取视频作者信息失败");
            }
        }

        if (user is not null)
        {
            this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), user);
        }
    }

    [RelayCommand]
    private async Task AddToViewLaterAsync()
    {
        try
        {
            await this.Get<IViewLaterService>().AddAsync(Data.Identifier.Id);
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.AddViewLaterSucceed), InfoType.Success));
        }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoItemViewModel>>().LogError(ex, "添加稍后再看失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.AddViewLaterFailed), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task RemoveViewLaterAsync()
    {
        try
        {
            await this.Get<IViewLaterService>().RemoveAsync([Data.Identifier.Id]);
            _removeAction?.Invoke(this);
        }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoItemViewModel>>().LogError(ex, "移除稍后再看视频失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToRemoveVideoFromViewLater), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task RemoveHistoryAsync()
    {
        try
        {
            await this.Get<IViewHistoryService>().RemoveVideoHistoryItemAsync(Data);
            _removeAction?.Invoke(this);
        }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoItemViewModel>>().LogError(ex, "移除历史记录视频失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToRemoveVideoFromHistory), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task RemoveFavoriteAsync()
    {
        try
        {
            await this.Get<IFavoriteService>().RemoveVideoAsync(_favFolder, Data.Identifier);
            _removeAction?.Invoke(this);
        }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoItemViewModel>>().LogError(ex, "移除收藏视频失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToRemoveVideoFromFavorite), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task OpenInBroswerAsync()
        => await Windows.System.Launcher.LaunchUriAsync(GetWebUri()).AsTask();

    [RelayCommand]
    private void CopyUri()
    {
        var dp = new DataPackage();
        dp.SetText(GetWebUri().ToString());
        dp.SetWebLink(GetWebUri());
        Clipboard.SetContent(dp);
        this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Copied), InfoType.Success));
    }

    [RelayCommand]
    private void Pin()
    {
        var pinItem = new PinItem(Data.Identifier.Id, Data.Identifier.Title, Data.Identifier.Cover.Uri.ToString(), PinContentType.Video);
        this.Get<PinnerViewModel>().AddItemCommand.Execute(pinItem);
    }

    private Uri GetWebUri()
    {
        var shortLink = Data.GetExtensionIfNotNull<string>(VideoExtensionDataId.ShortLink);
        return string.IsNullOrEmpty(shortLink) ? new Uri($"https://www.bilibili.com/video/av{Data.Identifier.Id}") : new Uri(shortLink);
    }
}
