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
        protected abstract string GetPageKey();
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
        public static BiliCopilot.UI.Models.Constants.RecommendFeedSource RecommendSource { get; set; } = BiliCopilot.UI.Models.Constants.RecommendFeedSource.Mobile;
        public static bool ShowSearchRecommend { get; set; } = true;
        public static T ReadLocalSetting<T>(SettingNames name, T fallback) => NavigationTestServices.Settings.Value is { } settings ? (settings.TryGetValue(name, out var value) ? (T)value! : fallback) : name == SettingNames.IncrementalRecommendRefresh ? (T)(object)IncrementalRecommendRefresh : name == SettingNames.ShowSearchRecommend ? (T)(object)ShowSearchRecommend : name == SettingNames.RecommendFeedSource ? (T)(object)RecommendSource : (T)(object)LastSection;
        public static void WriteLocalSetting<T>(SettingNames name, T value)
        {
            if (NavigationTestServices.Settings.Value is { } settings) settings[name] = value;
            else LastSection = value!.ToString()!;
        }
    }
    public static class ResourceToolkit
    {
        public static string GetLocalizedString(StringNames name) => name.ToString();
    }
}
namespace BiliCopilot.UI.Models.Constants { public enum StringNames { Recommend, Hot, Rank, WholePartitions, RefreshingRecommendVideos, UserSpace, ViewHistory, PopularSlim, DynamicFeed, Video, Live, Anime, Cinema, Article, Message, Settings, LikeMessageMultipleDescription, LikeMessageSingleDescription, RecommendArticle, HotArticle, Comprehensive, Followed, Unfollowed, FailedToFollowUser, FailedToUnfollowUser, FailedToGetUserInformation, FailedToGetUserRelation, FailedToSearchUserVideos } }
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
    public sealed class VideoItemViewModel(VideoInformation data, VideoCardStyle style)
    {
        public VideoInformation Data { get; } = data;
        public VideoCardStyle Style { get; } = style;
    }
}
namespace BiliCopilot.UI.Models
{
    public sealed class MediaSnapshot(VideoInformation video)
    {
        public VideoInformation Video { get; } = video;
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
