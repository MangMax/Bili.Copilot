// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;
using Richasy.WinUIKernel.Share.ViewModels;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 动态页面视图模型.
/// </summary>
public sealed partial class MomentPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MomentPageViewModel"/> class.
    /// </summary>
    public MomentPageViewModel(
        IMomentDiscoveryService discoveryService,
        CommentMainViewModel comment)
    {
        _momentDiscoveryService = discoveryService;
        CommentModule = comment;
    }

    [RelayCommand]
    private void Initialize()
        => InitializeSections();

    internal async Task CheckForUpdatesAsync(long accountId, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var view = await _momentDiscoveryService.GetComprehensiveMomentsAsync(cancellationToken: timeout.Token);
            cancellationToken.ThrowIfCancellationRequested();
            if (this.Get<IBiliTokenResolver>().GetToken()?.UserId != accountId)
            {
                return;
            }

            UseUpdatesAccount(accountId);

            var seen = ReadSeenMoment(accountId);
            var (latestAll, latestVideo, latestNonVideo) = MomentRefreshPolicy.ExtractLatestIds(view);
            // The first successful check establishes the baseline for in-app updates in this session.
            _startupMomentId ??= latestAll;
            _latestMomentId = Math.Max(_latestMomentId, latestAll);
            _latestVideoMomentId = Math.Max(_latestVideoMomentId, latestVideo);
            _latestNonVideoMomentId = Math.Max(_latestNonVideoMomentId, latestNonVideo);
            if (seen is null)
            {
                WriteSeenMoment(accountId, _latestMomentId);
            }

            UpdateUnread(seen ?? _latestMomentId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            this.Get<ILogger<MomentPageViewModel>>().LogDebug(ex, "检查应用内动态提示失败。");
        }
    }

    [RelayCommand]
    private async Task RefreshNewMomentsAsync()
    {
        var accountId = _updatesAccountId;
        if (!HasNewMoments || Sections?.Count is not > 0)
        {
            return;
        }

        var seenBefore = ReadSeenMoment(accountId).GetValueOrDefault();
        var checkedLatestAll = _latestMomentId;
        var checkedLatestVideo = _latestVideoMomentId;
        var checkedLatestNonVideo = _latestNonVideoMomentId;
        var refreshVideo = MomentRefreshPolicy.ShouldRefreshVideoSection(
            SelectedSection is VideoMomentSectionDetailViewModel,
            GetUnreadBaseline(seenBefore),
            checkedLatestVideo,
            checkedLatestNonVideo,
            _loadedVideoMomentId);

        MomentView? view;
        if (refreshVideo && Sections.OfType<VideoMomentSectionDetailViewModel>().FirstOrDefault() is { } videoSection)
        {
            if (videoSection.InitializeCommand.ExecutionTask is { IsCompleted: false } initialization)
            {
                await initialization;
            }

            view = await videoSection.RefreshWithResultAsync();
            if (view is null || this.Get<IBiliTokenResolver>().GetToken()?.UserId != accountId)
            {
                return;
            }
        }
        else if (Sections.OfType<ComprehensiveMomentSectionDetailViewModel>().FirstOrDefault() is { } comprehensiveSection)
        {
            refreshVideo = false;
            if (comprehensiveSection.InitializeCommand.ExecutionTask is { IsCompleted: false } initialization)
            {
                await initialization;
            }

            view = await comprehensiveSection.RefreshAllAsync();
            if (view is null || this.Get<IBiliTokenResolver>().GetToken()?.UserId != accountId)
            {
                return;
            }

            SelectSection(comprehensiveSection);
        }
        else
        {
            return;
        }

        var seen = MomentRefreshPolicy.ResolveSeenAfterRefresh(
            refreshVideo,
            ReadSeenMoment(accountId).GetValueOrDefault(),
            MomentRefreshPolicy.LatestId(view),
            checkedLatestAll,
            checkedLatestVideo);
        WriteSeenMoment(accountId, seen);
        UpdateUnread(seen);
    }

    private void RecordLoadedMoments(long accountId, MomentView view, bool videoOnly)
    {
        if (this.Get<IBiliTokenResolver>().GetToken()?.UserId != accountId)
        {
            return;
        }

        var latest = MomentRefreshPolicy.LatestId(view);
        if (latest == 0)
        {
            return;
        }

        UseUpdatesAccount(accountId);
        var seen = ReadSeenMoment(accountId).GetValueOrDefault();
        if (videoOnly)
        {
            _loadedVideoMomentId = Math.Max(_loadedVideoMomentId, latest);
        }
        else if (latest > seen)
        {
            seen = latest;
            WriteSeenMoment(accountId, seen);
        }

        UpdateUnread(seen);
    }

    private void UseUpdatesAccount(long accountId)
    {
        if (_updatesAccountId == accountId)
        {
            return;
        }

        _updatesAccountId = accountId;
        _startupMomentId = null;
        _latestMomentId = 0;
        _latestVideoMomentId = 0;
        _latestNonVideoMomentId = 0;
        _loadedVideoMomentId = 0;
    }

    private void UpdateUnread(ulong seen)
        => SetHasNewMoments(MomentRefreshPolicy.HasUnreadMoments(
            GetUnreadBaseline(seen), _loadedVideoMomentId, _latestVideoMomentId, _latestNonVideoMomentId));

    private ulong GetUnreadBaseline(ulong seen)
        => Math.Max(seen, _startupMomentId.GetValueOrDefault());

    private void SetHasNewMoments(bool value)
    {
        HasNewMoments = value;
        this.Get<NavigationViewModel>().SetMomentUnread(value);
    }

    private ulong? ReadSeenMoment(long accountId)
        => ulong.TryParse(this.Get<ISettingsToolkit>().ReadLocalSetting($"LastSeenMoment_{accountId}", string.Empty), out var id) ? id : null;

    private void WriteSeenMoment(long accountId, ulong id)
        => this.Get<ISettingsToolkit>().WriteLocalSetting($"LastSeenMoment_{accountId}", id.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal void ShowNotificationSummary(bool videoOnly)
    {
        if (Sections?.Count is not > 0)
        {
            InitializeSections(videoOnly);
            return;
        }

        var section = videoOnly ? Sections.First() : Sections.Last();
        SelectSection(section);
        if (section.InitializeCommand.IsRunning)
        {
            return;
        }

        if (section is ComprehensiveMomentSectionDetailViewModel comprehensive)
        {
            comprehensive.RefreshAll();
        }
        else
        {
            section.RefreshCommand.Execute(default);
        }
    }

    private void InitializeSections(bool? videoOnly = null)
    {
        if (Sections?.Count > 0)
        {
            return;
        }

        Sections = new List<IMomentSectionDetailViewModel>
        {
            new VideoMomentSectionDetailViewModel(_momentDiscoveryService,
                (accountId, view) => RecordLoadedMoments(accountId, view, videoOnly: true)),
            new ComprehensiveMomentSectionDetailViewModel(_momentDiscoveryService,
                (accountId, view) => RecordLoadedMoments(accountId, view, videoOnly: false)),
        };

        var isLastVideoSection = videoOnly ?? SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.LastMomentSectionIsVideo, true);
        var section = isLastVideoSection ? Sections.First() : Sections.Last();
        SelectSectionCommand.Execute(section);
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, () => Initialized?.Invoke(this, EventArgs.Empty));
    }

    [RelayCommand]
    private void SelectSection(IMomentSectionDetailViewModel section)
    {
        if (section is null || section == SelectedSection)
        {
            return;
        }

        SelectedSection = section;
        var isVideo = section is VideoMomentSectionDetailViewModel;
        SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.LastMomentSectionIsVideo, isVideo);
        SelectedSection.InitializeCommand.Execute(default);
    }

    [RelayCommand]
    private void ShowComment(MomentInformation data)
    {
        IsCommentsOpened = true;
        if (CommentModule.Id == data.CommentId)
        {
            return;
        }

        CommentModule.Initialize(data.CommentId, data.CommentType!.Value, Richasy.BiliKernel.Models.CommentSortType.Hot);
        CommentModule.RefreshCommand.Execute(default);
    }
}
