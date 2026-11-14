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
        PlayCount = information.CommunityInformation?.PlayCount;
        DanmakuCount = information.CommunityInformation?.DanmakuCount;
        FollowCount = information.CommunityInformation?.TrackCount;
        IsFollowed = information.IsTracking ?? false;
        Episodes = view.Episodes is null
            ? []
            : [.. view.Episodes.Select(p => new EpisodeItemViewModel(p, EpisodeCardStyle.Detail))];
        HasEpisodes = Episodes.Count > 0;
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
        Episodes = [];
        HasEpisodes = false;
    }
}
