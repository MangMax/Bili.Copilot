// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinRT;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 动态条目视图模型.
/// </summary>
[GeneratedBindableCustomProperty]
public sealed partial class MomentItemViewModel : ViewModelBase<MomentInformation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MomentItemViewModel"/> class.
    /// </summary>
    public MomentItemViewModel(MomentInformation data, MomentCardStyle style, Action<MomentItemViewModel> showCommentAction, bool isForwarded = false)
        : base(data)
    {
        _showMomentAction = showCommentAction;
        _operationService = this.Get<IMomentOperationService>();
        _logger = this.Get<ILogger<MomentItemViewModel>>();
        IsLiked = data.CommunityInformation?.IsLiked ?? false;
        LikeCount = data.CommunityInformation?.LikeCount;
        CommentCount = data.CommunityInformation?.CommentCount;
        Author = data.User?.Name?.TrimStart('@');
        ForwardHint = isForwarded && !string.IsNullOrEmpty(Author)
            ? string.Format(ResourceToolkit.GetLocalizedString(StringNames.ForwardedFromTemplate), Author)
            : null;
        Avatar = data.User?.Avatar?.Uri;
        Tip = data.Tip;
        Description = data.Description;
        NoData = data.Data is null;
        Style = style;

        if (!NoData)
        {
            if (data.Data is MomentInformation forward)
            {
                // 被转发的动态自己带上来源提示，转发卡里显示「转发了 @作者」.
                InnerContent = new MomentItemViewModel(forward, Style, showCommentAction, isForwarded: true);
            }
            else if (data.Data is VideoInformation video)
            {
                InnerContent = new VideoItemViewModel(video, VideoCardStyle.Moment);
            }
            else if (data.Data is EpisodeInformation episode)
            {
                InnerContent = new EpisodeItemViewModel(episode, EpisodeCardStyle.Moment);
            }
            else if (data.Data is IEnumerable<BiliImage> images)
            {
                InnerContent = images.ToList();
            }

            if (FindInnerContent<VideoInformation>() is VideoInformation vinfo)
            {
                VideoTitle = vinfo.Identifier.Title ?? ResourceToolkit.GetLocalizedString(StringNames.NoTitleVideo);
                VideoCover = vinfo.Identifier.Cover.Uri;
                VideoDuration = AppToolkit.FormatDuration(TimeSpan.FromSeconds(vinfo.Duration ?? 0));
            }
            else if (FindInnerContent<EpisodeInformation>() is EpisodeInformation einfo)
            {
                IsPgc = true;
                SeasonId = einfo.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.SeasonId).ToString();
                VideoTitle = einfo.Identifier.Title;
                VideoCover = einfo.Identifier.Cover.Uri;
                VideoDuration = AppToolkit.FormatDuration(TimeSpan.FromSeconds(einfo.Duration ?? 0));
            }
        }
    }

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        var state = !IsLiked;
        try
        {
            if (state)
            {
                await _operationService.LikeMomentAsync(Data);
            }
            else
            {
                await _operationService.DislikeMomentAsync(Data);
            }

            IsLiked = state;
            LikeCount = Math.Max(0, (LikeCount ?? 0) + (state ? 1 : -1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换评论点赞状态时失败");
        }
    }

    [RelayCommand]
    private void CopyMomentLink()
    {
        var url = new Uri($"https://t.bilibili.com/{Data.Id}");
        var package = new DataPackage();
        package.SetText(url.AbsoluteUri);
        package.SetWebLink(url);
        Clipboard.SetContent(package);
        this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Copied), InfoType.Success));
    }

    [RelayCommand]
    private void ShowComment()
        => _showMomentAction?.Invoke(this);

    /// <summary>
    /// 补充收藏数。动态流本身只带点赞/评论数，收藏数要按视频详情补取一次。
    /// </summary>
    public async Task LoadFavoriteCountAsync()
    {
        if (FavoriteCount is not null || GetFavoriteAid() is not { } aid)
        {
            return;
        }

        MediaIdentifier? identifier = FindInnerContent<VideoInformation>()?.Identifier;
        if (identifier is null && FindInnerContent<EpisodeInformation>() is EpisodeInformation episode)
        {
            identifier = new MediaIdentifier(aid, episode.Identifier.Title, episode.Identifier.Cover);
        }

        if (identifier is null)
        {
            return;
        }

        try
        {
            var metadata = await this.Get<VideoCardMetadataProvider>().GetAsync(identifier.Value);
            if (metadata.FavoriteCount is >= 0)
            {
                FavoriteCount = metadata.FavoriteCount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取视频收藏数失败");
        }
    }

    [RelayCommand]
    private async Task InitializeFavoritesAsync()
    {
        if (_favoriteFolders is not null || GetFavoriteAid() is not { } aid)
        {
            return;
        }

        try
        {
            IsLoadingFavorites = true;
            IsFavoriteFoldersLoadFailed = false;
            var (folders, ids) = await this.Get<IFavoriteService>().GetPlayingVideoFavoriteFoldersAsync(aid);
            IsFavorited = ids.Count > 0;
            _favoriteFolders = folders.Select(p => new PlayerFavoriteFolderViewModel(p, ids.Contains(p.Id))).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试获取收藏夹列表时失败");
            IsFavoriteFoldersLoadFailed = true;
        }
        finally
        {
            IsLoadingFavorites = false;
            ApplyFavoriteSearch();
        }
    }

    /// <summary>
    /// 按搜索词刷新过滤后的收藏夹列表.
    /// </summary>
    private void ApplyFavoriteSearch()
    {
        if (_favoriteFolders is null)
        {
            FilteredFavoriteFolders = default;
            HasNoFavoriteFolderResults = false;
            return;
        }

        var keyword = FavoriteSearchText.Trim();
        FilteredFavoriteFolders = string.IsNullOrEmpty(keyword)
            ? _favoriteFolders.ToList()
            : _favoriteFolders.Where(p => p.Data.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true).ToList();
        HasNoFavoriteFolderResults = FilteredFavoriteFolders.Count == 0;
    }

    /// <summary>
    /// 放弃这次打开弹窗产生的临时选择；下次重新从服务端读取收藏夹.
    /// </summary>
    public void DiscardFavoriteFolders()
    {
        _favoriteFolders = null;
        IsFavoriteFoldersLoadFailed = false;
        FavoriteSearchText = string.Empty;
        ApplyFavoriteSearch();
    }

    partial void OnFavoriteSearchTextChanged(string value)
        => ApplyFavoriteSearch();

    [RelayCommand]
    private async Task FavoriteAsync()
    {
        if (_favoriteFolders is null || GetFavoriteAid() is not { } aid)
        {
            return;
        }

        var selectedFolders = _favoriteFolders.Where(p => p.IsSelected).Select(p => p.Data.Id).ToList();
        var unselectedFolders = _favoriteFolders.Where(p => !p.IsSelected).Select(p => p.Data.Id).ToList();
        try
        {
            var wasFavorited = IsFavorited;
            await this.Get<IPlayerService>().FavoriteVideoAsync(aid, selectedFolders, unselectedFolders);
            var nowFavorited = selectedFolders.Count != 0;
            if (FavoriteCount is >= 0 && wasFavorited != nowFavorited)
            {
                FavoriteCount = Math.Max(0, FavoriteCount.Value + (nowFavorited ? 1 : -1));
            }

            IsFavorited = nowFavorited;
            DiscardFavoriteFolders();
            if (nowFavorited || wasFavorited)
            {
                this.Get<AppViewModel>().ShowTipCommand.Execute((
                    ResourceToolkit.GetLocalizedString(nowFavorited ? StringNames.FavoriteSucceed : StringNames.UnFavoriteSucceed),
                    InfoType.Success));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试收藏视频时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FavoriteFailed), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (FindInnerContent<VideoInformation>() is VideoInformation vinfo)
        {
            var snapshot = new MediaSnapshot(vinfo);
            this.Get<AppViewModel>().OpenPlayerCommand.Execute(snapshot);
        }
        else if (FindInnerContent<EpisodeInformation>() is EpisodeInformation einfo)
        {
            if (einfo.Identifier.Id == "0")
            {
                // 出差番剧没有可在站内播放的剧集号，使用网页打开.
                OpenInBroswerCommand.Execute(default);
                return;
            }

            // 带上动态里的完整剧集信息：花絮、预告片等不在季度正片列表里的内容，
            // 播放器需要按这个 ep 号取播放地址，而不是回退到正片第一集.
            this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(default, einfo));
        }
        else if (FindInnerContent<LiveInformation>() is LiveInformation linfo)
        {
            await Launcher.LaunchUriAsync(new Uri($"https://live.bilibili.com/{linfo.Identifier.Id}"));
        }
        else if (FindInnerContent<IEnumerable<BiliImage>>() is not IEnumerable<BiliImage>)
        {
            OpenInBroswerCommand.Execute(default);
        }
    }

    [RelayCommand]
    private void ShowUserSpace()
        => this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), Data.User);

    [RelayCommand]
    private void PlayInPrivate()
    {
        var vinfo = FindInnerContent<VideoInformation>();
        if (vinfo is null)
        {
            return;
        }

        var snapshot = new MediaSnapshot(vinfo, true);
        this.Get<AppViewModel>().OpenPlayerCommand.Execute(snapshot);
    }

    [RelayCommand]
    private async Task AddToViewLaterAsync()
    {
        var vinfo = FindInnerContent<VideoInformation>();
        var aid = string.Empty;
        if (vinfo is null)
        {
            var einfo = FindInnerContent<EpisodeInformation>();
            if (einfo is null)
            {
                return;
            }

            aid = einfo.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Aid).ToString();
        }
        else
        {
            aid = vinfo.Identifier.Id;
        }

        try
        {
            await this.Get<IViewLaterService>().AddAsync(aid);
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.AddViewLaterSucceed), InfoType.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加稍后再看失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.AddViewLaterFailed), InfoType.Error));
        }
    }

    [RelayCommand]
    private async Task OpenInBroswerAsync()
    {
        var url = GetMediaUrl() ?? $"https://t.bilibili.com/{Data.Id}";
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    [RelayCommand]
    private void CopyUrl()
    {
        var url = GetMediaUrl();
        if (url is not null)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(url);
            dataPackage.SetWebLink(new Uri(url));
            Clipboard.SetContent(dataPackage);
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Copied), InfoType.Success));
        }
    }

    [RelayCommand]
    private void Pin()
    {
        PinItem pinItem = default;
        if (FindInnerContent<VideoInformation>() is VideoInformation vinfo)
        {
            pinItem = new PinItem(vinfo.Identifier.Id, vinfo.Identifier.Title, vinfo.Identifier.Cover.Uri.ToString(), PinContentType.Video);
        }
        else if (FindInnerContent<EpisodeInformation>() is EpisodeInformation einfo)
        {
            pinItem = einfo.Identifier.Id == "0"
                ? new PinItem($"ss_{einfo.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.SeasonId)}", einfo.Identifier.Title, einfo.Identifier.Cover.Uri.ToString(), PinContentType.Pgc)
                : new PinItem($"ep_{einfo.Identifier.Id}", einfo.Identifier.Title, einfo.Identifier.Cover.Uri.ToString(), PinContentType.Pgc);
        }

        if (pinItem is not null)
        {
            this.Get<PinnerViewModel>().AddItemCommand.Execute(pinItem);
        }
    }

    private string? GetFavoriteAid()
    {
        if (FindInnerContent<VideoInformation>() is VideoInformation vinfo)
        {
            return vinfo.Identifier.Id;
        }

        if (FindInnerContent<EpisodeInformation>() is EpisodeInformation einfo)
        {
            // 番剧收藏沿用播放器的做法：按剧集关联的稿件 Id 收藏到视频收藏夹.
            var aid = einfo.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Aid);
            return aid > 0 ? aid.ToString() : null;
        }

        return null;
    }

    private string? GetMediaUrl()
    {
        if (FindInnerContent<VideoInformation>() is VideoInformation vinfo)
        {
            return $"https://www.bilibili.com/av{vinfo.Identifier.Id}";
        }
        else if (FindInnerContent<EpisodeInformation>() is EpisodeInformation episodeInformation)
        {
            if (episodeInformation.Identifier.Id == "0")
            {
                // 出差番剧.
                var ssid = episodeInformation.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.SeasonId);
                return $"https://www.bilibili.com/bangumi/play/ss{ssid}";
            }

            return $"https://www.bilibili.com/bangumi/play/ep{episodeInformation.Identifier.Id}";
        }

        return default;
    }

    private T? FindInnerContent<T>()
        where T : class
    {
        if (Data.Data is T info)
        {
            return info;
        }
        else if (Data.Data is MomentInformation minfo && minfo.Data is T info2)
        {
            return info2;
        }

        return default;
    }
}
