using BiliCopilot.UI.Controls.LivePartition;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 子分区选择器的外部行为：没有可用子分区时收起，有标签时展开并同步选中。
/// </summary>
public sealed class LiveSubPartitionSelectorTests : IDisposable
{
    public LiveSubPartitionSelectorTests()
        => NavigationTestServices.Current.Value = new()
        {
            [typeof(ILogger<LivePartitionDetailViewModel>)] = NullLogger<LivePartitionDetailViewModel>.Instance,
        };

    public void Dispose() => NavigationTestServices.Current.Value = null;

    [Fact]
    public async Task NoSubPartitionsCollapsesSelectorWithoutDeliveringEmptyItemList()
    {
        var service = new LiveDiscoveryFake
        {
            List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([LiveDiscoveryFake.Room("1")], [], 0)),
        };
        var detail = Detail(service);
        var selector = new LiveSubPartitionSelector { ViewModel = detail };
        selector.LoadForTest();

        // 首次挂载、标签尚未返回：不能把「空列表 + 失效选中项」交给 SlidingTabBar，且选择器必须收起。
        Assert.Empty(selector.TabBar.Deliveries);
        Assert.Equal(Visibility.Collapsed, selector.TabBar.Visibility);

        await detail.InitializeCommand.ExecuteAsync(null);

        // 该分区没有标签：房间可以显示，但子分区选择器仍然没有可选项。
        Assert.Single(detail.Rooms);
        Assert.Equal(Visibility.Collapsed, selector.TabBar.Visibility);
        Assert.Empty(selector.TabBar.Deliveries);
    }

    [Fact]
    public async Task LoadedTagsExpandSelectorAndSelectTheCurrentTag()
    {
        var service = new LiveDiscoveryFake
        {
            List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([LiveDiscoveryFake.Room("1")], [LiveDiscoveryFake.Tag("t1"), LiveDiscoveryFake.Tag("t2")], 3)),
        };
        var detail = Detail(service);
        var selector = new LiveSubPartitionSelector { ViewModel = detail };
        selector.LoadForTest();

        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(Visibility.Visible, selector.TabBar.Visibility);
        var delivery = Assert.Single(selector.TabBar.Deliveries);
        Assert.Equal(new[] { "t1", "t2" }, delivery.Items.Select(item => ((LiveTag)item.Tag!).Id));
        Assert.Equal("t1", ((LiveTag)delivery.Selected!).Id);
    }

    [Fact]
    public async Task SelectingATabSwitchesTheChildPartition()
    {
        var service = new LiveDiscoveryFake
        {
            List = (tag, pageNumber) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                tag is null
                    ? ([LiveDiscoveryFake.Room("1")], [LiveDiscoveryFake.Tag("t1"), LiveDiscoveryFake.Tag("t2")], 1)
                    : (tag.Id == "t1" ? [LiveDiscoveryFake.Room("t1-room")] : [LiveDiscoveryFake.Room("t2-room")], null, pageNumber + 1)),
        };
        var detail = Detail(service);
        var selector = new LiveSubPartitionSelector { ViewModel = detail };
        selector.LoadForTest();
        await detail.InitializeCommand.ExecuteAsync(null);

        // 用户在标签栏里选中另一个子分区：选择器只把用户选择转发给详情视图模型.
        selector.TabBar.SelectedItem = detail.Children![1];
        selector.TabBar.RaiseSelectionChanged();
        PageTestServices.Dispatcher.Drain();

        Assert.Equal("t2", detail.CurrentTag?.Id);
        Assert.Equal("t2-room", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    private static LivePartitionDetailViewModel Detail(LiveDiscoveryFake service)
        => new(new PartitionViewModel(new Partition("9", "网游")), service);
}
