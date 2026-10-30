using BiliCopilot.UI.Controls.Danmaku;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BiliCopilot.UI;

internal static class FontProbe
{
    internal static int Calls;
    internal static double EnumerationMilliseconds;
}

internal static class ExtraRuntimeChecks
{
    internal static async Task Fonts()
    {
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        Require(FontProbe.Calls == 0, "Startup eagerly enumerated fonts");
        SettingsToolkit.WriteLocalSetting(SettingNames.DanmakuFontFamily, "Fixture Missing Family");
        SettingsToolkit.WriteLocalSetting(SettingNames.SubtitleFontFamily, "Fixture Missing Family");
        Offline48.Service.Reset(false);
        var player = await Offline48.Open();
        Window? host = null;
        try
        {
            Require(FontProbe.Calls == 0, "Opening player eagerly enumerated fonts");
            var options = new DanmakuDisplayOptions { ViewModel = player.Danmaku };
            host = new Window { Title = "Owned font picker acceptance", Content = new ScrollViewer { Content = options } };
            host.Activate();
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (player.Danmaku.Fonts.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(50);
            Require(player.Danmaku.Fonts.Count > 10, "Real font directory did not populate picker");
            await player.ReloadFontsCommand.ExecuteAsync(null);
            Require(FontProbe.Calls == 1, "Font pickers did not share real enumeration");
            Require(player.Danmaku.DanmakuFontFamily == "Fixture Missing Family", "Danmaku fallback rewrote saved family");
            Require(player.SubtitleFontFamily == "Fixture Missing Family", "Subtitle fallback rewrote saved family");
            Offline48.Log("font-baseline", new { FontProbe.Calls, FontProbe.EnumerationMilliseconds, danmakuFonts = player.Danmaku.Fonts.Count, subtitleFonts = player.Fonts.Count, boundary = "real toolkit enumeration; offline host startup, not authenticated application startup" });
        }
        finally { host?.Close(); await player.CloseAsync(); }
    }

    internal static async Task Modes()
    {
        Offline48.Service.Reset(false);
        var player = await Offline48.Open();
        player.Window.EnterFullScreen();
        player.Window.ExitFullScreen();
        player.Window.EnterCompactOverlay();
        player.Window.ExitCompactOverlay();
        await player.CloseAsync();
        await Task.Delay(700);
        if (!player.Window.IsDisposed) throw new Exception("Mode switching kept player window alive after close");
    }
}
