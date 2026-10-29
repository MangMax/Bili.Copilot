// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters only for OS/UI presentation, localization and local settings.
// Page commands, collections and IVideoDiscoveryService are the production code.
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Pages { public class PopularPage { } }
namespace FluentIcons.Common { public enum Symbol { Balloon, Fire, RibbonStar } }
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
namespace BiliCopilot.UI.Toolkits
{
    public static class PageTestServices
    {
        public static Microsoft.UI.Dispatching.DispatcherQueue Dispatcher { get; set; } = new();
        public static T Get<T>(this object owner) => (T)(object)Dispatcher;
    }
    public static class SettingsToolkit
    {
        public static string LastSection { get; set; } = "Recommend";
        public static bool ShowSearchRecommend { get; set; } = true;
        public static T ReadLocalSetting<T>(SettingNames name, T fallback) => name == SettingNames.ShowSearchRecommend ? (T)(object)ShowSearchRecommend : (T)(object)LastSection;
        public static void WriteLocalSetting<T>(SettingNames name, T value) => LastSection = value!.ToString()!;
    }
    public static class ResourceToolkit
    {
        public static string GetLocalizedString(StringNames name) => name.ToString();
    }
}
namespace BiliCopilot.UI.Models.Constants { public enum StringNames { Recommend, Hot, Rank, WholePartitions } }
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
        public RelayCommand<MediaSnapshot> OpenPlayerCommand { get; } = new(_ => { });
    }
}
