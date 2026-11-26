// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Displays an indeterminate progress ring with a status message.
/// </summary>
public sealed partial class LoadingWidget : UserControl
{
    /// <summary>The status message dependency property.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(LoadingWidget), new PropertyMetadata(string.Empty));

    /// <summary>Initializes a new instance of the <see cref="LoadingWidget"/> class.</summary>
    public LoadingWidget() => InitializeComponent();

    /// <summary>Gets or sets the status message.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static bool IsVisible(Visibility visibility) => visibility == Visibility.Visible;
}
