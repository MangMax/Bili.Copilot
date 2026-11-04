#if SEARCH_RECOVERY_INVESTIGATION
// #125: only OS lifecycle, localization, playback/cards and unselected live/user/article sections
// are adapted. Search submission, navigation, page command and video result logic are real.
namespace Richasy.WinUIKernel.Share.Base
{
    public abstract class LayoutPageBase<T> : Microsoft.UI.Xaml.Controls.Page
    {
        public T ViewModel { get; protected set; } = default!;
        public bool IsLoaded { get; private set; }
        public void Load() { IsLoaded = true; OnPageLoaded(); }
        public void Unload() { IsLoaded = false; OnPageUnloaded(); }
        protected virtual void OnPageLoaded() { }
        protected virtual void OnPageUnloaded() { }
    }
}
namespace BiliCopilot.UI.Pages.Overlay
{
    public sealed partial class SearchPage { private void InitializeComponent() { } }
}
namespace BiliCopilot.UI.Toolkits
{
    public static class IdToolkit { public static long Bv2Av(string value) => throw new NotSupportedException(); }
}
namespace BiliCopilot.UI.ViewModels.Items
{
    public sealed class SeasonItemViewModel(Richasy.BiliKernel.Models.Media.SeasonInformation data, BiliCopilot.UI.Models.Constants.SeasonCardStyle style)
    {
        public Richasy.BiliKernel.Models.Media.SeasonInformation Data { get; } = data;
        public BiliCopilot.UI.Models.Constants.SeasonCardStyle Style { get; } = style;
    }
}
namespace BiliCopilot.UI.ViewModels.Components
{
    public abstract class UnselectedSearchSection(Richasy.BiliKernel.Bili.Search.ISearchService service) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, BiliCopilot.UI.ViewModels.Items.ISearchSectionDetailViewModel
    {
        public BiliCopilot.UI.Models.Constants.SearchSectionType SectionType => default;
        public bool IsEmpty => true;
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand TryFirstLoadCommand { get; } = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => throw new NotSupportedException("Non-video partition outside investigation seam"));
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand RefreshCommand => TryFirstLoadCommand;
        public void Initialize(string keyword, Richasy.BiliKernel.Models.Search.SearchPartition partition) { }
        public void Clear() { _ = service; }
    }

    public sealed class LiveSearchSectionDetailViewModel(Richasy.BiliKernel.Bili.Search.ISearchService service) : UnselectedSearchSection(service) { }
    public sealed class UserSearchSectionDetailViewModel(Richasy.BiliKernel.Bili.Search.ISearchService service) : UnselectedSearchSection(service) { }
    public sealed class ArticleSearchSectionDetailViewModel(Richasy.BiliKernel.Bili.Search.ISearchService service) : UnselectedSearchSection(service) { }
}
#endif

