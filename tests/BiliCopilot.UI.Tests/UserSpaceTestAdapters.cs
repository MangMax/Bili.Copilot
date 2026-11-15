// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters for the user-space target-ownership tests. The view models
// under test (UserSpacePageViewModel / UserMomentDetailViewModel) are linked
// production sources; only the WinUI-bound moment card, the pin shell and the
// toast surface are shimmed here. The toast severity enum lives with the
// AppViewModel adapter in PopularPageTestAdapters.cs.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.Items
{
    /// <summary>
    /// Moment card. Production construction touches WinRT types; the user-space
    /// view models only hand the payload back when comments are opened, so the
    /// source model and the comment callback are kept verbatim.
    /// </summary>
    public sealed class MomentItemViewModel(MomentInformation data, MomentCardStyle style, Action<MomentItemViewModel> showCommentAction)
    {
        /// <summary>动态源数据.</summary>
        public MomentInformation Data { get; } = data;

        /// <summary>卡片样式.</summary>
        public MomentCardStyle Style { get; } = style;

        /// <summary>内部内容；投稿的「动态」用它过滤掉视频类动态.</summary>
        public object? InnerContent { get; init; }

        /// <summary>触发评论回调.</summary>
        public void ShowComment() => showCommentAction?.Invoke(this);
    }

    /// <summary>
    /// Audio tile. The feed view model only constructs items; playback and
    /// navigation live on the Windows surface.
    /// </summary>
    public sealed class AudioItemViewModel(AudioInformation data)
    {
        /// <summary>音频信息.</summary>
        public AudioInformation Data { get; } = data;
    }

    /// <summary>
    /// Collection tile. The feed view model only constructs items.
    /// </summary>
    public sealed class CollectionItemViewModel(UserCollectionInfo data, string userId)
    {
        /// <summary>合集信息.</summary>
        public UserCollectionInfo Data { get; } = data;

        /// <summary>所属用户.</summary>
        public string UserId { get; } = userId;
    }
}

namespace BiliCopilot.UI.ViewModels.Core
{
    /// <summary>
    /// Pin shelf. Production owns file persistence and a token resolver; the
    /// user-space view model only adds a fixed item.
    /// </summary>
    public sealed class PinnerViewModel
    {
        public PinnerViewModel()
            => AddItemCommand = new AsyncRelayCommand<PinItem>(item =>
            {
                if (item is not null)
                {
                    Items.Add(item);
                }

                return Task.CompletedTask;
            });

        /// <summary>固定项.</summary>
        public List<PinItem> Items { get; } = new();

        /// <summary>添加固定项.</summary>
        public IAsyncRelayCommand<PinItem> AddItemCommand { get; }
    }
}
