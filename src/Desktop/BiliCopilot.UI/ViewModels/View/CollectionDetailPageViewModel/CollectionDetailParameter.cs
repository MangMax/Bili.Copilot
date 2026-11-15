// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 合集详情页的导航参数.
/// </summary>
/// <param name="UserId">所属用户 Id.</param>
/// <param name="CollectionId">合集或系列 Id.</param>
/// <param name="Title">标题.</param>
/// <param name="Cover">封面.</param>
/// <param name="IsSeason">是否为合集.</param>
/// <param name="Count">视频数量.</param>
public sealed record CollectionDetailParameter(
    string UserId,
    string CollectionId,
    string Title,
    Uri? Cover,
    bool IsSeason,
    int? Count);
