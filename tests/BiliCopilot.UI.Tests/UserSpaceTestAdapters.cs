// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters for the user-space target-ownership tests. The view models
// under test (UserSpacePageViewModel / UserMomentDetailViewModel) are linked
// production sources; only the WinUI-bound moment card, the pin shell and the
// toast surface are shimmed here. The toast severity enum lives with the
// AppViewModel adapter in PopularPageTestAdapters.cs.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.Moment;

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

        /// <summary>触发评论回调.</summary>
        public void ShowComment() => showCommentAction?.Invoke(this);
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
                Items.Add(item);
                return Task.CompletedTask;
            });

        /// <summary>固定项.</summary>
        public List<PinItem> Items { get; } = new();

        /// <summary>添加固定项.</summary>
        public IAsyncRelayCommand<PinItem> AddItemCommand { get; }
    }
}
