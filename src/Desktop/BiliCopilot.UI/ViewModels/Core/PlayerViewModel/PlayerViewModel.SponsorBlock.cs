// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型的空降助手部分.
/// </summary>
public sealed partial class PlayerViewModel
{
    private readonly SponsorBlockSkipTracker _sponsorBlockSkipTracker = new();
    private IReadOnlyList<SponsorBlockSegment> _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
    private CancellationTokenSource? _sponsorBlockLoadSource;
    private int _sponsorBlockGeneration;
    private DateTimeOffset _lastSponsorBlockCheckAt;
    private bool _isSponsorBlockSeeking;

    private bool IsSponsorBlockEnabled
        => SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);

    /// <summary>
    /// 清空空降助手状态并取消尚未完成的片段请求.
    /// </summary>
    private void ResetSponsorBlock()
    {
        _sponsorBlockGeneration++;
        var previousSource = _sponsorBlockLoadSource;
        _sponsorBlockLoadSource = null;
        if (previousSource is not null)
        {
            previousSource.Cancel();
            previousSource.Dispose();
        }

        _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
        _sponsorBlockSkipTracker.Reset();
        _lastSponsorBlockCheckAt = default;
        _isSponsorBlockSeeking = false;
    }

    /// <summary>
    /// 根据当前视频与分 P 异步加载片段，不阻塞播放初始化.
    /// </summary>
    private void StartSponsorBlockLoading()
    {
        ResetSponsorBlock();
        if (!IsSponsorBlockEnabled || Connector is not VideoConnectorViewModel videoConnector)
        {
            return;
        }

        var bvid = videoConnector.BvId;
        var cid = videoConnector._part?.Identifier.Id;
        if (string.IsNullOrWhiteSpace(bvid) || string.IsNullOrWhiteSpace(cid))
        {
            return;
        }

        var generation = _sponsorBlockGeneration;
        var source = new CancellationTokenSource();
        _sponsorBlockLoadSource = source;
        _ = LoadSponsorBlockSegmentsAsync(bvid, cid, generation, source.Token);
    }

    /// <summary>
    /// 检查当前进度是否需要跳过片段.
    /// </summary>
    /// <param name="position">当前播放位置（秒）.</param>
    private void CheckSponsorBlock(double position)
    {
        if (_sponsorBlockSegments.Count == 0
            || _isSponsorBlockSeeking
            || IsProgressChanging
            || Player?.IsPlaybackInitialized != true)
        {
            return;
        }

        // 进度事件很密集，先做低成本节流，再读取设置与匹配片段.
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSponsorBlockCheckAt < SponsorBlockSkipTracker.MinimumCheckInterval)
        {
            return;
        }

        _lastSponsorBlockCheckAt = now;

        if (!IsSponsorBlockEnabled)
        {
            return;
        }

        if (!_sponsorBlockSkipTracker.TryTakeSkip(
                _sponsorBlockSegments,
                position,
                Player.Duration,
                now,
                out var segment,
                out var targetPosition)
            || segment is null)
        {
            return;
        }

        _ = SkipSponsorSegmentAsync(segment, targetPosition, _sponsorBlockGeneration);
    }

    private async Task LoadSponsorBlockSegmentsAsync(string bvid, string cid, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var segments = await this.Get<SponsorBlockService>().GetSegmentsAsync(bvid, cid, cancellationToken);
            if (generation != _sponsorBlockGeneration)
            {
                return;
            }

            _sponsorBlockSegments = segments;
            logger.LogDebug("空降助手已加载 {Count} 个片段", segments.Count);
        }
        catch (Exception ex)
        {
            // 网络或解析失败都静默降级，不影响播放.
            logger.LogDebug(ex, "加载空降助手片段失败");
        }
    }

    private async Task SkipSponsorSegmentAsync(SponsorBlockSegment segment, double targetPosition, int generation)
    {
        if (Client is null || generation != _sponsorBlockGeneration)
        {
            return;
        }

        _isSponsorBlockSeeking = true;
        try
        {
            await Client.SetCurrentPositionAsync(targetPosition);

            var categoryName = SponsorBlockCategoryHelper.GetDisplayName(segment.Category);
            var message = string.Format(
                ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockSkippedTemplate),
                categoryName);
            Window?.ShowTip(message, InfoType.Information);
        }
        catch (Exception ex)
        {
            // 跳转失败时保持静默，不打断播放.
            logger.LogDebug(ex, "空降助手跳转失败");
        }
        finally
        {
            if (generation == _sponsorBlockGeneration)
            {
                _isSponsorBlockSeeking = false;
            }
        }
    }
}
