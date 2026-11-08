// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public PlayerViewModel()
    {
        Player.PropertyChanged += OnDanmakuPlayerPropertyChanged;
        Client!.ReadPosition = () => Player.Position;
        Client.ReadSpeed = () => Player.PlaybackRate;
        Client.ReadVolume = () => Player.Volume;
    }

    private bool _isDanmakuClosed;
    private int _danmakuPlaybackVersion;
    private BiliCopilot.UI.Danmaku.DanmakuLoadSession? _danmakuLoadSession;

    internal BiliCopilot.UI.Danmaku.DanmakuLoadSession? DanmakuLoadSession
    {
        get => _danmakuLoadSession;
        set => _danmakuLoadSession = value;
    }

    public DanmakuRenderViewModel Danmaku { get; } = new();

    /// <summary>
    /// 测试替身：生产实现由 <c>LiveModePolicy</c> 从媒体快照推导；
    /// 这里让能力集合跟随可写的 <c>IsLive</c>，守卫读到的仍是同一份策略。
    /// </summary>
    public BiliCopilot.UI.Live.LiveModeCapabilities LiveCapabilities
        => BiliCopilot.UI.Live.LiveModePolicy.For(
            IsLive ? BiliCopilot.UI.Models.Constants.BiliMediaType.Live : BiliCopilot.UI.Models.Constants.BiliMediaType.Video);

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

    public bool IsPlaybackInitialized { get; set; } = true;

    public double Volume { get; set; } = 100;

    public double Duration { get; set; } = 60;
}

public sealed class SeekTestClient
{
    public Func<double> ReadPosition { get; set; } = () => 21;
    public Func<double> ReadSpeed { get; set; } = () => 1;
    public Func<double> ReadVolume { get; set; } = () => 100;
    public Task<NativeValue> GetCurrentPositionAsync() => Task.FromResult(new NativeValue(ReadPosition()));
    public Task<NativeValue> GetSpeedAsync() => Task.FromResult(new NativeValue(ReadSpeed()));
    public Task<NativeValue> GetVolumeAsync() => Task.FromResult(new NativeValue(ReadVolume()));
    public sealed record NativeValue(double Value) { public bool IsSuccess => true; }

    public List<double> Speeds { get; } = [];
    public Task<SeekTestResult> SetSpeedAsync(double speed)
    {
        Speeds.Add(speed);
        return Task.FromResult(new SeekTestResult(true));
    }

    public List<Richasy.MpvKernel.Player.Models.MpvMediaSource> Loads { get; } = [];

    public bool CompleteLoads { get; set; } = true;
    public void CompleteLoad() => ReachFileLoaded?.Invoke(this, EventArgs.Empty);
    public void FailLoad() => ErrorOccurred?.Invoke(this, Richasy.MpvKernel.MpvError.LoadingFailed);
    public event EventHandler? ReachFileLoaded;
    public event EventHandler<Richasy.MpvKernel.MpvError>? ErrorOccurred;

    public Task PlayAsync(string url, Richasy.MpvKernel.Core.Models.MpvPlayOptions options)
    {
        Loads.Add(new(url, "id", "title", options));
        if (CompleteLoads) CompleteLoad();
        return Task.CompletedTask;
    }

    public List<double> Requests { get; } = [];

    public Func<double, Task<SeekTestResult>> Send { get; set; } = _ => Task.FromResult(new SeekTestResult(true));

    public Task<SeekTestResult> SetCurrentPositionAsync(double target)
    {
        Requests.Add(target);
        return Send(target);
    }
}

public sealed record SeekTestResult(bool IsSuccess);
