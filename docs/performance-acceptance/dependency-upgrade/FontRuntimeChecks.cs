using BiliCopilot.UI.Controls.Core;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;

namespace BiliCopilot.UI;

// Runs after idle sampling, using the real TeachingTip Loaded handler/toolkit.
internal static class FontRuntimeChecks
{
    internal static async Task Run(PlayerViewModel player)
    {
        var window = player.Window ?? throw new InvalidOperationException("Font check requires a live player window");
        var root = window.XamlRoot?.Content ?? throw new InvalidOperationException("Player XamlRoot is absent");
        var before = player.Fonts.Count;
        player.IsSubtitleSettingsVisible = true;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        ComboBox? combo = null;
        while (DateTime.UtcNow < deadline)
        {
            var overlay = Find<PlayerOverlay>(root);
            combo = overlay?.FindName("SubtitleFontComboBox") as ComboBox;
            if (combo?.IsLoaded == true && combo.SelectedItem != null && player.Fonts.Count > 0) break;
            await Task.Delay(100);
        }
        var selected = combo?.SelectedItem?.GetType().GetProperty("LocalName")?.GetValue(combo.SelectedItem) as string;
        var result = new { before, count = player.Fonts.Count, loaded = combo?.IsLoaded == true,
            selected, expected = player.SubtitleFontFamily,
            status = before == 0 && player.Fonts.Count > 0 && combo?.IsLoaded == true && selected == player.SubtitleFontFamily ? "PASS" : "FAIL",
            hwnd = window.Handle.ToInt64() };
        await File.WriteAllTextAsync(Path.Combine(Offline48.Folder,"font-results.json"), JsonSerializer.Serialize(result));
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Find<T>(VisualTreeHelper.GetChild(root, i));
            if (child != null) return child;
        }
        return null;
    }
}
