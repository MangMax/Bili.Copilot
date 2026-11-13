// Copyright (c) Bili Copilot. All rights reserved.

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

        // 头像与作者名共用同一条动画路径；38px 头像由串联动画自行缩放。
        AvatarConnectedAnimation.Prepare(AvatarCover, ViewModel.SpaceUser);
        ViewModel.ShowUserSpaceCommand.Execute(default);
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
