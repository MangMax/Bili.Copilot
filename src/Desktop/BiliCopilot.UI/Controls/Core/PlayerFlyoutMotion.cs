// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Core;

public enum PlayerFlyoutOrigin
{
    None,
    Bottom,
    Right,
}

/// <summary>
/// A presenter entrance, independent of popup visibility and placement ownership.
/// Styles select the origin and WinUI duration resources for each window mode.
/// </summary>
public sealed partial class PlayerFlyoutMotion : DependencyObject
{
    public static readonly DependencyProperty OriginProperty = DependencyProperty.RegisterAttached(
        "Origin", typeof(PlayerFlyoutOrigin), typeof(PlayerFlyoutMotion), new PropertyMetadata(PlayerFlyoutOrigin.None, OnOriginChanged));

    public static readonly DependencyProperty MoveDurationProperty = DependencyProperty.RegisterAttached(
        "MoveDuration", typeof(TimeSpan), typeof(PlayerFlyoutMotion), new PropertyMetadata(TimeSpan.FromMilliseconds(167)));

    public static readonly DependencyProperty FadeDurationProperty = DependencyProperty.RegisterAttached(
        "FadeDuration", typeof(TimeSpan), typeof(PlayerFlyoutMotion), new PropertyMetadata(TimeSpan.FromMilliseconds(83)));

    public static PlayerFlyoutOrigin GetOrigin(FrameworkElement element) => (PlayerFlyoutOrigin)element.GetValue(OriginProperty);

    public static void SetOrigin(FrameworkElement element, PlayerFlyoutOrigin value) => element.SetValue(OriginProperty, value);

    public static TimeSpan GetMoveDuration(FrameworkElement element) => (TimeSpan)element.GetValue(MoveDurationProperty);

    public static void SetMoveDuration(FrameworkElement element, TimeSpan value) => element.SetValue(MoveDurationProperty, value);

    public static TimeSpan GetFadeDuration(FrameworkElement element) => (TimeSpan)element.GetValue(FadeDurationProperty);

    public static void SetFadeDuration(FrameworkElement element, TimeSpan value) => element.SetValue(FadeDurationProperty, value);

    private static void OnOriginChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        if (GetOrigin(element) != PlayerFlyoutOrigin.None)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var element = (FrameworkElement)sender;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Translation");
        visual.StopAnimation("Opacity");
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        visual.Opacity = 1;
        if (!new UISettings().AnimationsEnabled)
        {
            return;
        }

        var compositor = visual.Compositor;
        // Fluent direct entrance: fast-out, slow-in (0,0,0,1).
        var ease = compositor.CreateCubicBezierEasingFunction(Vector2.Zero, new Vector2(0, 1));
        var move = compositor.CreateVector3KeyFrameAnimation();
        move.Duration = GetMoveDuration(element);
        move.InsertKeyFrame(0, GetOrigin(element) == PlayerFlyoutOrigin.Right ? new Vector3(12, 0, 0) : new Vector3(0, 12, 0));
        move.InsertKeyFrame(1, Vector3.Zero, ease);
        visual.StartAnimation("Translation", move);
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.Duration = GetFadeDuration(element);
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, compositor.CreateLinearEasingFunction());
        visual.StartAnimation("Opacity", fade);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        var visual = ElementCompositionPreview.GetElementVisual((FrameworkElement)sender);
        visual.StopAnimation("Translation");
        visual.StopAnimation("Opacity");
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        visual.Opacity = 1;
    }
}
