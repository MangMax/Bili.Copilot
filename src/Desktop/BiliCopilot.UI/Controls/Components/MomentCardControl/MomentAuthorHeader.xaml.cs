// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Xaml.Media;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Controls.Components;

public sealed partial class MomentAuthorHeader : MomentCardPresenter
{
    public static readonly DependencyProperty ShowMoreButtonProperty = DependencyProperty.Register(
        nameof(ShowMoreButton), typeof(bool), typeof(MomentAuthorHeader), new PropertyMetadata(true));

    public bool ShowMoreButton
    {
        get => (bool)GetValue(ShowMoreButtonProperty);
        set => SetValue(ShowMoreButtonProperty, value);
    }

    public MomentAuthorHeader() => InitializeComponent();

    private void OnUserSpaceClick(object sender, RoutedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        // 番剧动态的“作者”是番剧账号，点进去应该看这个季度的详情，而不是用户空间.
        if (ViewModel.IsPgc && !string.IsNullOrEmpty(ViewModel.SeasonId))
        {
            // 头像飞到季度详情页的海报上；两张图不同，只按季度 Id 匹配.
            AvatarConnectedAnimation.PrepareForTarget(AvatarCover, ViewModel.SeasonId);
            this.Get<NavigationViewModel>().NavigateToOver(typeof(PgcDetailPage), ViewModel.SeasonId);
            return;
        }

        // 头像与作者名共用同一条动画路径；38px 头像由串联动画自行缩放。
        UserSpaceNavigation.Open(AvatarCover, ViewModel.UserSpaceProfile, ViewModel.ShowUserSpaceCommand);
    }

    private void OnMoreClick(object sender, RoutedEventArgs args)
    {
        DependencyObject? parent = this;
        while (parent is not null && parent is not MomentCardControl)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        (parent as MomentCardControl)?.ShowMore((FrameworkElement)sender);
    }
}
