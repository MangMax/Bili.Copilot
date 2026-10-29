global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using BiliCopilot.UI.ViewModels.Components;
namespace BiliCopilot.UI {
    // Offline composition root only; production control/base/VM are not replaced.
    internal static class GlobalDependencies {
        public static SearchBoxViewModel Model = null!;
        public static T Get<T>(this object caller) where T:class => typeof(T)==typeof(SearchBoxViewModel)
            ? (T)(object)Model : typeof(T)==typeof(ViewModels.Core.NavigationViewModel)
            ? (T)(object)new ViewModels.Core.NavigationViewModel() : throw new NotSupportedException(typeof(T).Name);
    }
}
namespace BiliCopilot.UI.Models.Constants {
    public enum StringNames { Search, SearchTip, HotSearch, WholePartitions }
    public enum SettingNames { ShowSearchRecommend }
}
namespace BiliCopilot.UI.Toolkits {
    public static class ResourceToolkit { public static string GetLocalizedString(Models.Constants.StringNames name)=>name.ToString(); }
    public static class SettingsToolkit { public static bool Recommend=true; public static bool ReadLocalSetting(Models.Constants.SettingNames name,bool fallback)=>Recommend; }
}
namespace BiliCopilot.UI.ViewModels.Core {
    public class NavigationViewModel {
        public void Search(string keyword)=>Acceptance51.Trace.Write("navigation-adapter",new {route="global",keyword});
        public void SearchInRegion(string keyword)=>Acceptance51.Trace.Write("navigation-adapter",new {route="region",keyword});
    }
}
namespace Acceptance51 {
    public sealed partial class FixtureConverter:IValueConverter {
        public bool VisibilityMode {get;set;}
        public object Convert(object value,Type targetType,object parameter,string language)=>VisibilityMode ? (value==null?Visibility.Collapsed:Visibility.Visible) : value;
        public object ConvertBack(object value,Type targetType,object parameter,string language)=>throw new NotSupportedException();
    }
}
