using BiliCopilot.UI.Controls.Components;
using Microsoft.UI.Xaml.Controls;

public sealed class SlidingTabBarTests
{
    [Fact]
    public void OverflowArrowsScrollWithoutChangingSelectionAndDisappearWhenItemsFit()
    {
        var tabs = CreateLaidOutTabs(out var first, out _);
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        tabs.ActualWidth = 228;
        tabs.TestTrack.ActualWidth = 400;
        tabs.TestScroller.ViewportWidth = 172;
        tabs.TestScroller.ScrollableWidth = 228;
        tabs.RaiseLayoutUpdated();
        Assert.Equal(Visibility.Visible, tabs.TestScrollRight.Visibility);
        Assert.False(tabs.TestScrollLeft.IsEnabled);
        Assert.True(tabs.TestScrollRight.IsEnabled);
        tabs.TestScroll(1);
        Assert.Equal(172, tabs.TestScroller.HorizontalOffset);
        tabs.TestScroll(1);
        Assert.Equal(228, tabs.TestScroller.HorizontalOffset);
        Assert.False(tabs.TestScrollRight.IsEnabled);
        tabs.TestSettings.AnimationsEnabled = false;
        tabs.TestScroll(-1);
        Assert.True(tabs.TestScroller.AnimationDisabled);
        tabs.TestScroll(-1);
        Assert.Equal(0, tabs.TestScroller.HorizontalOffset);
        Assert.Same(first.Tag, tabs.SelectedItem);
        Assert.Equal(0, changes);
        tabs.ActualWidth = 420;
        tabs.RaiseLayoutUpdated();
        Assert.Equal(Visibility.Collapsed, tabs.TestScrollLeft.Visibility);
        Assert.Equal(Visibility.Collapsed, tabs.TestScrollRight.Visibility);
    }

    [Fact]
    public void RapidSelectionRetargetsOneBackgroundAndResizeSettlesToNewBounds()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        tabs.TestSelector.SelectedItem = second;
        tabs.TestSelector.SelectedItem = first;
        tabs.TestSelector.SelectedItem = second;
        Assert.Equal(150, tabs.TestIndicator.Width);
        Assert.Equal(110, tabs.TestTranslation.X);
        Assert.Equal(2, tabs.TestIndicator.Animations.Count);
        Assert.Equal(2, tabs.TestTranslation.Animations.Count);
        Assert.Equal(150, tabs.TestIndicator.Animations.Single(a => a.Path == "Width").To);
        Assert.Equal(110, tabs.TestTranslation.Animations.Single(a => a.Path == "X").To);

