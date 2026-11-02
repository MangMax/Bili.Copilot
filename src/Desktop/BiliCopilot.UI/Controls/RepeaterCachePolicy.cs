// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.WinUIKernel.Share.Base.Repeater;

namespace BiliCopilot.UI.Controls;

/// <summary>
/// Sets the viewport cache on the ItemsRepeater owned by the packaged AppRepeater.
/// </summary>
public sealed partial class RepeaterCachePolicy : DependencyObject
{
    public static readonly DependencyProperty VerticalCacheLengthProperty =
        DependencyProperty.RegisterAttached(
            "VerticalCacheLength",
            typeof(double),
            typeof(RepeaterCachePolicy),
            new PropertyMetadata(20d, OnCacheLengthChanged));

    public static double GetVerticalCacheLength(AppRepeater element)
        => (double)element.GetValue(VerticalCacheLengthProperty);

    public static void SetVerticalCacheLength(AppRepeater element, double value)
        => element.SetValue(VerticalCacheLengthProperty, value);

    private static void OnCacheLengthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is AppRepeater repeater)
        {
            // Keep the instance-owned handler across unload/remount. No layout-event traversal.
            repeater.Loaded -= OnLoaded;
            repeater.Loaded += OnLoaded;
            Apply(repeater);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
        => Apply((AppRepeater)sender);

    private static void Apply(AppRepeater owner)
    {
        if (owner.FindName("Repeater") is ItemsRepeater repeater)
        {
            repeater.VerticalCacheLength = GetVerticalCacheLength(owner);
        }
    }
}
