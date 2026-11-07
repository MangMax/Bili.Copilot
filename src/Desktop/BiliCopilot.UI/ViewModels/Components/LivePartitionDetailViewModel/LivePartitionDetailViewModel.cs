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

    /// <summary>
    /// 没有可用子分区时重新加载分区列表；页面复用失败的详情视图模型时用它重试.
    /// </summary>
    /// <returns>是否发起了重试.</returns>
    public bool EnsureInitializedOrRetry()
    {
        if (!HasNoChildPartitions)
        {
            return false;
        }

        InitializeCommand.Execute(default);
        return true;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsLiveLoading || Children?.Count > 0)
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

            TryAddRooms(lives, CurrentTag);
            IsLoadFailed = false;
        }
        catch (Exception ex)
        {
            // 失败收尾：Children、CurrentTag、Rooms 一起回到「没有可用子分区」的状态，
            // 选择器据此收起，而不是拿到空列表和失效选中项。
            IsLoadFailed = true;
            Children = null;
            CurrentTag = null;
            ClearRooms();
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name} 分区视频时出错.");
        }

        IsLiveLoading = false;
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (HasNoChildPartitions)
        {
            // 没有可用的子分区（首次加载失败，或该分区没有标签）：重试分区列表，
            // 而不是在空标签上分页。
            ClearRooms();
            await InitializeAsync();
            return;
        }

        _childPartitionRoomCache.Remove(CurrentTag);
        _childPartitionOffsetCache.Remove(CurrentTag);
        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            ClearRooms();
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
        if (CurrentTag is not { } target)
        {
            return;
        }

        _pendingRoomTag = target;
        if (_roomLoadInProgress)
        {
            // 已有请求在飞行：它结束后会为最新选择再发起一次，
            // 保证切换标签后一定有一次对应请求，而不是静默丢掉最后的选择。
            return;
        }

        _roomLoadInProgress = true;
        IsLiveLoading = true;
        try
        {
            while (_pendingRoomTag is { } tag)
            {
                _pendingRoomTag = null;
                await LoadChildPartitionRoomsAsync(tag);
            }
        }
        finally
        {
            _roomLoadInProgress = false;
            IsLiveLoading = false;
        }

        LiveListUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadChildPartitionRoomsAsync(LiveTag target)
    {
        if (_preventLoadMore)
        {
            return;
        }

        try
        {
            var pn = _childPartitionOffsetCache.TryGetValue(target, out var cachePN) ? cachePN : 0;
            var (rooms, _, nextPn) = await _service.GetPartitionLiveListAsync(Data.Data, target, pn);
            if (!Equals(target, CurrentTag))
            {
                // 旧标签的响应不得写入已切换的视图。
                return;
            }

            _childPartitionOffsetCache[target] = nextPn;
            TryAddRooms(rooms, target);
        }
        catch (Exception ex)
        {
            if (Equals(target, CurrentTag))
            {
                // 失败收尾：当前标签的可见列表不能停留在半加载的旧内容。
                ClearRooms();
                _preventLoadMore = true;
            }

            _logger.LogError(ex, $"尝试加载 {Data.Data.Name}/{target.Name} 分区视频时出错.");
        }
    }

    private void TryAddRooms(IReadOnlyList<LiveInformation> rooms, LiveTag? target)
    {
        if (rooms is null || !Equals(target, CurrentTag))
        {
            return;
        }

        foreach (var item in rooms)
        {
            Rooms.Add(new LiveItemViewModel(item, Models.Constants.LiveCardStyle.Partition));
        }
    }

    private void ClearRooms()
    {
        Rooms.Clear();
        _preventLoadMore = false;
    }
}
