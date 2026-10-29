// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Danmaku;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private DanmakuLoadSession? _danmakuLoadSession;
    private bool _isDanmakuClosed;
    private int _danmakuPlaybackVersion;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeDanmakuAsync()
    {
        _danmakuLoadSession?.Cancel();
        if (_isDanmakuClosed || Window is null || Window.IsClosed)
        {
            return;
        }

        SettingsToolkit.DeleteLocalSetting(Models.Constants.SettingNames.IsDanmakuEnabled);
        IsDanmakuEnabled = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.IsDanmakuEnabled, true);
        IsDanmakuControlVisible = IsDanmakuEnabled;
        if (!IsDanmakuEnabled)
        {
            IsDanmakuLoading = false;
            return;
        }

        try
        {
            string aid;
            string cid;
            double duration;
            if (_snapshot.Type == Models.Constants.BiliMediaType.Video)
            {
                var connector = (VideoConnectorViewModel)Connector;
                aid = connector._view.Information.Identifier.Id;
                cid = connector._part.Identifier.Id;
                duration = connector._view.Information.Duration ?? 0;
                Danmaku.Title = connector._view.Information.Identifier.Title;
            }
            else if (_snapshot.Type == Models.Constants.BiliMediaType.Pgc)
            {
                var connector = (PgcConnectorViewModel)Connector;
                aid = connector._episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Aid).ToString();
                cid = connector._episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Cid).ToString();
                duration = connector._episode.Duration ?? 0;
                Danmaku.Title = $"{connector.SeasonTitle} - {connector.EpisodeTitle}";
            }
            else
            {
                return;
            }

            // Snapshot identifiers before the first await; never read a new connector from old work.
            var segmentCount = Convert.ToInt32(Math.Ceiling(TimeSpan.FromSeconds(duration).TotalMinutes / 6.0));
            _danmakuLoadSession ??= new DanmakuLoadSession(
                this.Get<IDanmakuService>(),
                action => queue.TryEnqueue(() => action()),
                state =>
                {
                    if (_isDanmakuClosed || Window is null || Window.IsClosed)
                    {
                        return;
                    }

                    if (state.Error is not null)
                    {
                        logger.LogError(state.Error, "获取弹幕分片失败");
                        return;
                    }

                    IsDanmakuLoading = state.IsLoading;
                    if (state.Items is { } items)
                    {
                        Danmaku.DanmakuCountText = items.Count > 0
                            ? string.Format(ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.DanmakuCountTemplate), items.Count.ToString("N0"))
                            : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.DanmakuEmpty);
                        Danmaku.Initialize(items);
                    }
                });
            await _danmakuLoadSession.LoadAsync(aid, cid, segmentCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize danmaku.");
            IsDanmakuLoading = false;
            IsDanmakuControlVisible = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ReloadDanmakuAsync()
    {
        if (_isDanmakuClosed || Player is null || Window is null || Window.IsClosed || !Player.IsPlaybackInitialized || Danmaku is null)
        {
            return;
        }

        _danmakuLoadSession?.Cancel();
        Danmaku.ClearAll();
        await InitializeDanmakuAsync();
    }
}
