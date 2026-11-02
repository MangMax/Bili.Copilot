// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    public PlayerViewModel() => Player.PropertyChanged += OnDanmakuPlayerPropertyChanged;

    private bool _isDanmakuClosed;
    private int _danmakuPlaybackVersion;
    private BiliCopilot.UI.Danmaku.DanmakuLoadSession? _danmakuLoadSession;

    internal BiliCopilot.UI.Danmaku.DanmakuLoadSession? DanmakuLoadSession
    {
        get => _danmakuLoadSession;
        set => _danmakuLoadSession = value;
    }

    public DanmakuRenderViewModel Danmaku { get; } = new();

    public SeekTestPlayer Player { get; set; } = new();

    public SeekTestClient? Client { get; set; } = new();

    public void ChangeScene() => _danmakuPlaybackVersion++;

    public void CloseInput() => _isDanmakuClosed = true;

    public Task DrainPositionChangesAsync() => WaitForPositionChangesAsync();
}

public sealed class SeekTestPlayer
{
    public List<double> ReplayRequests { get; } = [];

    public Task ReplayAsync(double target)
    {
        ReplayRequests.Add(target);
        return Task.CompletedTask;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void Notify(string property) => PropertyChanged?.Invoke(this, new(property));

    public Richasy.MpvKernel.Core.Enums.MpvPlayerState PlaybackState { get; set; }

    public double PlaybackRate { get; set; } = 1;

    public double Position { get; set; } = 21;

    public double Duration { get; set; } = 60;
}

public sealed class SeekTestClient
{
    public Task<SeekTestResult> SetSpeedAsync(double speed) => Task.FromResult(new SeekTestResult(true));

    public List<double> Requests { get; } = [];

    public Func<double, Task<SeekTestResult>> Send { get; set; } = _ => Task.FromResult(new SeekTestResult(true));

    public Task<SeekTestResult> SetCurrentPositionAsync(double target)
    {
        Requests.Add(target);
        return Send(target);
    }
}

public sealed record SeekTestResult(bool IsSuccess);
