// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Icon plus number used by every card media overlay. Callers own the icon
/// element so a video play glyph and a live viewer glyph share one layout.
/// </summary>
public sealed partial class CardStatControl : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(CardStatControl), new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CardStatControl), new PropertyMetadata(null));

    /// <summary>
    /// Initializes a new instance of the <see cref="CardStatControl"/> class.
    /// </summary>
    public CardStatControl() => InitializeComponent();

    /// <summary>
    /// Gets or sets the icon element rendered before the text.
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the text rendered after the icon.
    /// </summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