        second.ActualWidth = 180;
        second.Left = 125;
        tabs.RaiseLayoutUpdated();
        Assert.Equal(180, tabs.TestIndicator.Width);
        Assert.Equal(125, tabs.TestTranslation.X);
        Assert.Empty(tabs.TestIndicator.Animations);
    }

    [Fact]
    public void RebuiltEmptyAndUnselectedItemsNeverRetainOldBackgroundOrEmitCommands()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        tabs.Select(second.Tag);
        var replacement = new SelectorBarItem { Tag = new object(), ActualWidth = 72, ActualHeight = 32, Left = 8 };
        tabs.SetItems([replacement], replacement.Tag);
        tabs.RaiseLayoutUpdated();
        Assert.Equal(8, tabs.TestTranslation.X);
        Assert.Equal(72, tabs.TestIndicator.Width);
        Assert.Empty(tabs.TestIndicator.Animations);
        tabs.Select(first.Tag); // A stale identity is not a valid selection in the rebuilt list.
        Assert.Null(tabs.SelectedItem);
        Assert.Equal(Visibility.Collapsed, tabs.TestIndicator.Visibility);
        tabs.SetItems([], null);
        Assert.Empty(tabs.TestSelector.Items);
        Assert.Equal(Visibility.Collapsed, tabs.TestIndicator.Visibility);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void UnloadStopsAnimationAndRemountSubscribesOnlyOnce()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        tabs.Select(second.Tag);
        tabs.RaiseUnloaded();
        Assert.Empty(tabs.TestIndicator.Animations);
        Assert.Empty(tabs.TestTranslation.Animations);
        Assert.Equal(Visibility.Collapsed, tabs.TestIndicator.Visibility);
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        tabs.TestSelector.SelectedItem = first;
        Assert.Equal(0, changes);
        tabs.RaiseLoaded();
        tabs.RaiseLoaded();
        Assert.Empty(tabs.TestIndicator.Animations);
        tabs.TestSelector.SelectedItem = second;
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DeferredUnloadAfterImmediateReinsertKeepsContentSynchronizedOnce()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        object? content = first.Tag;
        var changes = 0;
        tabs.SelectionChanged += (_, _) => { content = tabs.SelectedItem; changes++; };
        // Native remove + insert in one dispatch sends Loaded before the old Unloaded.
        tabs.RaiseLoaded();
        tabs.RaiseDeferredUnloaded();
        tabs.TestSelector.SelectedItem = second;
        Assert.Same(second.Tag, content);
        Assert.Equal(1, changes);
        Assert.Equal(Visibility.Visible, tabs.TestIndicator.Visibility);
        tabs.RaiseUnloaded();
        tabs.TestSelector.SelectedItem = first;
        Assert.Equal(1, changes);
        Assert.Equal(Visibility.Collapsed, tabs.TestIndicator.Visibility);
    }

    [Fact]
    public void DisabledMotionAndScaleChangesPositionImmediately()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        tabs.TestSettings.AnimationsEnabled = false;
        tabs.Select(second.Tag);
        Assert.Equal(110, tabs.TestTranslation.X);
        Assert.Empty(tabs.TestIndicator.Animations);
        tabs.TestSettings.AnimationsEnabled = true;
        tabs.Select(first.Tag);
        Assert.NotEmpty(tabs.TestIndicator.Animations);
        tabs.XamlRoot.RaiseChanged();
        Assert.Equal(0, tabs.TestTranslation.X);
        Assert.Empty(tabs.TestIndicator.Animations);
    }

    [Fact]
    public void UnmeasuredSelectionWaitsForLayoutBeforeShowingBackground()
    {
        var tabs = new SlidingTabBar();
        var item = new SelectorBarItem { Tag = new object() };
        tabs.SetItems([item], item.Tag);
        tabs.RaiseLoaded();
        Assert.Equal(Visibility.Collapsed, tabs.TestIndicator.Visibility);
        item.ActualWidth = 90;
        item.ActualHeight = 42;
        item.Left = 20;
        tabs.RaiseLayoutUpdated();
        Assert.Equal(Visibility.Visible, tabs.TestIndicator.Visibility);
        Assert.Equal(90, tabs.TestIndicator.Width);
        Assert.Equal(20, tabs.TestTranslation.X);
        Assert.Empty(tabs.TestIndicator.Animations);
    }

    [Fact]
    public void DisablingMotionSettlesCurrentAnimationAndIgnoresQueuedEventsFromPreviousMount()
    {
        var tabs = CreateLaidOutTabs(out var first, out var second);
        tabs.TestSelector.SelectedItem = second;
        Assert.NotEmpty(tabs.TestIndicator.Animations);
        tabs.TestSettings.AnimationsEnabled = false;
        tabs.TestSettings.RaiseAnimationsEnabledChanged();
        tabs.DispatcherQueue.Drain();
        Assert.Empty(tabs.TestIndicator.Animations);
        Assert.Equal(110, tabs.TestTranslation.X);

        tabs.TestSettings.RaiseAnimationsEnabledChanged();
        tabs.RaiseUnloaded();
        tabs.Select(first.Tag);
        tabs.RaiseLoaded();
        tabs.TestSettings.AnimationsEnabled = true;
        tabs.TestSelector.SelectedItem = second;
        tabs.DispatcherQueue.Drain();
        Assert.NotEmpty(tabs.TestIndicator.Animations);
    }

    private static SlidingTabBar CreateLaidOutTabs(out SelectorBarItem first, out SelectorBarItem second)
    {
        var tabs = new SlidingTabBar();
        first = new() { Tag = new object(), ActualWidth = 100, ActualHeight = 40 };
        second = new() { Tag = new object(), ActualWidth = 150, ActualHeight = 40, Left = 110 };
        tabs.SetItems([first, second], first.Tag);
        tabs.RaiseLoaded();
        tabs.RaiseLayoutUpdated();
        return tabs;
    }

    [Fact]
    public void FirstLayoutPositionsBackgroundAtActualSelectedBoundsWithoutAnimation()
    {
        var tabs = new SlidingTabBar();
        var item = new SelectorBarItem { Tag = new object(), ActualWidth = 116, ActualHeight = 38, Left = 74, Top = 4 };
        tabs.SetItems([item], item.Tag);
        tabs.RaiseLoaded();
        tabs.RaiseLayoutUpdated();

        Assert.Equal(Visibility.Visible, tabs.TestIndicator.Visibility);
        Assert.Equal(116, tabs.TestIndicator.Width);
        Assert.Equal(38, tabs.TestIndicator.Height);
        Assert.Equal(74, tabs.TestTranslation.X);
        Assert.Equal(4, tabs.TestTranslation.Y);
        Assert.Equal(1, item.BringIntoViewCount);
        Assert.Empty(tabs.TestIndicator.Animations);
    }

    [Fact]
    public void InitialSelectionAndModelChangesDoNotEchoUserCommands()
    {
        var tabs = new SlidingTabBar();
        var first = new SelectorBarItem { Text = "合集", Tag = new object() };
        var second = new SelectorBarItem { Text = "较长的推荐标题", Tag = new object() };
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        tabs.SetItems([first, second], second.Tag);
        tabs.RaiseLoaded();

        Assert.Same(second.Tag, tabs.SelectedItem);
        tabs.Select(first.Tag);
        Assert.Same(first.Tag, tabs.SelectedItem);
        Assert.Equal(0, changes);

        tabs.TestSelector.SelectedItem = second;
        Assert.Same(second.Tag, tabs.SelectedItem);
        Assert.Equal(1, changes);
    }
}
