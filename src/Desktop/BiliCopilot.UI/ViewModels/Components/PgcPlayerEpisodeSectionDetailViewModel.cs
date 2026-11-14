// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// PGC 播放器分集详情视图模型.
/// </summary>
public sealed partial class PgcPlayerEpisodeSectionDetailViewModel : ViewModelBase, IPlayerSectionDetailViewModel
{
    private readonly Action<EpisodeInformation> _episodeSelectedAction;
    private string _currentEpisodeId;

    [ObservableProperty]
    public partial List<EpisodeItemViewModel>? Episodes { get; set; }

    [ObservableProperty]
    public partial bool OnlyIndex { get; set; }

    /// <summary>
    /// 初始化 <see cref="PgcPlayerEpisodeSectionDetailViewModel"/> 类的新实例.
    /// </summary>
    /// <param name="episodes">正片剧集.</param>
    /// <param name="extras">附加分区，例如花絮、预告片.</param>
    /// <param name="epid">当前播放的剧集 Id.</param>
    /// <param name="action">选择剧集后的回调.</param>
    public PgcPlayerEpisodeSectionDetailViewModel(
        IList<EpisodeInformation>? episodes,
        Dictionary<string, IList<EpisodeInformation>>? extras,
        string epid,
        Action<EpisodeInformation> action)
    {
        _episodeSelectedAction = action;
        _currentEpisodeId = epid;

        var groups = new List<PgcPlayerEpisodeGroupViewModel>();
        if (episodes is { Count: > 0 })
        {
            var (mainEpisodes, mainSupportsIndex) = BuildEpisodes(episodes);
            groups.Add(new PgcPlayerEpisodeGroupViewModel(
                ResourceToolkit.GetLocalizedString(StringNames.MainEpisode),
                mainEpisodes,
                isMainGroup: true,
                supportsIndex: mainSupportsIndex));
        }

        if (extras is not null)
        {
            foreach (var extra in extras)
            {
                if (extra.Value is { Count: > 0 })
                {
                    // 花絮、预告等分区直接沿用接口给的标题.
                    var (extraEpisodes, extraSupportsIndex) = BuildEpisodes(extra.Value);
                    groups.Add(new PgcPlayerEpisodeGroupViewModel(extra.Key, extraEpisodes, supportsIndex: extraSupportsIndex));
                }
            }
        }

        Groups = groups;
        HasMultipleGroups = groups.Count > 1;
        SelectedGroup = groups.FirstOrDefault(p => p.Episodes.Any(item => item.Data.Identifier.Id == epid))
            ?? groups.FirstOrDefault();
        OnlyIndex = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.IsPgcPlayerPartsOnlyIndex, false);
    }

    /// <summary>
    /// 分集分组：正片与各个附加分区.
    /// </summary>
    public List<PgcPlayerEpisodeGroupViewModel> Groups { get; }

    /// <summary>
    /// 当前分组.
    /// </summary>
    [ObservableProperty]
    public partial PgcPlayerEpisodeGroupViewModel? SelectedGroup { get; set; }

    /// <summary>
    /// 是否有多个分组.
    /// </summary>
    [ObservableProperty]
    public partial bool HasMultipleGroups { get; set; }

    /// <summary>
    /// 是否用「仅显示序号」排版；只有正片适合.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowIndexLayout { get; set; }

    /// <summary>
    /// 是否显示「仅显示序号」开关.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowIndexToggle { get; set; }

    /// <inheritdoc/>
    public string Title { get; } = ResourceToolkit.GetLocalizedString(StringNames.Episodes);

    [RelayCommand]
    private static Task TryFirstLoadAsync()
        => Task.CompletedTask;

    private (List<EpisodeItemViewModel> Episodes, bool SupportsIndex) BuildEpisodes(IList<EpisodeInformation> episodes)
    {
        var list = episodes
            .Select(p => new EpisodeItemViewModel(p, EpisodeCardStyle.Player, PlayEpisode))
            .ToList();

        // 接口没有单独的 index 字段：正片和部分附加分区（如 2020版）用纯数字标题当序号，
        // 花絮、预告、UP论道等的标题是描述文字，解析不出序号.
        var supportsIndex = list.All(p => p.Index is not null);
        for (var i = 0; i < list.Count; i++)
        {
            list[i].Index ??= i + 1;
            list[i].IsSelected = list[i].Data.Identifier.Id == _currentEpisodeId;
        }

        return (list, supportsIndex);
    }

    partial void OnSelectedGroupChanged(PgcPlayerEpisodeGroupViewModel? value)
    {
        Episodes = value?.Episodes;
        UpdateIndexAvailability();
        HighlightCurrentEpisode();
    }

    private void UpdateIndexAvailability()
    {
        // 只有「每一集都有真实序号」的分组才适合只显示序号（正片、2020版这类），
        // 花絮、预告等标题是描述文字，没有序号，只能按卡片展示.
        var supportsIndex = SelectedGroup?.SupportsIndex ?? false;
        ShowIndexToggle = supportsIndex;
        ShowIndexLayout = OnlyIndex && supportsIndex;
    }

    private void HighlightCurrentEpisode()
    {
        foreach (var item in Groups.SelectMany(p => p.Episodes))
        {
            item.IsSelected = item.Data.Identifier.Id == _currentEpisodeId;
        }
    }

    private void PlayEpisode(EpisodeItemViewModel episode)
    {
        _currentEpisodeId = episode.Data.Identifier.Id;
        HighlightCurrentEpisode();
        _episodeSelectedAction?.Invoke(episode.Data);
    }

    partial void OnOnlyIndexChanged(bool value)
    {
        SettingsToolkit.WriteLocalSetting(SettingNames.IsPgcPlayerPartsOnlyIndex, value);
        UpdateIndexAvailability();
    }
}

/// <summary>
/// 播放器里的一组剧集，比如正片或花絮.
/// </summary>
public sealed class PgcPlayerEpisodeGroupViewModel(
    string title,
    List<EpisodeItemViewModel> episodes,
    bool isMainGroup = false,
    bool supportsIndex = false)
{
    /// <summary>
    /// 是否是正片分组.
    /// </summary>
    public bool IsMainGroup { get; } = isMainGroup;

    /// <summary>
    /// 是否每一集都有真实序号，可以只显示序号.
    /// </summary>
    public bool SupportsIndex { get; } = supportsIndex;

    /// <summary>
    /// 分组标题.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// 分组里的剧集.
    /// </summary>
    public List<EpisodeItemViewModel> Episodes { get; } = episodes;
}
