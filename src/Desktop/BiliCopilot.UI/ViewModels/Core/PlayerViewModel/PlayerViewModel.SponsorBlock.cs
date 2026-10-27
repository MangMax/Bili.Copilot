// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型的空降助手部分.
/// </summary>
public sealed partial class PlayerViewModel
{
    private readonly SponsorBlockController _sponsorBlockController = new(SponsorBlockBehaviorSettings.Read);
    private IReadOnlyList<SponsorBlockSegment> _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
    private CancellationTokenSource? _sponsorBlockLoadSource;
    private int _sponsorBlockGeneration;
    private bool _isSponsorBlockSeeking;

    /// <summary>
    /// 当前分 P 的空降助手片段列表，供进度条标记等界面复用.
    /// </summary>
    public IReadOnlyList<SponsorBlockSegment> SponsorBlockSegments => _sponsorBlockSegments;

    /// <summary>
    /// 当前播放位置所在的空降助手片段.
    /// </summary>
    public SponsorBlockSegment? ActiveSponsorBlockSegment => _sponsorBlockController.ActiveSegment;

    private bool IsSponsorBlockEnabled
        => SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);

    /// <summary>
    /// 点击跳过提示：跳到片段终点.
    /// </summary>
    [RelayCommand]
    private async Task AcceptSponsorBlockPromptAsync()
    {
        var decision = _sponsorBlockController.AcceptActivePrompt(Player?.Duration ?? 0);
        if (decision.IsActionable)
        {
            HideSponsorBlockTip();
            await ApplySponsorBlockDecisionAsync(decision, _sponsorBlockGeneration);
        }
    }

    /// <summary>
    /// 关闭跳过提示.
    /// </summary>
    [RelayCommand]
    private void DismissSponsorBlockPrompt()
    {
        var decision = _sponsorBlockController.DismissActivePrompt();
        if (decision.Kind == SponsorBlockActionKind.HideButton)
        {
            HideSponsorBlockTip();
        }
    }

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
        var resetDecision = _sponsorBlockController.Reset();
        HideSponsorBlockTip();
        _isSponsorBlockSeeking = false;

        if (resetDecision is { Kind: SponsorBlockActionKind.RestoreMute, MuteState: bool restoreState })
        {
            _ = ApplySponsorBlockMuteAsync(restoreState);
        }
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
    /// 检查当前进度需要执行的空降助手动作.
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

        if (!IsSponsorBlockEnabled)
        {
            // 播放中途关闭开关时，立刻放开控制器接管的静音并收起提示.
            if (_sponsorBlockController.IsMuteApplied || _sponsorBlockController.IsPromptActive)
            {
                var resetDecision = _sponsorBlockController.Reset();
                HideSponsorBlockTip();
                if (resetDecision is { Kind: SponsorBlockActionKind.RestoreMute, MuteState: bool restoreState })
                {
                    _ = ApplySponsorBlockMuteAsync(restoreState);
                }
            }

            return;
        }

        var decision = _sponsorBlockController.Evaluate(
            _sponsorBlockSegments,
            position,
            Player.Duration,
            IsMute,
            DateTimeOffset.UtcNow);

        if (decision.IsActionable)
        {
            _ = ApplySponsorBlockDecisionAsync(decision, _sponsorBlockGeneration);
        }
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

    private async Task ApplySponsorBlockDecisionAsync(SponsorBlockDecision decision, int generation)
    {
        if (generation != _sponsorBlockGeneration)
        {
            return;
        }

        switch (decision.Kind)
        {
            case SponsorBlockActionKind.Skip when decision.Segment is not null:
                await SkipSponsorSegmentAsync(decision.Segment, decision.TargetPosition, generation);
                break;
            case SponsorBlockActionKind.Mute
                when decision.MuteState is bool muteState && _sponsorBlockController.IsMuteApplied:
                // 用户恰好在进入片段时手动切换了静音，则放弃这次自动静音.
                await ApplySponsorBlockMuteAsync(muteState);
                break;
            case SponsorBlockActionKind.RestoreMute:
                if (decision.MuteState is bool restoreState)
                {
                    await ApplySponsorBlockMuteAsync(restoreState);
                }

                break;
            case SponsorBlockActionKind.ShowButton when decision.Segment is not null:
                SponsorBlockTipText = string.Format(
                    ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockPromptTemplate),
                    SponsorBlockCategoryHelper.GetDisplayName(decision.Segment.Category));
                RequestShowSponsorBlockTip?.Invoke(this, EventArgs.Empty);
                break;
            case SponsorBlockActionKind.HideButton:
                HideSponsorBlockTip();
                break;
        }
    }

    private async Task ApplySponsorBlockMuteAsync(bool isMuted)
    {
        if (Client is null)
        {
            return;
        }

        try
        {
            IsMute = isMuted;
            await Client.SetMuteAsync(isMuted);
        }
        catch (Exception ex)
        {
            // 静音切换失败时保持静默，不打断播放.
            logger.LogDebug(ex, "空降助手静音状态切换失败");
        }
    }

    private void HideSponsorBlockTip()
    {
        SponsorBlockTipText = null;
        RequestHideSponsorBlockTip?.Invoke(this, EventArgs.Empty);
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
