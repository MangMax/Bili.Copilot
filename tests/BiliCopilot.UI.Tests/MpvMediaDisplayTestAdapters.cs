namespace Microsoft.UI.Dispatching
{
    public sealed partial class DispatcherQueue
    {
        public DispatcherQueueTimer MediaTimer { get; } = new();
        public DispatcherQueueTimer CreateTimer() => MediaTimer;
        public bool TryEnqueue(Action callback) => TryEnqueue(DispatcherQueuePriority.Low, callback);
    }

    public sealed class DispatcherQueueTimer
    {
        private bool _running;
        public TimeSpan Interval { get; set; }
        public event Action<DispatcherQueueTimer, object>? Tick;
        public void Start() => _running = true;
        public void Stop() => _running = false;
        public void Fire() { if (_running) Tick?.Invoke(this, EventArgs.Empty); }
    }
}

namespace Richasy.MpvKernel
{
    public enum MpvFormat { Int64 }
    // Success/Unavailable 供小窗比例适配器读取播放属性；LoadingFailed 供播放失败路径使用。
    public enum MpvError { Success, Unavailable, LoadingFailed, Http403, Http404, Http500, TlsError, VoInitFailed, AoInitFailed, Unsupported, UnknownFormat, NothingToPlay }
    public struct MpvNode { public long IntegerValue; }
    public sealed class MpvInteropHandle
    {
        public Dictionary<string, long> Properties { get; } = new();
        public Action<string>? Reading { get; set; }
    }
    public static class MpvNative
    {
        public static MpvError GetProperty(MpvInteropHandle handle, string name, MpvFormat format, out MpvNode node)
        {
            handle.Reading?.Invoke(name);
            var found = handle.Properties.TryGetValue(name, out var value);
            node = new() { IntegerValue = value };
            return found ? MpvError.Success : MpvError.Unavailable;
        }
    }
}

namespace Richasy.MpvKernel.Core
{
    public sealed class MpvClient
    {
        public MpvInteropHandle Handle { get; } = new();
        public bool IsDisposed { get; set; }
        public event EventHandler? ReachFileLoading;
        public event EventHandler? ReachFileLoaded;
        public event EventHandler? ReachFileEnd;
        public void Loading() => ReachFileLoading?.Invoke(this, EventArgs.Empty);
        public void Loaded() => ReachFileLoaded?.Invoke(this, EventArgs.Empty);
        public void End() => ReachFileEnd?.Invoke(this, EventArgs.Empty);
        public void SetOutput(long entry, long width, long height, long rotation = 0, long position = 0)
        {
            // mpv exposes the playing slot separately from its lifetime-unique entry ID.
            Handle.Properties["playlist-playing-pos"] = position;
            Handle.Properties[$"playlist/{position}/id"] = entry;
            Handle.Properties["video-out-params/dw"] = width;
            Handle.Properties["video-out-params/dh"] = height;
            Handle.Properties["video-out-params/rotate"] = rotation;
        }
    }
}
