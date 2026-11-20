// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 综合动态分区详情视图模型.
/// </summary>
public sealed partial class ComprehensiveMomentSectionDetailViewModel : LayoutPageViewModelBase, IMomentSectionDetailViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComprehensiveMomentSectionDetailViewModel"/> class.
    /// </summary>
    public ComprehensiveMomentSectionDetailViewModel(
        IMomentDiscoveryService service)
    {
        _service = service;
        _logger = this.Get<ILogger<ComprehensiveMomentSectionDetailViewModel>>();
    }

    /// <inheritdoc/>
    protected override string GetPageKey()
        => "ComprehensiveMoment";

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Upers.Count == 0)
        {
            await RefreshAllAsync();
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (SelectedUper?.IsTotal != false)
        {
            RefreshAll();
        }
        else
        {
            SelectedUper.RefreshCommand.Execute(default);
        }
    }

    internal void RefreshAll()
        => _ = RefreshAllAsync();

    internal async Task<MomentView?> RefreshAllAsync()
    {
        if (IsLoading)
        {
            return null;
        }

        IsLoading = true;
        try
        {
            var response = await _service.GetComprehensiveMomentsAsync();
            var total = new MomentUperSectionViewModel(default);
            total.InjectFirstPageData(response);
            Upers.Clear();
            Upers.Add(total);
            if (response.Users is { } upers)
            {
                foreach (var item in upers)
                {
                    Upers.Add(new MomentUperSectionViewModel(item));
                }
            }

            SelectUper(total);
            Initialized?.Invoke(this, EventArgs.Empty);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试获取综合动态时发生错误.");
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectUper(MomentUperSectionViewModel item)
    {
        if (item == null)
        {
            return;
        }

        SelectedUper = item;
        SelectedUper.InitializeCommand.Execute(default);
    }
}
