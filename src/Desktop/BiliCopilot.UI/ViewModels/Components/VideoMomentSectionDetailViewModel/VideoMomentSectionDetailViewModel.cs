// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 视频动态分区详情视图模型.
/// </summary>
public sealed partial class VideoMomentSectionDetailViewModel : ViewModelBase, IMomentSectionDetailViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMomentSectionDetailViewModel"/> class.
    /// </summary>
    public VideoMomentSectionDetailViewModel(
        IMomentDiscoveryService service)
    {
        _service = service;
        _logger = this.Get<ILogger<VideoMomentSectionDetailViewModel>>();
    }

    IRelayCommand IMomentSectionDetailViewModel.RefreshCommand => RefreshCommand;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Items.Count > 0 || _preventLoadMore)
        {
            return;
        }

        await LoadItemsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _offset = default;
        _baseline = default;
        IsEmpty = false;
        _preventLoadMore = false;
        _seenIds.Clear();
        _seenOffsets.Clear();
        Items.Clear();
        await LoadItemsAsync();
    }

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        if (_loadCancellation is not null || _preventLoadMore)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        try
        {
            IsLoading = true;
            var initialCount = Items.Count;
            do
            {
                var view = await _service.GetVideoMomentsAsync(_offset, _baseline, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                _baseline ??= view.UpdateBaseline;
                _preventLoadMore = view.HasMoreMoments != true
                    || string.IsNullOrEmpty(view.Offset) || !_seenOffsets.Add(view.Offset);
                _offset = view.Offset;
                foreach (var item in view.Moments)
                {
                    if (_seenIds.Add(item.Id))
                    {
                        Items.Add(new MomentItemViewModel(item, Models.Constants.MomentCardStyle.Video, ShowComment));
                    }
                }
            }
            while (!_preventLoadMore && Items.Count == initialCount);

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载动态列表失败.");
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsEmpty = Items.Count == 0;
                IsLoading = false;
            }
        }
    }

    private void ShowComment(MomentItemViewModel item)
        => this.Get<MomentPageViewModel>().ShowCommentCommand.Execute(item.Data);
}
