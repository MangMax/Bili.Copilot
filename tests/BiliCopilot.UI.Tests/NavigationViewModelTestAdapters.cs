global using Microsoft.UI.Xaml.Controls;

// OS/window/settings/DI and external navigation-item surfaces only.
// The VM, frame, factory, and SearchBoxViewModel implementations are linked production sources.
namespace Microsoft.UI.Xaml
{
    public enum FocusState { Programmatic }
}
namespace Microsoft.UI.Xaml.Controls
{
    public class Page : UIElement { }
    public sealed class NavigationView { public object? SelectedItem { get; set; } }
}
namespace BiliCopilot.UI.Controls
{
    public sealed partial class MainFrame
    {
        public int FocusCount { get; private set; }
        public bool Focus(FocusState state) { FocusCount++; return true; }
    }
}
namespace BiliCopilot.UI.Forms
{
    public sealed class MainWindow
    {
        public int ActivationCount { get; private set; }
        public void Activate() => ActivationCount++;
    }
}
namespace Richasy.WinUIKernel.Share.Toolkits
{
    public interface ISettingsToolkit
    {
        T ReadLocalSetting<T>(string key, T fallback);

        void WriteLocalSetting<T>(string key, T value);
    }
}
namespace Richasy.WinUIKernel.Share.ViewModels
{
    public interface INavServiceViewModel { void NavigateTo(Type pageType, object? parameter = null); }
    public sealed class AppNavigationItemViewModel(INavServiceViewModel navigation, Type pageType, string title, FluentIcons.Common.Symbol symbol, bool isSelected, bool isVisible)
    {
        public INavServiceViewModel Navigation { get; } = navigation;
        public string Title { get; } = title;
        public FluentIcons.Common.Symbol Symbol { get; } = symbol;
        public string PageKey { get; } = pageType.FullName!;
        public bool IsSelected { get; set; } = isSelected;
        public bool IsVisible { get; set; } = isVisible;
    }
}
namespace BiliCopilot.UI.Pages.Overlay
{
    public sealed class NavigationSearchTarget
    {
        public List<string> Searches { get; } = new();
        public CommunityToolkit.Mvvm.Input.RelayCommand<string> SearchCommand => new(value => Searches.Add(value!));
    }
}
namespace BiliCopilot.UI.Toolkits
{
    public static class NavigationTestServices
    {
        public static AsyncLocal<Dictionary<Type, object>?> Current { get; } = new();
        public static AsyncLocal<Dictionary<Models.Constants.SettingNames, object?>?> Settings { get; } = new();
    }
}
