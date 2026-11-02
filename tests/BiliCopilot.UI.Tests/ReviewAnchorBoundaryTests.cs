using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml;
namespace BiliCopilot.UI.Tests;
public sealed class ReviewAnchorBoundaryTests
{
    [Fact]
    public void CancelBetweenChangingAndChangedMustNotResetNewPage()
    {
        var control = new IncrementVerticalScrollControl();
        var root = new FrameworkElement();
        var anchor = new RecommendationScrollAnchor(root, control);
        control.View.VerticalOffset = 600;
        anchor.Capture(RecommendationListChange.ResetToTop);
        anchor.Cancel();
        control.View.VerticalOffset = 400;
        anchor.Restore(null, EventArgs.Empty);
        control.View.DispatcherQueue.Drain();
        Assert.Equal(400, control.View.VerticalOffset);
    }
    [Fact]
    public void CancelDuringLayoutMustNotResetNewPage()
    {
        var control = new IncrementVerticalScrollControl();
        var root = new FrameworkElement();
        var anchor = new RecommendationScrollAnchor(root, control);
        control.View.VerticalOffset = 600;
        anchor.Capture(RecommendationListChange.ResetToTop);
        anchor.Restore(null, EventArgs.Empty);
        root.LayoutPass = () => { anchor.Cancel(); control.View.VerticalOffset = 400; };
        control.View.DispatcherQueue.Drain();
        Assert.Equal(400, control.View.VerticalOffset);
    }
    [Fact]
    public void UnloadedRootMustIgnoreQueuedReset()
    {
        var control = new IncrementVerticalScrollControl();
        var root = new FrameworkElement();
        var anchor = new RecommendationScrollAnchor(root, control);
        control.View.VerticalOffset = 600;
        anchor.Capture(RecommendationListChange.ResetToTop);
        anchor.Restore(null, EventArgs.Empty);
        root.IsLoaded = false;
        control.View.DispatcherQueue.Drain();
        Assert.Equal(600, control.View.VerticalOffset);
    }
}
