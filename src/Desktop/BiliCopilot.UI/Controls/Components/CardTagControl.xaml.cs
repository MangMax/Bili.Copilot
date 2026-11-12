// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Shared category tag used by video and live cards.
/// </summary>
public sealed partial class CardTagControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CardTagControl), new PropertyMetadata(null, OnTextChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="CardTagControl"/> class.
    /// </summary>
    public CardTagControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the tag text.
    /// </summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((CardTagControl)sender).TagText.Text = args.NewValue as string ?? string.Empty;
}
