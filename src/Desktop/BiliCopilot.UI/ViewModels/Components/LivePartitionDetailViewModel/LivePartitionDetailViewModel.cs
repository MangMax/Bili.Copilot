// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 直播分区详情视图模型.
/// </summary>
public sealed partial class LivePartitionDetailViewModel : ViewModelBase<PartitionViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LivePartitionDetailViewModel"/> class.
    /// </summary>
    public LivePartitionDetailViewModel(
        PartitionViewModel partition,
        ILiveDiscoveryService service)
        : base(partition)
    {
        _service = service;
        _logger = this.Get<ILogger<LivePartitionDetailViewModel>>();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Children?.Count > 0)
        {
            return;
        }

        IsLiveLoading = true;
        try
        {
            var (lives, tags, nextPageNumber) = await _service.GetPartitionLiveListAsync(Data.Data);
            if (tags is { Count: > 0 })
            {
                Children = [.. tags];
                CurrentTag = Children[0];
                _childPartitionOffsetCache[CurrentTag] = nextPageNumber;
            }

            TryAddRooms(lives);
            IsLoadFailed = false;
        }
        catch (Exception ex)
        {
            IsLoadFailed = true;
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name} 分区视频时出错.");
        }

        IsLiveLoading = false;
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentTag is null)
        {
            // 没有可用的子分区（首次加载失败，或该分区没有标签）：重试分区列表，
            // 而不是在空标签上分页。
            Rooms.Clear();
            _preventLoadMore = false;
            await InitializeAsync();
            return;
        }

        _childPartitionRoomCache.Remove(CurrentTag);
        _childPartitionOffsetCache.Remove(CurrentTag);
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            Rooms.Clear();
            _preventLoadMore = false;
            await LoadRoomsAsync();
        });
    }

    [RelayCommand]
    private void ChangeChildPartition(LiveTag tag)
    {
        if (tag is null || tag.Equals(CurrentTag))
        {
            return;
        }

        _preventLoadMore = false;
        if (Rooms.Count > 0)
        {
            _childPartitionRoomCache[CurrentTag] = Rooms.ToList();
        }

        CurrentTag = tag;
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            Rooms.Clear();
            if (_childPartitionRoomCache.TryGetValue(CurrentTag, out var cache))
            {
                foreach (var item in cache)
                {
                    Rooms.Add(item);
                }
            }

            if (Rooms.Count == 0)
            {
                await LoadRoomsAsync();
            }
            else
            {
                LiveListUpdated?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    [RelayCommand]
    private async Task LoadRoomsAsync()
    {
        if (IsLiveLoading || CurrentTag is null)
        {
            return;
        }

        IsLiveLoading = true;
        await LoadChildPartitionRoomsAsync();
        IsLiveLoading = false;
        LiveListUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadChildPartitionRoomsAsync()
    {
        if (_preventLoadMore)
        {
            return;
        }

        try
        {
            var pn = 0;
            if (_childPartitionOffsetCache.TryGetValue(CurrentTag, out var cachePN))
            {
                pn = cachePN;
            }

            var (rooms, _, nextPn) = await _service.GetPartitionLiveListAsync(Data.Data, CurrentTag, pn);
            _childPartitionOffsetCache[CurrentTag] = nextPn;
            TryAddRooms(rooms);
        }
        catch (Exception ex)
        {
            _preventLoadMore = true;
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name}/{CurrentTag.Name} 分区视频时出错.");
        }
    }

    private void TryAddRooms(IReadOnlyList<LiveInformation> rooms)
    {
        if (rooms is not null)
        {
            foreach (var item in rooms)
            {
                Rooms.Add(new LiveItemViewModel(item, Models.Constants.LiveCardStyle.Partition));
            }
        }
    }
}
