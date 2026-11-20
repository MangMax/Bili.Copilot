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

            if (_updatesAccountId != accountId)
            {
                _updatesAccountId = accountId;
                _latestMomentId = 0;
            }

            var seen = ReadSeenMoment(accountId);
            _latestMomentId = Math.Max(_latestMomentId, LatestId(view));
            if (seen is null)
            {
                WriteSeenMoment(accountId, _latestMomentId);
            }

            SetHasNewMoments(seen is { } id && _latestMomentId > id);
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
        if (!HasNewMoments || Sections?.LastOrDefault() is not ComprehensiveMomentSectionDetailViewModel section)
        {
            return;
        }

        if (section.InitializeCommand.ExecutionTask is { IsCompleted: false } initialization)
        {
            await initialization;
        }

        var view = await section.RefreshAllAsync();
        if (view is null || this.Get<IBiliTokenResolver>().GetToken()?.UserId != accountId)
        {
            return;
        }

        SelectSection(section);
        var seen = Math.Max(ReadSeenMoment(accountId).GetValueOrDefault(), LatestId(view));
        WriteSeenMoment(accountId, seen);
        SetHasNewMoments(_latestMomentId > seen);
    }

    private void SetHasNewMoments(bool value)
    {
        HasNewMoments = value;
        this.Get<NavigationViewModel>().SetMomentUnread(value);
    }

    private static ulong LatestId(MomentView view)
        => view.Moments.Select(moment => ulong.TryParse(moment.Id, out var id) ? id : 0UL).DefaultIfEmpty().Max();

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
            new VideoMomentSectionDetailViewModel(_momentDiscoveryService),
            new ComprehensiveMomentSectionDetailViewModel(_momentDiscoveryService),
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
