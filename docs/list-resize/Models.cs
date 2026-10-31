using CommunityToolkit.Mvvm.Input;
using Richasy.WinUIKernel.Share.ViewModels;
using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.ViewModels.Items
{
    [WinRT.GeneratedBindableCustomProperty]
    public partial class LiveItemViewModel : ViewModelBase
    {
        public LiveCardStyle Style => LiveCardStyle.Recommend;
        public Uri Cover => new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
        public string Title => "Live local fixture with a wrapping title";
        public string Author => "Local live author";
        public string Subtitle => "Live category";
        public double ViewerCount => 1234;
        public RelayCommand PlayCommand { get; } = new(()=>{});
        public RelayCommand ShowUserSpaceCommand { get; } = new(()=>{});
        public RelayCommand PinCommand { get; } = new(()=>{});
        public RelayCommand RemoveHistoryCommand { get; } = new(()=>{});
    }
    [WinRT.GeneratedBindableCustomProperty]
    public partial class ArticleItemViewModel : ViewModelBase
    {
        public ArticleCardStyle Style => ArticleCardStyle.Partition;
        public Uri Cover => new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
        public Uri Avatar => Cover;
        public string Title => "Article local fixture with a wrapping title";
        public string Author => "Local article author";
        public string PublishRelativeTime => "Today";
        public RelayCommand OpenReaderCommand { get; } = new(()=>{});
        public RelayCommand ShowUserSpaceCommand { get; } = new(()=>{});
        public RelayCommand OpenInBroswerCommand { get; } = new(()=>{});
        public RelayCommand PinCommand { get; } = new(()=>{});
        public RelayCommand RemoveHistoryCommand { get; } = new(()=>{});
    }
    [WinRT.GeneratedBindableCustomProperty]
    public partial class UserItemViewModel : ViewModelBase
    {
        public Uri Avatar => new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
        public string Name => "Local user";
        public string Description => "Avatar list with wrapping text";
        public string Id => "12345";
        public int Level => 0;
        public Visibility IsVip => Visibility.Collapsed;
        public bool IsFollowed => false;
        public RelayCommand ShowUserSpaceCommand { get; } = new(()=>{});
        public RelayCommand ToggleFollowCommand { get; } = new(()=>{});
        public RelayCommand PinCommand { get; } = new(()=>{});
    }
}
namespace Acceptance5053
{
    public sealed partial class LevelConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type type, object parameter, string language) => null;
        public object ConvertBack(object value, Type type, object parameter, string language) => throw new NotSupportedException();
    }
}
