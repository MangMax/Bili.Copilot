// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 番剧/电影/电视剧详情页视图模型.
/// </summary>
public sealed partial class PgcDetailPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailPageViewModel"/> class.
    /// </summary>
    public PgcDetailPageViewModel(
        IPlayerService playerService,
        IEntertainmentDiscoveryService entertainmentService,
        ILogger<PgcDetailPageViewModel> logger)
    {
        _playerService = playerService;
        _entertainmentService = entertainmentService;
        _logger = logger;
    }

    /// <summary>
    /// 加载指定季度的详情.
    /// </summary>
    /// <param name="seasonId">季度 Id.</param>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeAsync(string seasonId)
    {
        if (string.IsNullOrWhiteSpace(seasonId))
        {
            return;
        }

        var generation = ++_loadGeneration;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsLoading = true;
        HasError = false;
        ClearContent();
        try
        {
            var view = await _playerService.GetPgcPageDetailAsync(seasonId, null, cancellation.Token);
            if (generation != _loadGeneration)
            {
                return;
            }

            _view = view;
            ApplyView(view);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 目标已切换，结果作废.
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            _logger.LogError(ex, "获取剧集详情失败");
            HasError = true;
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
            }

            cancellation.Dispose();
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }
        }
    }

    /// <summary>
    /// 取消当前加载；页面被隐藏或关闭时调用.
    /// </summary>
    public void CancelLoad()
    {
        _loadGeneration++;
        var cancellation = _loadCancellation;
        _loadCancellation = null;
        if (cancellation is not null)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 加载已经自行结束并释放，取消是幂等的.
            }

            cancellation.Dispose();
        }

        IsLoading = false;
    }

    /// <summary>
    /// 播放选中的剧集.
    /// </summary>
    /// <param name="episode">剧集.</param>
    [RelayCommand]
    private void PlayEpisode(EpisodeItemViewModel episode)
    {
        if (episode is null || _view is null)
        {
            return;
        }

        this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(_view.Information, episode.Data));
    }

    /// <summary>
    /// 继续播放：优先上次进度，其次第一集.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        if (_view?.Episodes is not { Count: > 0 } episodes)
        {
            return;
        }

        var progressId = _view.Progress?.Cid;
        var target = string.IsNullOrEmpty(progressId)
            ? episodes[0]
            : episodes.FirstOrDefault(p => p.Identifier.Id == progressId) ?? episodes[0];
        this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(_view.Information, target));
    }

    /// <summary>
    /// 追番/取消追番.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        if (_view is null)
        {
            return;
        }

        var seasonId = _view.Information.Identifier.Id;
        try
        {
            if (IsFollowed)
            {
                await _entertainmentService.UnfollowAsync(seasonId);
                IsFollowed = false;
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Unfollowed), InfoType.Success));
            }
            else
            {
                await _entertainmentService.FollowAsync(seasonId);
                IsFollowed = true;
                this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Followed), InfoType.Success));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换追番状态失败");
            var message = ResourceToolkit.GetLocalizedString(IsFollowed ? StringNames.FailedToUnfollowPgc : StringNames.FailedToFollowPgc);
            this.Get<AppViewModel>().ShowTipCommand.Execute((message, InfoType.Error));
        }
    }

    /// <summary>
    /// 在当前季度里重新加载.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_view is null)
        {
            return;
        }

        await InitializeAsync(_view.Information.Identifier.Id);
    }

    /// <summary>
    /// 切换分区.
    /// </summary>
    /// <param name="tab">目标分区.</param>
    public void SelectTab(PgcDetailTabViewModel? tab)
    {
        if (tab is null || ReferenceEquals(tab, SelectedTab))
        {
            return;
        }

        SelectedTab = tab;
        Episodes = [.. tab.Episodes];
        HasEpisodes = Episodes.Count > 0;
        ShowIntro = tab.IsIntro;
    }

    private void ApplyView(PgcPlayerView view)
    {
        var information = view.Information;
        SeasonTitle = information.Identifier.Title;
        Cover = information.Identifier.Cover?.Uri;
        Alias = information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Alias);
        Description = information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Description);
        Tags = information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Tags);
        ProgressText = information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.DisplayProgress);
        var score = information.GetExtensionIfNotNull<double>(SeasonExtensionDataId.Score);
        ScoreText = score > 0 ? score.ToString("0.0") : string.Empty;
        var ratingCount = information.GetExtensionIfNotNull<int>(SeasonExtensionDataId.RatingCount);
        RatingCountText = ratingCount > 0
            ? string.Format(ResourceToolkit.GetLocalizedString(StringNames.RatingCountTemplate), AppToolkit.FormatCount(ratingCount))
            : string.Empty;
        PlayCount = information.CommunityInformation?.PlayCount;
        DanmakuCount = information.CommunityInformation?.DanmakuCount;
        FollowCount = information.CommunityInformation?.TrackCount;
        IsFollowed = information.IsTracking ?? false;

        var tabs = new List<PgcDetailTabViewModel>();
        if (view.Episodes is { Count: > 0 } episodes)
        {
            tabs.Add(new PgcDetailTabViewModel(
                ResourceToolkit.GetLocalizedString(StringNames.MainEpisode),
                [.. episodes.Select(p => new EpisodeItemViewModel(p, EpisodeCardStyle.Detail))]));
        }

        if (view.Extras is not null)
        {
            foreach (var section in view.Extras)
            {
                if (section.Value is not { Count: > 0 })
                {
                    continue;
                }

                tabs.Add(new PgcDetailTabViewModel(
                    section.Key,
                    [.. section.Value.Select(p => new EpisodeItemViewModel(p, EpisodeCardStyle.Detail))]));
            }
        }

        tabs.Add(new PgcDetailTabViewModel(
            ResourceToolkit.GetLocalizedString(StringNames.MediaReview),
            [],
            isIntro: true));
        Tabs = tabs;

        var labor = information.GetExtensionIfNotNull<Dictionary<string, string>>(SeasonExtensionDataId.LaborSections);
        LaborItems = labor is null
            ? []
            : [.. labor.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => new PgcLaborItemViewModel(p.Key, p.Value))];
        HasLabor = LaborItems.Count > 0;

        SelectTab(tabs.FirstOrDefault(p => p.HasEpisodes) ?? tabs.FirstOrDefault());
        if (SelectedTab is not null)
        {
            Episodes = [.. SelectedTab.Episodes];
            HasEpisodes = Episodes.Count > 0;
            ShowIntro = SelectedTab.IsIntro;
        }
    }

    private void ClearContent()
    {
        _view = null;
        SeasonTitle = string.Empty;
        Cover = default;
        Alias = string.Empty;
        Description = string.Empty;
        Tags = string.Empty;
        ProgressText = string.Empty;
        ScoreText = string.Empty;
        PlayCount = default;
        DanmakuCount = default;
        FollowCount = default;
        IsFollowed = false;
        Tabs = [];
        SelectedTab = default;
        Episodes = [];
        HasEpisodes = false;
        ShowIntro = false;
        LaborItems = [];
        HasLabor = false;
        RatingCountText = string.Empty;
    }
}
