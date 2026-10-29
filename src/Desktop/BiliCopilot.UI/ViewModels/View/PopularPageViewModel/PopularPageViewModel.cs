// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Media;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 流行视频页面视图模型.
/// </summary>
public sealed partial class PopularPageViewModel : LayoutPageViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PopularPageViewModel"/> class.
    /// </summary>
    public PopularPageViewModel(
        IVideoDiscoveryService discoveryService,
        ILogger<PopularPageViewModel> logger)
    {
        _service = discoveryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override string GetPageKey() => nameof(PopularPage);

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Sections.Count > 0)
        {
            return;
        }

        Sections.Add(new PopularSectionItemViewModel(FluentIcons.Common.Symbol.Balloon, ResourceToolkit.GetLocalizedString(StringNames.Recommend), PopularSectionType.Recommend));
        Sections.Add(new PopularSectionItemViewModel(FluentIcons.Common.Symbol.Fire, ResourceToolkit.GetLocalizedString(StringNames.Hot), PopularSectionType.Hot));
        Sections.Add(new PopularSectionItemViewModel(FluentIcons.Common.Symbol.RibbonStar, ResourceToolkit.GetLocalizedString(StringNames.Rank), PopularSectionType.Rank));
        await LoadPartitionsAsync();
        await Task.Delay(200);
        var lastSelectedSectionId = SettingsToolkit.ReadLocalSetting(SettingNames.PopularPageLastSelectedSectionId, PopularSectionType.Recommend.ToString());
        if (int.TryParse(lastSelectedSectionId, out var partitionId))
        {
            var section = Sections.OfType<PopularRankPartitionViewModel>().FirstOrDefault(p => p.Data.Id == partitionId.ToString());
            if (section != null)
            {
                SelectSectionCommand.Execute(section);
            }
            else
            {
                SelectSectionCommand.Execute(Sections.First());
            }
        }
        else if (Enum.TryParse<PopularSectionType>(lastSelectedSectionId, out var sectionType))
        {
            var section = Sections.OfType<PopularSectionItemViewModel>().First(p => p.Type == sectionType);
            SelectSectionCommand.Execute(section);
        }
        else
        {
            SelectSectionCommand.Execute(Sections.First());
        }

        SectionInitialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectSection(IPopularSectionItemViewModel vm)
    {
        if (vm is null || vm == SelectedSection)
        {
            return;
        }

        ClearRefreshTip();

        if (Videos.Count > 0)
        {
            _videoCache[SelectedSection] = Videos.ToList();
        }

        SelectedSection = vm;
        var sectionId = vm switch
        {
            PopularSectionItemViewModel section => section.Type.ToString(),
            PopularRankPartitionViewModel partition => partition.Data.Id,
            _ => string.Empty
        };
        SettingsToolkit.WriteLocalSetting(SettingNames.PopularPageLastSelectedSectionId, sectionId);

        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            Videos.Clear();
            if (_videoCache.TryGetValue(vm, out var cacheVideos))
            {
                foreach (var video in cacheVideos)
                {
                    Videos.Add(video);
                }

                VideoListUpdated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await LoadVideosAsync();
            }
        });
    }

    [RelayCommand]
    private async Task LoadVideosAsync()
    {
        if (IsVideoLoading || _isRecommendRefreshing)
        {
            return;
        }

        if (SelectedSection is PopularSectionItemViewModel section2 && section2.Type == PopularSectionType.Rank && Videos.Count > 0)
        {
            return;
        }

        IsVideoLoading = true;
        if (SelectedSection is PopularSectionItemViewModel section)
        {
            if (section.Type == PopularSectionType.Recommend)
            {
                await LoadRecommendVideosAsync();
            }
            else if (section.Type == PopularSectionType.Hot)
            {
                await LoadHotVideosAsync();
            }
            else if (section.Type == PopularSectionType.Rank && Videos.Count == 0)
            {
                await LoadTotalRankVideosAsync();
            }
        }
        else if (SelectedSection is PopularRankPartitionViewModel partition && Videos.Count == 0)
        {
            await LoadPartitionRankVideosAsync(partition.Data);
        }

        VideoListUpdated?.Invoke(this, EventArgs.Empty);
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, () => IsVideoLoading = false);
    }

    [RelayCommand]
    private async Task RefreshVideoAsync()
    {
        if (SelectedSection is PopularSectionItemViewModel { Type: PopularSectionType.Recommend })
        {
            await RefreshRecommendVideosAsync();
            return;
        }

        ClearRefreshTip();
        Videos.Clear();
        _videoCache.Remove(SelectedSection);
        if (SelectedSection is PopularSectionItemViewModel { Type: PopularSectionType.Hot })
        {
            _hotOffset = 0;
        }

        await LoadVideosAsync();
    }

    [RelayCommand]
    private void UndoRefresh()
    {
        if (_recommendRefreshSnapshot is null)
        {
            return;
        }

        var snapshot = _recommendRefreshSnapshot;

        // 撤销同样走最小差异：仍在原位且未变化的条目保持原引用，避免整列表重建。
        RecommendListDiff.Apply(Videos, snapshot.Items, p => p.Data.Identifier.Id);

        _recommendOffset = snapshot.Offset;
        ClearRefreshTip();
        VideoListUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清除撤销快照与刷新提示.
    /// </summary>
    /// <remarks>
    /// 切换分区、走普通刷新或撤销时 <see cref="Videos"/> 会被替换或重载，
    /// 旧快照指向的列表已不在页面上，必须一并失效；同时递增代次，让仍在路上的推荐刷新结果作废。
    /// </remarks>
    private void ClearRefreshTip()
    {
        _recommendRefreshSnapshot = null;
        RefreshTipState = Toolkits.RefreshTipState.Hidden;
        _refreshGeneration++;
    }

    [RelayCommand]
    private async Task PlayCuratedListAsync()
    {
        try
        {
            var videos = await _service.GetCuratedPlaylistAsync();
            var firstSnapshot = new MediaSnapshot(videos[0]) { Playlist = videos.ToList().ConvertAll(p => new MediaSnapshot(p)) };
            this.Get<AppViewModel>().OpenPlayerCommand.Execute(firstSnapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取精选视频时失败");
        }
    }
}
