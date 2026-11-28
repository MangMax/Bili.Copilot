// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Converters;
using BiliCopilot.UI.ViewModels.Core;
using System.Globalization;
using Windows.UI;

namespace BiliCopilot.UI.Controls.Danmaku;

public sealed partial class DanmakuSendSettings : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(DanmakuSendViewModel), typeof(DanmakuSendSettings), new PropertyMetadata(null));

    public DanmakuSendSettings() => InitializeComponent();

    public DanmakuSendViewModel ViewModel
    {
        get => (DanmakuSendViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnSizeItemClick(object sender, RoutedEventArgs e)
        => ViewModel.IsStandardSize = (sender as FrameworkElement).Tag.ToString() == "Standard";

    private void OnColorItemClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Color = (Color)((Button)sender).DataContext;
        DanmakuColorInput.Text = ColorTextConverter.Format(ViewModel.Color);
    }

    private void OnColorInputChanged(object sender, TextChangedEventArgs e)
    {
        var text = DanmakuColorInput.Text.AsSpan();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length == 6 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            ViewModel.Color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
    }

    private void OnColorInputLostFocus(object sender, RoutedEventArgs e)
        => DanmakuColorInput.Text = ColorTextConverter.Format(ViewModel.Color);
}
