// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 番剧详情页的一个分区，例如正片、花絮、评价.
/// </summary>
public sealed class PgcDetailTabViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailTabViewModel"/> class.
    /// </summary>
    /// <param name="title">分区标题.</param>
    /// <param name="episodes">分区里的剧集；评价分区为空.</param>
    /// <param name="isIntro">是否是简介/评价分区.</param>
    public PgcDetailTabViewModel(string title, List<EpisodeItemViewModel> episodes, bool isIntro = false)
    {
        Title = title;
        Episodes = episodes;
        IsIntro = isIntro;
    }

    /// <summary>
    /// 分区标题.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 分区里的剧集.
    /// </summary>
    public List<EpisodeItemViewModel> Episodes { get; }

    /// <summary>
    /// 是否是简介/评价分区.
    /// </summary>
    public bool IsIntro { get; }

    /// <summary>
    /// 是否有剧集.
    /// </summary>
    public bool HasEpisodes => Episodes.Count > 0;
}
