// Only Windows/XAML and animation surfaces are adapted; both frame implementations are linked unchanged.
namespace Microsoft.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public class UIElement { public Visibility Visibility { get; set; } }
}
namespace CommunityToolkit.WinUI.Animations
{
    public sealed class OpacityAnimation
    {
        public TimeSpan Duration { get; set; }
        public double From { get; set; }
        public double To { get; set; }
    }
    public sealed class ImplicitAnimationSet : List<OpacityAnimation> { }
    public static class Implicit
    {
        public static void SetShowAnimations(UIElement element, ImplicitAnimationSet animations) { }
        public static void SetHideAnimations(UIElement element, ImplicitAnimationSet animations) { }
    }
}
namespace BiliCopilot.UI.Controls
{
    public sealed class NavigationTestChildren : List<UIElement>
    {
        public void Move(uint from, uint to)
        {
            var item = this[(int)from];
            RemoveAt((int)from);
            Insert((int)to, item);
        }
    }
    public sealed class NavigationTestGrid { public NavigationTestChildren Children { get; } = new(); }
    public sealed partial class MainFrame
    {
        private NavigationTestGrid RootGrid { get; } = new();
        private void InitializeComponent() { }
    }
    public sealed partial class OverlayFrame
    {
        private NavigationTestGrid RootGrid { get; } = new();
        private void InitializeComponent() { }
    }
}
namespace BiliCopilot.UI.Pages
{
    public class NavigationTestPage : UIElement, IParameterPage, IInitializePage, ViewModels.View.ICancelPageViewModel
    {
        public object? Parameter { get; private set; }
        public int InitializeCount { get; private set; }
        public int CancelCount { get; private set; }
        public void SetParameter(object? parameter) => Parameter = parameter;
        public void Initialize() => InitializeCount++;
        public void CancelLoading() => CancelCount++;
    }
    public sealed class PopularPage : NavigationTestPage { }
    public sealed class MomentPage : NavigationTestPage { }
    public sealed class VideoPartitionPage : NavigationTestPage { }
    public sealed class LivePartitionPage : NavigationTestPage { }
    public sealed class AnimePage : NavigationTestPage { }
    public sealed class CinemaPage : NavigationTestPage { }
    public sealed class ArticlePartitionPage : NavigationTestPage { }
    public sealed class MessagePage : NavigationTestPage { }
    public sealed class SettingsPage : NavigationTestPage { }
 }
namespace BiliCopilot.UI.Pages.Overlay
{
    public sealed class ArticleReaderPage : NavigationTestPage { }
    public sealed class FansPage : NavigationTestPage { }
    public sealed class FavoritesPage : NavigationTestPage { }
    public sealed class FollowsPage : NavigationTestPage { }
    public sealed class HistoryPage : NavigationTestPage { }
    public sealed class SearchPage : NavigationTestPage { }
    public sealed class UserSpacePage : NavigationTestPage { }
    public sealed class ViewLaterPage : NavigationTestPage { }
}
