// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters only for the live card item and the Windows presentation surface.
// LivePartitionDetailViewModel, PartitionViewModel and ILiveDiscoveryService are production code.
using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Items
{
    /// <summary>
    /// 直播卡片条目的无 WinUI 替身：详情视图模型只构造它并透出原始数据。
    /// </summary>
    public sealed class LiveItemViewModel(LiveInformation data, LiveCardStyle style)
    {
        public LiveInformation Data { get; } = data;

        public LiveCardStyle Style { get; } = style;
    }
}

namespace BiliCopilot.UI.Tests
{
    /// <summary>
    /// 记录调用并允许测试替换返回值的 <see cref="ILiveDiscoveryService"/> 替身.
    /// </summary>
    public sealed class LiveDiscoveryFake : ILiveDiscoveryService
    {
        public Func<LiveTag?, int, Task<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>> List { get; set; }
            = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([Room("1")], [Tag("t1")], 1));

        public List<(string? Tag, int PageNumber)> Calls { get; } = new();

        public Func<Task<IReadOnlyList<Partition>>>? Partitions { get; set; }

        public Task<(IReadOnlyList<LiveInformation>? Follows, IReadOnlyList<LiveInformation>? Recommend, int NextPageNumber)> GetFeedAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<LiveInformation>?, IReadOnlyList<LiveInformation>?, int)>((null, null, 0));

        public Task<IReadOnlyList<Partition>> GetLivePartitionsAsync(CancellationToken cancellationToken = default)
            => Partitions?.Invoke() ?? Task.FromResult<IReadOnlyList<Partition>>([]);

        public Task<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)> GetPartitionLiveListAsync(Partition partion, LiveTag? tag = default, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            Calls.Add((tag?.Id, pageNumber));
            return List(tag, pageNumber);
        }

        public static LiveTag Tag(string id) => new(id, id, "default");

        public static LiveInformation Room(string id) => new(new MediaIdentifier(id, id, null));
    }
}
