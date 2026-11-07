// Only OS/player output and account presentation surfaces are adapted here.
using Richasy.BiliKernel.Models.Media;

namespace Richasy.MpvKernel.Core.Models
{
    public sealed class MpvPlayOptions
    {
        public IntPtr WindowHandle { get; set; }
        public double? StartPosition { get; set; }
        public double InitialVolume { get; set; }
        public double InitialSpeed { get; set; }
        public string? UserAgent { get; set; }
        public Dictionary<string, string>? HttpHeaders { get; set; }
        public bool EnableCookies { get; set; }
        public string? MediaName { get; set; }
        public List<string>? AudioTracks { get; set; }
    }
}
namespace Richasy.MpvKernel.Player.Models
{
    public sealed record MpvMediaSource(string Url, string? Id, string? Title, Richasy.MpvKernel.Core.Models.MpvPlayOptions Options);
}
namespace Richasy.MpvKernel.Player
{
    public interface IMpvMediaSourceResolver
    {
        Task<Models.MpvMediaSource> GetSourceAsync();
        IMpvMediaSourceResolver Clone();
    }
}
namespace BiliCopilot.UI.Toolkits
{
    public static class AppToolkit
    {
        public static string GetPreferCodecId() => "avc";
    }
}
namespace BiliCopilot.UI.ViewModels.Components
{
    public sealed class AccountViewModel
    {
        public AccountProfile MyProfile { get; } = new();
    }
    public sealed class AccountProfile
    {
        public AccountUser User { get; } = new();
    }
    public sealed class AccountUser { public string Id => "viewer"; }
}
namespace BiliCopilot.UI.ViewModels.Items
{
    public sealed class SourceItemViewModel(PlayerFormatInformation format, object? unused)
    {
        public bool IsEnabled => !format.NeedVip;
    }
}
namespace BiliCopilot.UI.ViewModels.View
{
    public sealed partial class SettingsPageViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { }
}

namespace BiliCopilot.UI.ViewModels.Core
{
    public sealed partial class PlayerViewModel
    {
        public bool IsConnecting { get; set; }
        private double? _continuePosition;
        internal List<Exception> RefreshErrors { get; } = [];
        partial void ReportPlaybackRefreshFailure(Exception exception) => RefreshErrors.Add(exception);
        private BiliCopilot.UI.Resolvers.MediaSourceResolverBase? _sourceResolver;
        internal void SetRefreshResolver(BiliCopilot.UI.Resolvers.MediaSourceResolverBase resolver) => _sourceResolver = resolver;
    }
}

// MpvError 统一由 MpvMediaDisplayTestAdapters.cs 声明（含 LoadingFailed），此处不再重复定义。
