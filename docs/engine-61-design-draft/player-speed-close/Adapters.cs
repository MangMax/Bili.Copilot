namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private Task? _closeTask;
    private TaskCompletionSource? _disposeCompletion;
    private Task _initializationTask = Task.CompletedTask;
    private bool _isDanmakuClosed;
    private IDisposable? _danmakuLoadSession;
    private readonly Logger logger = new();
    private readonly AppViewModel app = new();
    private int _danmakuPlaybackVersion;
    public PlayerViewModel() { Player = new(Client); app.Players.Add(this); }
    public Client Client { get; set; } = new();
    public Player Player { get; set; }
    public Window? Window { get; set; }
    public bool UseIntegrationOperation => false;
    public bool IsSpeedChanging { get; set; }
    public DateTimeOffset LastSpeedChangingTime { get; set; }
    public Task SpeedAsync() => IncreaseSpeedAsync();
    public void ChangeMedia() => _danmakuPlaybackVersion++;
    public void HoldInitialization(Task task) => _initializationTask = task;
    private T Get<T>() => (T)(object)app;
    private void ShowMainWindow() { }
    private Task ReleasePlaybackEventsAsync() => Task.CompletedTask;
    private void OnPlayerPropertyChanged(object? s, EventArgs e) { }
    private void OnDanmakuPlayerPropertyChanged(object? s, EventArgs e) { }
    private void OnWindowSizeChanged(object? s, EventArgs e) { }
    private void OnWindowDestroying(object? s, EventArgs e) { }
}
public sealed class Client
{
    public List<double> Speeds { get; } = [];
    public Func<Task> Send { get; set; } = () => Task.CompletedTask;
    public int Disposals { get; private set; }
    public bool IsDisposed => Disposals != 0;
    public async Task<Result> SetSpeedAsync(double value) { Speeds.Add(value); await Send(); return new(true); }
    public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
}
public sealed record Result(bool IsSuccess);
public sealed class Player(Client client)
{
    public double PlaybackRate => 1;
    public event EventHandler? PropertyChanged;
    public ValueTask DisposeAsync() => client.DisposeAsync();
}
public class Window
{
    public bool IsClosed => true;
    public bool IsDisposed => true;
    public event EventHandler? SizeChanged;
    public event EventHandler? Destroying;
    public Window GetWindow() => this;
    public void Close() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
public sealed class MainWindow : Window { }
public sealed class AppViewModel
{
    public List<PlayerViewModel> Players { get; } = [];
    public List<Window> Windows { get; } = [];
    public bool IsMainWindowVisible() => true;
}
public class Application { public static Application Current { get; } = new App(); }
public sealed class App : Application { public TrayLifecycle TrayLifecycle { get; } = new(); }
public sealed class TrayLifecycle
{
    public Task ClosePlayerAsync(bool a, bool b, int c, bool d, Action show, Func<Task> close, Action exit) => close();
}
public static class SettingsToolkit { public static T ReadLocalSetting<T>(string key, T fallback) => fallback; }
public static class SettingNames { public const string HideMainWindowOnPlay = "hide"; }
public sealed class Logger { public void LogError(Exception ex, string text) { } }
