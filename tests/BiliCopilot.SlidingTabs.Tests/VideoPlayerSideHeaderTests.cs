using BiliCopilot.UI.Controls.Player;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;

public sealed class VideoPlayerSideHeaderTests
{
    [Fact]
    public void HeaderSynchronizesContentOnceAndReattachesToCurrentModelOnly()
    {
        var first = new Section("合集");
        var second = new Section("评论");
        var model = new VideoConnectorViewModel { Sections = [first, second], SelectedSection = second };
        var header = new VideoPlayerSideHeader { ViewModel = model };
        header.Mount();
        Assert.Equal(new[] { "合集", "评论" }, header.TestTabs.TestSelector.Items.Select(item => item.Text));
        Assert.Same(second, header.TestTabs.SelectedItem);
        Assert.Equal(0, model.SelectSectionCommand.Calls);

        header.TestTabs.TestSelector.SelectedItem = header.TestTabs.TestSelector.Items[0];
        Assert.Same(first, model.SelectedSection);
        Assert.Equal(1, model.SelectSectionCommand.Calls);
        model.SelectedSection = second;
        Assert.Same(second, header.TestTabs.SelectedItem);
        Assert.Equal(1, model.SelectSectionCommand.Calls);

        header.Unmount();
        model.Sections = [second];
        header.Mount();
        header.Mount();
        Assert.Single(header.TestTabs.TestSelector.Items);
        model.Sections = [first, second];
        Assert.Equal(2, header.TestTabs.TestSelector.Items.Count);

        var replacement = new VideoConnectorViewModel { Sections = [second], SelectedSection = second };
        header.ViewModel = replacement;
        model.SelectedSection = first;
        Assert.Same(second, header.TestTabs.SelectedItem);
        replacement.Sections = null;
        replacement.SelectedSection = null;
        Assert.Empty(header.TestTabs.TestSelector.Items);
        Assert.Null(header.TestTabs.SelectedItem);
        Assert.Equal(0, replacement.SelectSectionCommand.Calls);
    }
}
