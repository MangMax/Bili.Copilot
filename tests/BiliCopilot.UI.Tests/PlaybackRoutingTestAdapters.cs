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
    /// <summary>
    /// 直播连接器的无 WinUI 替身：只验证关闭时会取消进行中的直播请求。
    /// </summary>
    public sealed class LiveConnectorViewModel
    {
        public CommunityToolkit.Mvvm.Input.IRelayCommand OpenPanelCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        public bool IsCancelPendingCalled { get; private set; }

        public void CancelPending() => IsCancelPendingCalled = true;

        internal void ApplyPlaybackInfo(BiliCopilot.UI.Resolvers.LiveMediaSourceResolver resolver)
        {
        }
    }

    public sealed partial class PlayerViewModel
    {
        private void StopLiveRecoveryMonitor() { }
        private readonly Microsoft.Extensions.Logging.ILogger<PlayerViewModel> logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlayerViewModel>.Instance;
        public string? ErrorMessage { get; set; }
        public string? ErrorActionText { get; set; }
        public bool HasErrorAction { get; set; }
        public bool IsEnd { get; set; }
        public bool IsExtraPanelVisible { get; set; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand BackCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        private void CheckBlackBackgroundVisible() { }
        private Task OpenLiveInBrowserAsync() => Task.CompletedTask;
        public bool IsConnecting { get; set; }

        /// <summary>测试替身：生产实现由 <c>LiveModePolicy</c> 从媒体快照推导。</summary>
        public bool IsLive { get; set; }

        public LiveConnectorViewModel? Connector { get; set; }
        private double? _continuePosition;
        internal void SetTerminalPauseTask(Task task) => _liveTerminalPauseDrained = task;
        internal void SetPositionQueueTask(Task task) => _positionChangesDrained = task;
        internal List<Exception> RefreshErrors { get; } = [];
        partial void ReportPlaybackRefreshFailure(Exception exception) => RefreshErrors.Add(exception);
        private BiliCopilot.UI.Resolvers.MediaSourceResolverBase? _sourceResolver;
        internal void SetRefreshResolver(BiliCopilot.UI.Resolvers.MediaSourceResolverBase resolver) => _sourceResolver = resolver;
    }
}

// MpvError 统一由 MpvMediaDisplayTestAdapters.cs 声明（含 LoadingFailed），此处不再重复定义。
