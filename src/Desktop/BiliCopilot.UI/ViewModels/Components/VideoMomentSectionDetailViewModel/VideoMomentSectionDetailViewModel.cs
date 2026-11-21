// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 视频动态分区详情视图模型.
/// </summary>
public sealed partial class VideoMomentSectionDetailViewModel : ViewModelBase, IMomentSectionDetailViewModel
{
    private readonly Action<long, MomentView> _onFeedLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMomentSectionDetailViewModel"/> class.
    /// </summary>
    public VideoMomentSectionDetailViewModel(
        IMomentDiscoveryService service,
        Action<long, MomentView> onFeedLoaded)
    {
        _service = service;
        _onFeedLoaded = onFeedLoaded;
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
    private Task RefreshAsync()
        => RefreshWithResultAsync();

    internal Task<MomentView?> RefreshWithResultAsync()
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
        return LoadItemsCoreAsync();
    }

    [RelayCommand]
    private Task LoadItemsAsync()
        => LoadItemsCoreAsync();

    private async Task<MomentView?> LoadItemsCoreAsync()
    {
        if (_loadCancellation is not null || _preventLoadMore)
        {
            return null;
        }

        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        MomentView? firstView = null;
        try
        {
            IsLoading = true;
            var accountId = _offset is null ? this.Get<IBiliTokenResolver>().GetToken()?.UserId : null;
            var initialCount = Items.Count;
            do
            {
                var view = await _service.GetVideoMomentsAsync(_offset, _baseline, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (firstView is null || (firstView.Moments.Count == 0 && view.Moments.Count > 0))
                {
                    firstView = view;
                }

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

            if (accountId is { } id && firstView is not null)
            {
                _onFeedLoaded(id, firstView);
            }

            ListUpdated?.Invoke(this, EventArgs.Empty);
            return firstView;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载动态列表失败.");
            return null;
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
