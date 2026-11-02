// Only XAML, services and renderer protocols are adapted. The panel is linked production source.
using System.ComponentModel;
using BiliCopilot.UI.ViewModels.Core;
namespace Microsoft.UI.Xaml { public class RoutedEventArgs : EventArgs { } public delegate void RoutedEventHandler(object sender, RoutedEventArgs e); }
namespace Microsoft.UI { public static class Colors { public static object Gray => "gray"; public static object White => "white"; } }
namespace Microsoft.UI.Dispatching
{
    public enum DispatcherQueuePriority { Low }
    public sealed class DispatcherQueue
    {
        readonly System.Collections.Concurrent.ConcurrentQueue<Action> pending = new();
        readonly int ownerThread = Environment.CurrentManagedThreadId;
        public bool HasThreadAccess => Environment.CurrentManagedThreadId == ownerThread;
        public bool TryEnqueue(DispatcherQueuePriority p, Action a) { pending.Enqueue(a); return true; }
        public void Drain() { while (pending.TryDequeue(out var a)) a(); }
    }
}
namespace Microsoft.Extensions.Logging
{
    public static class TestLogs { public static readonly List<Exception> Errors = []; }
    public sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) { if (error != null) TestLogs.Errors.Add(error); }
    }
}
namespace BiliCopilot.UI.Models.Constants { public enum DanmakuRendererType { DirectX, Win2D } public enum DanmakuLocation { Scroll } public enum SettingNames { DanmakuLocation, DanmakuColor, IsDanmakuStandardSize } }
namespace BiliCopilot.UI.Toolkits
{
    public static class TestServices { public static T Get<T>(this object owner) => (T)Activator.CreateInstance(typeof(Microsoft.Extensions.Logging.TestLogger<>).MakeGenericType(typeof(T).GenericTypeArguments))!; }
    public static class SettingsToolkit { public static T ReadLocalSetting<T>(Models.Constants.SettingNames key, T value) => value; }
    public static class AppToolkit { public static object HexToColor(string s) => s; }
}
namespace Richasy.Danmaku.Enums { public enum DanmakuMode { Scroll } }
namespace Richasy.Danmaku.Models { public sealed class DanmakuItem { public uint StartMs; public Enums.DanmakuMode Mode; public object? TextColor; public int BaseFontSize; public string? Text; public bool HasOutline, HasBorder; } }
namespace Richasy.BiliKernel.Models { public static class BilibiliDanmakuParser { public static List<Richasy.Danmaku.Models.DanmakuItem> GetDanmakuList(List<int> list, bool b) => []; } }
namespace Richasy.Danmaku
{
    public class DanmakuConfiguration { public static DanmakuConfiguration CreateDefault() => new(); public double TextOpacity, FontSizeScale, OutlineSize; public float RollingAreaRatio, RollingSpeedBase; public string? FontFamilyName; public bool IsTextBold; }
    public class RendererProtocol
    {
        public Exception? WorkerFailure { get; set; }
        public Exception? NextFailure; public Exception? CloseFailure; public bool PublishFailure; public int Calls, Closes; public Action? BeforeCall; public Action? BeforeClose;
        protected void Call() { Calls++; BeforeCall?.Invoke(); if (WorkerFailure is { } f) throw new InvalidOperationException("terminal", f); if (NextFailure is { } e) { NextFailure = null; if (PublishFailure) WorkerFailure = e; throw e; } }
        public void Close() { Closes++; BeforeClose?.Invoke(); if (CloseFailure is { } e) throw e; }
        public void Dispose() => Close(); public void Clear() => Call(); public void Stop() => Call(); public void Pause() => Call(); public void Resume() => Call(); public List<uint> SeekRequests { get; } = []; public void Seek(uint t) { Call(); SeekRequests.Add(t); } public void UpdateTime(uint t) => Call();
        public void AddRealtimeDanmaku(Models.DanmakuItem item, bool b) => Call(); public void SetDanmakuList(List<Models.DanmakuItem> items) => Call(); public void SetDanmakuEnabledType(bool a, bool b, bool c) => Call(); public void UpdateConfiguration(DanmakuConfiguration c) => Call();
        public void SetRollingDensity(int v) => Call();
        public void SetOpacity(double v) => Call();
        public void SetBorderColor(object v) => Call();
        public void SetRollingAreaRatio(int v) => Call();
        public void SetDanmakuFontSizeOffset(double v) => Call();
        public void SetFontFamilyName(string v) => Call();
        public void SetIsTextBold(bool v) => Call();
        public void SetOutlineSize(double v) => Call();
        public void SetRollingSpeed(double v) => Call();

    }
    public sealed class DanmakuRenderer : RendererProtocol { public DanmakuRenderer(object grid, object logger) { } }
}
namespace Richasy.Danmaku.Legacy { public sealed class DanmakuFrostMaster : Richasy.Danmaku.RendererProtocol { public DanmakuFrostMaster(object grid) { } } }
namespace BiliCopilot.UI.ViewModels.Core
{
    public sealed class DanmakuRenderViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public Models.Constants.DanmakuRendererType Renderer { get; set; } = Models.Constants.DanmakuRendererType.Win2D;
        bool visible = true; public bool IsShowDanmaku { get => visible; set { visible = value; PropertyChanged?.Invoke(this, new(nameof(IsShowDanmaku))); if (value) Redraw(); } }
        public bool IsPaused, IsDanmakuBold, IsRollingEnabled, IsTopEnabled, IsBottomEnabled; public double DanmakuOpacity, DanmakuArea, DanmakuFontSize, OutlineSize, DanmakuSpeed, ExtraSpeed; public string DanmakuFontFamily = "test"; public int Position; public List<int> GetCachedDanmakus() => [];
        public event EventHandler? RequestClearDanmaku, PauseDanmaku, ResumeDanmaku, RequestResetStyle, ExtraSpeedChanged, RequestRedrawDanmaku;
        public event EventHandler<int>? ProgressChanged, SeekRequested; public event EventHandler<string>? RequestAddSingleDanmaku;
        public void Progress(int p) { Position = p; ProgressChanged?.Invoke(this, p); }
        public void Seek(int p) { Position = p; SeekRequested?.Invoke(this, p); }
        public void Redraw() => RequestRedrawDanmaku?.Invoke(this, EventArgs.Empty);
        public void OtherInputs() { PauseDanmaku?.Invoke(this, EventArgs.Empty); ResumeDanmaku?.Invoke(this, EventArgs.Empty); RequestResetStyle?.Invoke(this, EventArgs.Empty); ExtraSpeedChanged?.Invoke(this, EventArgs.Empty); RequestAddSingleDanmaku?.Invoke(this, "text"); RequestClearDanmaku?.Invoke(this, EventArgs.Empty); }
    }
}
namespace BiliCopilot.UI.Controls.Danmaku
{
    public abstract class DanmakuControlBase
    {
        public event RoutedEventHandler? Loaded, Unloaded; public bool IsLoaded { get; private set; }
        public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; } = new(); public DanmakuRenderViewModel ViewModel { get; private set; } = new();
        protected virtual void OnViewModelChanged(DanmakuRenderViewModel? oldValue, DanmakuRenderViewModel? newValue) { }
        public void SetViewModel(DanmakuRenderViewModel vm) { var old = ViewModel; ViewModel = vm; OnViewModelChanged(old, vm); }
        public void Load() { IsLoaded = true; Loaded?.Invoke(this, new()); DispatcherQueue.Drain(); }
        public void Unload() { IsLoaded = false; Unloaded?.Invoke(this, new()); }
    }
    public sealed partial class VideoDanmakuPanel
    {
        public sealed class TestGrid { public List<object> Children { get; } = []; }
        TestGrid RootGrid { get; } = new(); void InitializeComponent() { }
        public Richasy.Danmaku.DanmakuRenderer? TestCore => _danmakuRenderer;
        public Richasy.Danmaku.Legacy.DanmakuFrostMaster? TestMaster => _danmakuMaster;
    }
}
