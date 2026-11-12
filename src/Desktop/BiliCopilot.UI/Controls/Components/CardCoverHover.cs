// Copyright (c) Bili Copilot. All rights reserved.

using System.Numerics;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// The shared cover hover motion. It mirrors the approved card design's
/// <c>transform .16s ease-out</c> image zoom.
/// </summary>
/// <remarks>
/// The motion runs on the composition visual of the cover host, so it stays
/// smooth while the UI thread is busy and retargets from the current scale
/// when the pointer leaves mid-flight. A XAML storyboard on a
/// <see cref="Microsoft.UI.Xaml.Media.CompositeTransform"/> cannot be used
/// here: the transform is not an animation root inside a templated, recycled
/// card, so the dependent animation is dropped and the cover never scales.
/// </remarks>
internal static class CardCoverHover
{
    private const float HoverScale = 1.025f;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(160);
    private static readonly UISettings UiSettings = new();

    /// <summary>
    /// Animates the cover host to its hovered or resting scale.
    /// </summary>
    /// <param name="host">The element that contains only the cover image.</param>
    /// <param name="hovered">Whether the card is hovered.</param>
    public static void Animate(FrameworkElement? host, bool hovered)
    {
        if (host is null || host.XamlRoot is null || host.ActualWidth <= 0 || host.ActualHeight <= 0)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(host);
        var target = hovered ? new Vector3(HoverScale, HoverScale, 1) : Vector3.One;
        // Composition scales around the visual's top-left unless a center is set.
        visual.CenterPoint = new Vector3((float)host.ActualWidth / 2, (float)host.ActualHeight / 2, 0);
        visual.StopAnimation("Scale");

        if (!UiSettings.AnimationsEnabled)
        {
            visual.Scale = target;
            return;
        }

        // Read the in-flight value first so reversing mid-animation continues
        // from where the cover currently is instead of jumping to rest.
        var from = visual.Scale;
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = Duration;
        animation.InsertKeyFrame(0f, from);
        // CSS ease-out: cubic-bezier(0, 0, .58, 1).
        animation.InsertKeyFrame(
            1f,
            target,
            visual.Compositor.CreateCubicBezierEasingFunction(Vector2.Zero, new Vector2(0.58f, 1f)));
        visual.StartAnimation("Scale", animation);
    }

    /// <summary>
    /// Returns the cover to its rest scale without animating.
    /// Used when a card leaves the visual tree so recycled cards never appear zoomed.
    /// </summary>
    /// <param name="host">The cover host to reset.</param>
    public static void Reset(FrameworkElement? host)
    {
        if (host is null)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(host);
        visual.StopAnimation("Scale");
        visual.Scale = Vector3.One;
    }
}
