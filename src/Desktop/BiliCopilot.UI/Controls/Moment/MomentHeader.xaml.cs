// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 动态头部.
/// </summary>
public sealed partial class MomentHeader : MomentHeaderBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MomentHeader"/> class.
    /// </summary>
    public MomentHeader() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        ViewModel.Initialized += OnViewModelInitialized;
        InitializeChildPartitions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.Initialized -= OnViewModelInitialized;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    private static string GetSectionName(MomentSectionType type)
        => type switch
        {
            MomentSectionType.Video => ResourceToolkit.GetLocalizedString(StringNames.Video),
            MomentSectionType.Comprehensive => ResourceToolkit.GetLocalizedString(StringNames.Comprehensive),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is IMomentSectionDetailViewModel item && item != ViewModel.SelectedSection)
        {
            ViewModel.SelectSectionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        var items = new List<SelectorBarItem>();
        if (ViewModel.Sections is not null)
        {
            foreach (var item in ViewModel.Sections)
            {
                items.Add(new SelectorBarItem()
                {
                    Text = GetSectionName(item.SectionType),
                    Tag = item,
                });
            }
        }

        Selector.SetItems(items, ViewModel.SelectedSection);
    }
}

/// <summary>
/// 动态头部基类.
/// </summary>
public abstract class MomentHeaderBase : LayoutUserControlBase<MomentPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MomentHeaderBase"/> class.
    /// </summary>
    protected MomentHeaderBase() => ViewModel = this.Get<MomentPageViewModel>();
}
