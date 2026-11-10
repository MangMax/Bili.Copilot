// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播与弹幕时间轴的隔离（#192）：直播播放推进位置时不得驱动时间轴弹幕，
/// 否则会按视频时间轴去重放/跳过直播弹幕。
/// </summary>
public sealed class LiveTimelineIsolationTests : IDisposable
{
    public LiveTimelineIsolationTests() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();

    public void Dispose() => BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = null;

    [Fact]
    public void LivePositionChange_DoesNotAdvanceDanmakuTimeline()
    {
        var vm = new PlayerViewModel { IsLive = true };

        vm.Player.Position = 42;
        vm.Player.Notify(nameof(vm.Player.Position));

        Assert.Equal(0, vm.Danmaku.Position);
    }

    [Fact]
    public void RecordedPositionChange_StillAdvancesDanmakuTimeline()
    {
        var vm = new PlayerViewModel { IsLive = false };

        vm.Player.Position = 42;
        vm.Player.Notify(nameof(vm.Player.Position));

        Assert.Equal(42, vm.Danmaku.Position);
    }
}
