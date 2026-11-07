using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// "直播时使用网页播放器"开关：开启时左键仍按旧行为在浏览器打开，关闭时进入应用内播放器。
/// </summary>
public sealed class LiveEntryPolicyTests
{
    [Fact]
    public void WebPlayerSettingOn_OpensBrowser()
        => Assert.Equal(LiveEntryTarget.Browser, LiveEntryPolicy.Decide(useWebPlayerWhenLive: true));

    [Fact]
    public void WebPlayerSettingOff_OpensNativePlayer()
        => Assert.Equal(LiveEntryTarget.NativePlayer, LiveEntryPolicy.Decide(useWebPlayerWhenLive: false));
}
