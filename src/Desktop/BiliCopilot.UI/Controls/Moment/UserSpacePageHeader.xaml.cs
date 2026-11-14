// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.View;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 用户空间页面头部.
/// </summary>
public sealed partial class UserSpacePageHeader : UserSpacePageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePageHeader"/> class.
    /// </summary>
    public UserSpacePageHeader() => InitializeComponent();

    public static string GetFollowLabel(bool isFollowed)
        => ResourceToolkit.GetLocalizedString(isFollowed ? StringNames.Followed : StringNames.Follow);

    /// <summary>
    /// 页头头像元素，供串联动画作为目标.
    /// </summary>
    public RecommendationCoverImage AvatarElement => AvatarCover;

    private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e)
        => VisualStateManager.GoToState(this, e.NewSize.Width < 760 ? "Compact" : "Wide", false);

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

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is UserSpaceTabViewModel item && item != ViewModel.SelectedTab)
        {
            ViewModel.SelectTabCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        var items = new List<SelectorBarItem>();
        if (ViewModel.Tabs is { Count: > 0 } tabs)
        {
            foreach (var item in tabs)
            {
                items.Add(new SelectorBarItem()
                {
                    Text = item.Title,
                    Tag = item,
                });
            }
        }

        Selector.SetItems(items, ViewModel.SelectedTab);
    }
}
