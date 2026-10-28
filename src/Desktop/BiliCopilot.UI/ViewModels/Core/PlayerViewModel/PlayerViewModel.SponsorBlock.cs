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
    /// <summary>
    /// 总开关的缓存时长：播放位置的逐帧回报不应每次都读本地设置，
    /// 但缓存必须足够短，让用户关闭开关后仍能立刻生效.
    /// </summary>
    private const long SponsorBlockEnabledCacheMs = 1000;

    private readonly SponsorBlockController _sponsorBlockController = new(SponsorBlockBehaviorSettings.Read);
    private IReadOnlyList<SponsorBlockSegment> _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
    private CancellationTokenSource? _sponsorBlockLoadSource;
    private int _sponsorBlockGeneration;
    private bool _isSponsorBlockSeeking;
    private bool _isSponsorBlockDisabled;
    private bool _hasSponsorBlockEnabledCache;
    private bool _cachedSponsorBlockEnabled;
    private long _sponsorBlockEnabledCacheTicks;

    /// <summary>
    /// 当前分 P 的空降助手片段列表，供进度条标记等界面复用.
    /// </summary>
    public IReadOnlyList<SponsorBlockSegment> SponsorBlockSegments => _sponsorBlockSegments;

    /// <summary>
    /// 当前分 P 的空降片段列表已更新（异步加载完成或切换分 P 时重置）.
    /// </summary>
    /// <remarks>
    /// 始终在 UI 线程触发，订阅方可以直接更新可视化元素.
    /// </remarks>
    public event EventHandler? SponsorBlockSegmentsChanged;

    /// <summary>
    /// 空降助手总开关是否开启.
    /// </summary>
    private bool IsSponsorBlockEnabled
    {
        get
        {
            if (_hasSponsorBlockEnabledCache
                && Environment.TickCount64 - _sponsorBlockEnabledCacheTicks < SponsorBlockEnabledCacheMs)
            {
                return _cachedSponsorBlockEnabled;
            }

            return ReadSponsorBlockEnabled();
        }
    }

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
    /// 空降助手跳转：自动跳过与点击进度条标记共用同一条路径.
    /// <para>
    /// 与控制器保持一致：目标裁剪到视频时长，且与当前位置相差不足
    /// <see cref="SponsorBlockController.MinimumRemainingSeconds"/> 时不下发无意义的跳转.
    /// </para>
    /// </summary>
    /// <param name="target">目标位置（秒）.</param>
    /// <returns>确实下发了跳转时返回 <c>true</c>.</returns>
    internal async Task<bool> SeekToSponsorBlockAsync(double target)
    {
        if (Client is null || Player is null)
        {
            return false;
        }

        var duration = Player.Duration;
        if (!double.IsFinite(target) || target < 0)
        {
            return false;
        }

        if (duration > 0 && target > duration)
        {
            target = duration;
        }

        if (Math.Abs(Player.Position - target) < SponsorBlockController.MinimumRemainingSeconds)
        {
            return false;
        }

        try
        {
            await Client.SetCurrentPositionAsync(target);
            return true;
        }
        catch (Exception ex)
        {
            // 跳转失败时保持静默，不打断播放.
            logger.LogDebug(ex, "空降助手跳转失败");
            return false;
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
        NotifySponsorBlockSegmentsChanged();
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

        if (!ReadSponsorBlockEnabled())
        {
            // 开关关闭时不再发起请求，也不保留任何片段.
            _isSponsorBlockDisabled = true;
            return;
        }

        _isSponsorBlockDisabled = false;

        if (Connector is not VideoConnectorViewModel videoConnector)
        {
            return;
        }

        var bvid = videoConnector.BvId;
        var cid = videoConnector.CurrentPartId;
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
        if (_isSponsorBlockSeeking
            || IsProgressChanging
            || Player?.IsPlaybackInitialized != true)
        {
            return;
        }

        if (!IsSponsorBlockEnabled)
        {
            DisableSponsorBlock();
            return;
        }

        if (_isSponsorBlockDisabled)
        {
            // 播放中途重新打开开关：片段已在关闭时清空，需要重新拉取.
            _isSponsorBlockDisabled = false;
            StartSponsorBlockLoading();
        }

        if (_sponsorBlockSegments.Count == 0)
        {
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

    /// <summary>
    /// 播放中途关闭总开关：取消未完成请求、清空片段与标记、收起提示并恢复静音.
    /// </summary>
    private void DisableSponsorBlock()
    {
        if (_isSponsorBlockDisabled)
        {
            return;
        }

        _isSponsorBlockDisabled = true;
        ResetSponsorBlock();
    }

    private bool ReadSponsorBlockEnabled()
    {
        _cachedSponsorBlockEnabled = SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);
        _sponsorBlockEnabledCacheTicks = Environment.TickCount64;
        _hasSponsorBlockEnabledCache = true;
        return _cachedSponsorBlockEnabled;
    }

    private async Task LoadSponsorBlockSegmentsAsync(string bvid, string cid, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var segments = await this.Get<SponsorBlockService>().GetSegmentsAsync(bvid, cid, cancellationToken);

            // 请求期间可能已切换视频 / 分 P，或用户关闭了总开关；两者都必须丢弃结果.
            // 这里重新读一次开关（并顺带刷新缓存），确保关闭后即使还没触发进度检查，
            // 迟到的结果也不会再写回视图模型.
            if (!SponsorBlockLoadGuard.ShouldApplyResult(
                generation,
                _sponsorBlockGeneration,
                ReadSponsorBlockEnabled()))
            {
                return;
            }

            _sponsorBlockSegments = segments;
            NotifySponsorBlockSegmentsChanged();
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
        // 该方法的调用点是即发即弃的，异常必须在此消化，不能逃逸为未观察到的任务异常.
        try
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
        catch (Exception ex)
        {
            // 任何界面或播放器异常都不应影响播放.
            logger.LogDebug(ex, "应用空降助手决策失败");
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

    /// <summary>
    /// 通知界面片段列表已变化。加载完成发生在后台线程，必须切回 UI 线程.
    /// </summary>
    private void NotifySponsorBlockSegmentsChanged()
        => queue.TryEnqueue(() => SponsorBlockSegmentsChanged?.Invoke(this, EventArgs.Empty));

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
            if (!await SeekToSponsorBlockAsync(targetPosition))
            {
                return;
            }

            var categoryName = SponsorBlockCategoryHelper.GetDisplayName(segment.Category);
            var message = string.Format(
                ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockSkippedTemplate),
                categoryName);
            Window?.ShowTip(message, InfoType.Information);
        }
        catch (Exception ex)
        {
            // 提示失败时保持静默，不打断播放.
            logger.LogDebug(ex, "空降助手跳过提示失败");
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
