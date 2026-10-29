global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Windows.Foundation;
// Only app-global localization and the post-copy notification are substituted.
// Markdown parsing/rendering and Clipboard.SetContent are the linked production code.
namespace BiliCopilot.UI.Models.Constants { public enum StringNames { Copy, Copied } public enum InfoType { Success } }
namespace BiliCopilot.UI.Toolkits {
    public static class ResourceToolkit { public static string GetLocalizedString(Models.Constants.StringNames name) => name.ToString(); }
}
namespace BiliCopilot.UI.ViewModels { internal class NamespaceMarker {} }
namespace BiliCopilot.UI.ViewModels.Core {
    public class AppViewModel { public TipCommand ShowTipCommand { get; } = new(); }
    public class TipCommand { public void Execute(object value) { } }
}
namespace Richasy.WinUIKernel.Share.Base {
    public static class GlobalDependencies { public static OwnedKernel Kernel { get; } = new(); }
    public class OwnedKernel { public T GetRequiredService<T>() where T : new() => new(); }
}
