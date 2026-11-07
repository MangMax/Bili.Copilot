// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters only for OS/UI presentation, localization and local settings.
// Page commands, collections and IVideoDiscoveryService are the production code.
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;


namespace Richasy.WinUIKernel.Share.Base
{
    /// <summary>
    /// Toast severity. Production lives in the Windows-only WinUIKernel package;
    /// the view models here only pass it through to <c>AppViewModel.ShowTipCommand</c>.
    /// </summary>
    public enum InfoType
    {
        /// <summary>Information.</summary>
        Information,

        /// <summary>Error.</summary>
        Error,

        /// <summary>Warning.</summary>
        Warning,

        /// <summary>Success.</summary>
        Success,

        /// <summary>Loading.</summary>
        Loading,
    }
}

namespace FluentIcons.Common { public enum Symbol { Balloon, Fire, RibbonStar, Rocket, DesignIdeas, VideoClip, VideoChat, Dust, FilmstripPlay, DocumentBulletList, Chat, Settings } }
namespace BiliCopilot.UI.ViewModels.View
{
    public abstract class LayoutPageViewModelBase : ObservableObject
    {
        public bool IsNavColumnManualHide { get; set; }

        public double NavColumnWidth { get; set; }

        protected abstract string GetPageKey();

        protected virtual void IsNavManualHideChanged(bool value) { }
    }
}
namespace Microsoft.UI.Dispatching
{
    public enum DispatcherQueuePriority { Low }
    public sealed class DispatcherQueue : SynchronizationContext
    {
        private readonly Queue<Action> _pending = new();
        public bool TryEnqueue(DispatcherQueuePriority priority, Action callback)
        {
            _pending.Enqueue(callback);
            return true;
        }

        public void Drain()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                while (_pending.TryDequeue(out var callback)) callback();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public override void Post(SendOrPostCallback callback, object? state) => _pending.Enqueue(() => callback(state));
    }
}
namespace BiliCopilot.UI
{
    /// <summary>
    /// Mirrors the production <c>BiliCopilot.UI.Get&lt;T&gt;</c> extension so linked
    /// view models resolve services through the adapter dictionary.
    /// </summary>
    public static class TestServiceLocator
    {
        public static T Get<T>(this object owner) => Toolkits.NavigationTestServices.Current.Value is { } services && services.TryGetValue(typeof(T), out var service) ? (T)service : (T)(object)Toolkits.PageTestServices.Dispatcher;
    }
}
namespace BiliCopilot.UI.Toolkits
{
    public static class PageTestServices
    {
        public static Microsoft.UI.Dispatching.DispatcherQueue Dispatcher { get; set; } = new();
    }
    public static class SettingsToolkit
    {
        public static string LastSection { get; set; } = "Recommend";
        public static bool IncrementalRecommendRefresh { get; set; }
        private static readonly AsyncLocal<int> _recommendSourceIndex = new();
        public static int RecommendSourceIndex
        {
            get => _recommendSourceIndex.Value;
            set => _recommendSourceIndex.Value = value;
        }
        public static bool ShowSearchRecommend { get; set; } = true;
        public static T ReadLocalSetting<T>(SettingNames name, T fallback) => NavigationTestServices.Settings.Value is { } settings ? (settings.TryGetValue(name, out var value) ? (T)value! : fallback) : name == SettingNames.IncrementalRecommendRefresh ? (T)(object)IncrementalRecommendRefresh : name == SettingNames.ShowSearchRecommend ? (T)(object)ShowSearchRecommend : name == SettingNames.RecommendFeedSource ? (T)(object)RecommendSourceIndex : (T)(object)LastSection;
        public static void WriteLocalSetting<T>(SettingNames name, T value)
        {
            if (NavigationTestServices.Settings.Value is { } settings) settings[name] = value;
            else if (name == SettingNames.RecommendFeedSource) RecommendSourceIndex = (int)(object)value!;
            else LastSection = value!.ToString()!;
        }
    }
    public static class ResourceToolkit
    {
        public static string GetLocalizedString(StringNames name) => name.ToString();
    }
}
namespace BiliCopilot.UI.Models.Constants { public enum StringNames { SearchVerificationRequired, SearchFailed, Recommend, Hot, Rank, WholePartitions, RefreshingRecommendVideos, UserSpace, ViewHistory, PopularSlim, DynamicFeed, Video, Live, Anime, Cinema, Article, Message, Settings, RecommendLive, RecommendLivePartition, LikeMessageMultipleDescription, LikeMessageSingleDescription, RecommendArticle, HotArticle, Comprehensive, User, Followed, Unfollowed, FailedToFollowUser, FailedToUnfollowUser, FailedToGetUserInformation, FailedToGetUserRelation, FailedToSearchUserVideos } }
namespace BiliCopilot.UI.ViewModels.Items
{
    public interface IPopularSectionItemViewModel { }
    public sealed class PopularSectionItemViewModel(FluentIcons.Common.Symbol icon, string title, PopularSectionType type) : IPopularSectionItemViewModel
    {
        public FluentIcons.Common.Symbol Icon { get; } = icon;
        public string Title { get; } = title;
        public PopularSectionType Type { get; } = type;
    }
    public sealed class PopularRankPartitionViewModel(Partition data) : IPopularSectionItemViewModel
    {
        public Partition Data { get; } = data;
    }
    public sealed class VideoItemViewModel(VideoInformation data, VideoCardStyle style, Action<VideoItemViewModel>? removeAction = null, Action<VideoItemViewModel, RecommendationFeedbackReason>? feedbackAccepted = null)
    {
        public VideoInformation Data { get; } = data;
        public Action<VideoItemViewModel>? RemoveAction { get; } = removeAction;
        public Action<VideoItemViewModel, RecommendationFeedbackReason>? FeedbackAccepted { get; } = feedbackAccepted;
#if SEARCH_RECOVERY_INVESTIGATION
        public RelayCommand PlayCommand { get; } = new(() => throw new NotSupportedException());
#endif
        public VideoCardStyle Style { get; } = style;
        public int? RankPosition { get; init; }
    }
}
namespace BiliCopilot.UI.Models
{
    public sealed class MediaSnapshot
    {
        public MediaSnapshot(VideoInformation video)
            => Video = video;

        public MediaSnapshot(LiveInformation live)
        {
            Live = live;
            Type = BiliCopilot.UI.Models.Constants.BiliMediaType.Live;
        }

        public VideoInformation? Video { get; }

        public LiveInformation? Live { get; }

        public BiliCopilot.UI.Models.Constants.BiliMediaType Type { get; set; }

        public EpisodeInformation? Episode { get; set; }
        public SeasonInformation? Season { get; set; }
        public int? PreferQuality { get; set; }
        public List<MediaSnapshot> Playlist { get; set; } = new();
    }
}
namespace BiliCopilot.UI.ViewModels.Core
{
    public sealed class AppViewModel
    {
        public AppViewModel()
            => ShowTipCommand = new AsyncRelayCommand<(string, Richasy.WinUIKernel.Share.Base.InfoType)>(data =>
            {
                Tips.Add((data.Item1, data.Item2));
                return Task.CompletedTask;
            });

        public List<BiliCopilot.UI.Forms.MainWindow> Windows { get; } = new();
        public RelayCommand<MediaSnapshot> OpenPlayerCommand { get; } = new(_ => { });
        public List<(string Message, Richasy.WinUIKernel.Share.Base.InfoType Type)> Tips { get; } = new();
        public IAsyncRelayCommand<(string, Richasy.WinUIKernel.Share.Base.InfoType)> ShowTipCommand { get; }
    }
}
