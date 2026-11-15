// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 合集详情页视图模型.
/// </summary>
public sealed partial class CollectionDetailPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionDetailPageViewModel"/> class.
    /// </summary>
    /// <param name="userService">用户服务.</param>
    /// <param name="logger">日志.</param>
    public CollectionDetailPageViewModel(IUserService userService, ILogger<CollectionDetailPageViewModel> logger)
        => Feed = new CollectionVideoFeedViewModel(userService, logger);

    /// <summary>
    /// 载入指定合集.
    /// </summary>
    /// <param name="parameter">合集参数.</param>
    public void Initialize(CollectionDetailParameter parameter)
    {
        Title = parameter.Title;
        Cover = parameter.Cover;
        CountText = parameter.Count is > 0
            ? string.Format(
                ResourceToolkit.GetLocalizedString(StringNames.CollectionCountTemplate),
                AppToolkit.FormatCount(parameter.Count.Value))
            : string.Empty;
        Feed.ResetCommand.Execute(parameter);
        _ = Feed.LoadItemsCommand.ExecuteAsync(default);
    }
}
