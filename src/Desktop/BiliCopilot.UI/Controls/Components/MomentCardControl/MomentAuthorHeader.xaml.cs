// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media;

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
