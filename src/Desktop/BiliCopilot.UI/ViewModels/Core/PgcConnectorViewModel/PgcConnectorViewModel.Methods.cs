// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PgcConnectorViewModel
{
    private void InitializeView(PgcPlayerView view)
    {
        if (view is null)
        {
            return;
        }

        _view = view;
        _type = view.Information.GetExtensionIfNotNull<EntertainmentType>(SeasonExtensionDataId.PgcType);
        Cover = view.Information.Identifier.Cover.SourceUri;
        SeasonTitle = view.Information.Identifier.Title;
        Description = view.Information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Description);
        Alias = view.Information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Alias);
        if (string.IsNullOrEmpty(Alias))
        {
            Alias = view.Information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Subtitle);
        }

        SeasonId = view.Information.Identifier.Id;
        IsFollow = view.Information.IsTracking ?? false;
        PlayCount = view.Information.CommunityInformation.PlayCount ?? 0;
        DanmakuCount = view.Information.CommunityInformation.DanmakuCount ?? 0;
        CommentCount = view.Information.CommunityInformation.CommentCount ?? 0;
        LikeCount = view.Information.CommunityInformation.LikeCount ?? 0;
        CoinCount = view.Information.CommunityInformation.CoinCount ?? 0;
        FavoriteCount = view.Information.CommunityInformation.FavoriteCount ?? 0;
        FollowCount = view.Information.CommunityInformation.TrackCount ?? 0;
    }

    private void ClearView()
    {
        _view = default;
        Cover = default;
        SeasonTitle = default;
        IsFollow = false;
        PlayCount = 0;
        DanmakuCount = 0;
        CommentCount = 0;
        LikeCount = 0;
        CoinCount = 0;
        FavoriteCount = 0;
        FollowCount = 0;
        IsLiked = false;
        IsCoined = false;
        IsFavorited = false;
        IsCoinAlsoLike = true;
        FavoriteFolders = default;
        Sections?.Clear();
        SelectedSection = default;
    }

    private void InitializeSections()
    {
        if (Sections?.Count > 0)
        {
            return;
        }

        var sections = new List<IPlayerSectionDetailViewModel>
        {
            new PgcPlayerInfoSectionDetailViewModel(this),
        };

        if (_view.Episodes?.Count > 0 || _view.Extras?.Count > 0)
        {
            // 正片与附加分区（花絮、预告…）放进同一个选集控件，用下拉框切换.
            sections.Add(new PgcPlayerEpisodeSectionDetailViewModel(_view.Episodes, _view.Extras, _episode.Identifier.Id, ChangeEpisode));
        }

        if (_view.Seasons?.Count > 1)
        {
            sections.Add(new PgcPlayerSeasonSectionDetailViewModel(_view.Seasons, _view.Information.Identifier.Id, ChangeSeason));
        }

        sections.Add(_comments);

        Sections = sections;
        SelectSection(Sections[0]);
        SectionInitialized?.Invoke(this, EventArgs.Empty);
    }

    private EpisodeInformation? FindInitialEpisode(string? initialEpisodeId)
    {
        EpisodeInformation? playEpisode = default;
        if (!string.IsNullOrEmpty(initialEpisodeId))
        {
            playEpisode = FindEpisode(initialEpisodeId);
            if (playEpisode is null
                && _snapshot.Episode is { } requested
                && requested.Identifier.Id == initialEpisodeId)
            {
                // 花絮、预告片这类内容不在季度正片列表里（附加分区目前不随接口返回）。
                // 这时要按请求的 ep 号取播放地址，而不是退回第一集——那会播出完全不同的正片。
                return requested;
            }
        }

        if (playEpisode == null)
        {
            var historyEpisodeId = _view.Progress?.Cid;
            var autoLoadHistory = SettingsToolkit.ReadLocalSetting(SettingNames.AutoLoadHistory, true);
            if (!string.IsNullOrEmpty(historyEpisodeId) && autoLoadHistory)
            {
                playEpisode = _view.Episodes.FirstOrDefault(p => p.Identifier.Id == historyEpisodeId);
            }
        }

        return playEpisode ?? _view.Episodes.FirstOrDefault();
    }

    private EpisodeInformation? FindEpisode(string episodeId)
    {
        var episode = _view.Episodes?.FirstOrDefault(p => p.Identifier.Id == episodeId);

        // 花絮、预告片这类内容在季度详情里属于附加分区，不在正片列表里；
        // 只查正片会让它们退回到第一集（往往是最新一集），播放错误的内容。
        if (episode is null && _view.Extras is not null)
        {
            foreach (var extra in _view.Extras.Values)
            {
                episode = extra?.FirstOrDefault(p => p.Identifier.Id == episodeId);
                if (episode is not null)
                {
                    break;
                }
            }
        }

        return episode;
    }

    private EpisodeInformation? FindNextEpisode()
    {
        if (Sections is null)
        {
            return default;
        }

        if (Sections.OfType<PgcPlayerEpisodeSectionDetailViewModel>().FirstOrDefault() is PgcPlayerEpisodeSectionDetailViewModel episodeSection)
        {
            var index = episodeSection.Episodes.ConvertAll(p => p.Data).IndexOf(_episode);
            if (index < episodeSection.Episodes.Count - 1)
            {
                return episodeSection.Episodes[index + 1].Data;
            }
        }

        return default;
    }

    private EpisodeInformation? FindPreviousEpisode()
    {
        if (Sections is null)
        {
            return default;
        }

        if (Sections.OfType<PgcPlayerEpisodeSectionDetailViewModel>().FirstOrDefault() is PgcPlayerEpisodeSectionDetailViewModel episodeSection)
        {
            var index = episodeSection.Episodes.ConvertAll(p => p.Data).IndexOf(_episode);
            if (index > 0)
            {
                return episodeSection.Episodes[index - 1].Data;
            }
        }

        return default;
    }

    private void ChangeEpisode(EpisodeInformation episode)
    {
        if (episode.Identifier.Id == _episode.Identifier.Id)
        {
            return;
        }

        NewMediaRequest?.Invoke(this, new MediaSnapshot(default, episode));
    }

    private void ChangeSeason(SeasonItemViewModel season)
    {
        if (season.Data.Identifier.Id == _view.Information.Identifier.Id)
        {
            return;
        }

        NewMediaRequest?.Invoke(this, new MediaSnapshot(season.Data, default));
    }

    [RelayCommand]
    private void SelectSection(IPlayerSectionDetailViewModel section)
    {
        if (section is null || section == SelectedSection)
        {
            return;
        }

        SelectedSection = section;
        SelectedSection.TryFirstLoadCommand.Execute(default);
    }
}
