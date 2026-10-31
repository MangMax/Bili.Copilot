using CommunityToolkit.Mvvm.Input;
using Richasy.WinUIKernel.Share.ViewModels;
using BiliCopilot.UI.Models.Constants;
namespace BiliCopilot.UI.ViewModels.Items;
[WinRT.GeneratedBindableCustomProperty]
public partial class SeasonItemViewModel : ViewModelBase
{
    public SeasonCardStyle Style=>SeasonCardStyle.Index;
    public Uri Cover=>new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
    public string Title=>"Local poster fixture";
    public string Subtitle=>"Episode 12";
    public string Highlight=>"9.9";
    public bool? InWantWatch=>false;
    public bool? InWatching=>false;
    public bool? InWatched=>false;
    public RelayCommand PlayCommand{get;}=new(()=>{});
    public RelayCommand OpenInBroswerCommand{get;}=new(()=>{});
    public RelayCommand FollowCommand{get;}=new(()=>{});
    public RelayCommand PinCommand{get;}=new(()=>{});
    public RelayCommand MarkWantWatchCommand{get;}=new(()=>{});
    public RelayCommand MarkWatchingCommand{get;}=new(()=>{});
    public RelayCommand MarkWatchedCommand{get;}=new(()=>{});
    public RelayCommand UnfollowCommand{get;}=new(()=>{});
}
[WinRT.GeneratedBindableCustomProperty]
public partial class MomentItemViewModel : ViewModelBase
{
    public MomentCardStyle Style=>MomentCardStyle.Video;
    public Uri Avatar=>new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
    public Uri VideoCover=>Avatar;
    public string Author=>"Local dynamic author";
    public string Tip=>"Today";
    public Richasy.BiliKernel.Models.Appearance.EmoteText Description=>new("Dynamic body text with wrapping.",null);
    public bool IsLiked=>false;
    public double LikeCount=>12;
    public double CommentCount=>23;
    public RelayCommand ToggleLikeCommand{get;}=new(()=>{});
    public RelayCommand ShowCommentCommand{get;}=new(()=>{});
    public string VideoDuration=>"12:00";
    public string VideoTitle=>"Dynamic video title";
    public RelayCommand ActivateCommand{get;}=new(()=>{});
    public RelayCommand ShowUserSpaceCommand{get;}=new(()=>{});
    public RelayCommand PlayInPrivateCommand{get;}=new(()=>{});
    public RelayCommand AddToViewLaterCommand{get;}=new(()=>{});
    public RelayCommand OpenInBroswerCommand{get;}=new(()=>{});
    public RelayCommand CopyUrlCommand{get;}=new(()=>{});
    public RelayCommand PinCommand{get;}=new(()=>{});
}
[WinRT.GeneratedBindableCustomProperty]
public partial class NotifyMessageItemViewModel : ViewModelBase
{
    public Uri FirstUserAvatar=>null;
    public bool IsMultipleUsers=>false;
    public string FirstUserName=>"";
    public string PublishRelativeTime=>"Today";
    public string Subtitle=>"Ordinary text notification";
    public string Message=>"A local notification with enough text to wrap when the available width changes. This is the production notification template.";
    public string SourceContent=>"Original content";
    public RelayCommand ActiveCommand{get;}=new(()=>{});
}
