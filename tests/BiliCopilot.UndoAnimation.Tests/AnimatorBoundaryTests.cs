using BiliCopilot.UI.Controls.Components;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Xunit;
namespace BiliCopilot.UndoAnimation.Tests;
// These tests assert lifecycle and clock release, not DWM pixels or WinUI animation timing.
public class AnimatorBoundaryTests
{
    private static FrameworkElement Button() => new() { Visibility = Visibility.Collapsed, RenderTransform = new CompositeTransform() };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenAncestorStopsAnExistingShowOrHide(bool hiding)
    {
        Storyboard.Begun.Clear();
        var parent = new FrameworkElement(); var button = Button(); button.Parent = parent;
        UndoRefreshButtonAnimator.Apply(button, true, true);
        if (hiding) { Storyboard.Begun[^1].Complete(); UndoRefreshButtonAnimator.Apply(button, false, true); }
        var old = Storyboard.Begun[^1];
        parent.Visibility = Visibility.Collapsed;
        UndoRefreshButtonAnimator.Apply(button, false, true);
        Assert.Equal(Visibility.Collapsed, button.Visibility);
        Assert.True(old.StopCount > 0);
        old.Complete();
        Assert.Equal(Visibility.Collapsed, button.Visibility);
        UndoRefreshButtonAnimator.Apply(button, true, false);
        Assert.Equal(1, button.Opacity);
    }
    [Fact]
    public void FallbackShowStopsTheCapturedHideClock()
    {
        Storyboard.Begun.Clear(); var button = Button();
        UndoRefreshButtonAnimator.Apply(button, true, false);
        UndoRefreshButtonAnimator.Apply(button, false, true, () => false);
        var old = Storyboard.Begun[^1]; old.Complete();
        Assert.Equal(1, old.StopCount);
        Assert.Equal(Visibility.Visible, button.Visibility);
        Assert.NotSame(old, Storyboard.Begun[^1]);
        Assert.Equal(0, Storyboard.Begun[^1].StopCount);
    }
    [Fact]
    public void PredicateReentryCannotOverwriteTheNewShow()
    {
        Storyboard.Begun.Clear(); var button = Button();
        UndoRefreshButtonAnimator.Apply(button, true, false);
        UndoRefreshButtonAnimator.Apply(button, false, true, () => { UndoRefreshButtonAnimator.Apply(button, true, true); return true; });
        var old = Storyboard.Begun[^1]; old.Complete();
        Assert.Equal(Visibility.Visible, button.Visibility);
        Assert.NotSame(old, Storyboard.Begun[^1]);
        Assert.Equal(0, Storyboard.Begun[^1].StopCount);
        Assert.True(old.StopCount > 0);
    }
}
