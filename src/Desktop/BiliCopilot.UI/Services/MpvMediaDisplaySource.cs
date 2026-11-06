// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Dispatching;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core;

namespace BiliCopilot.UI.Services;

/// <summary>
/// Adapts libmpv output parameters to media-scoped display dimensions. The pinned
/// MpvClient has no size notification; sample on the UI timer even while paused.
/// No window/cover sizes or decoder storage dimensions are used as a fallback.
/// </summary>
internal sealed class MpvMediaDisplaySource : IDisposable
{
    private readonly MpvClient _client;
    private readonly DispatcherQueue _queue;
    private readonly DispatcherQueueTimer _timer;
    private readonly Func<long> _beginMedia;
    private readonly Action<long, double, double> _publish;
    private readonly object _gate = new();
    private long _generation;
    private long _observedGeneration = -1;
    private long _windowGeneration;
    private long? _loadedEntry;
    private long? _previousEntry;
    private bool _acceptEvents;
    private bool _closed;
    private bool _awaitingLoad;
    private bool _hasRuntimeDimensions;

    public MpvMediaDisplaySource(MpvClient client, DispatcherQueue queue, Func<long> beginMedia, Action<long, double, double> publish)
    {
        _client = client;
        _queue = queue;
        _beginMedia = beginMedia;
        _publish = publish;
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += OnTick;
        client.ReachFileLoading += OnLoading;
        client.ReachFileLoaded += OnLoaded;
        client.ReachFileEnd += OnEnded;
    }

    /// <summary>Capture the application source lifetime before resolving its selected DASH stream.</summary>
    public Func<double, double, Task> CreateMetadataPublisher()
    {
        long generation;
        lock (_gate)
        {
            ResetWindowGeneration();
            generation = _generation;
        }

        return (width, height) =>
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_queue.TryEnqueue(() =>
            {
                try
                {
                    lock (_gate)
                    {
                        if (!_closed && generation == _generation && !_hasRuntimeDimensions)
                        {
                            _publish(_windowGeneration, width, height);
                        }
                    }

                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }))
            {
                completion.TrySetResult();
            }

            return completion.Task;
        };
    }

    /// <summary>Invalidate immediately, before awaiting any old-media stop work.</summary>
    public void Suspend()
    {
        lock (_gate)
        {
            _acceptEvents = false;
            if (!_client.IsDisposed)
            {
                _previousEntry = ReadPlayingEntry() ?? _loadedEntry ?? _previousEntry;
            }

            _loadedEntry = null;
            _generation++;
            ResetWindowGeneration();
        }

        _timer.Stop();
    }

    /// <summary>Called after old playback has stopped and before starting the new load.</summary>
    public void Resume()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _acceptEvents = true;
            _awaitingLoad = true;
        }

        _timer.Start();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _acceptEvents = false;
            _loadedEntry = null;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _client.ReachFileLoading -= OnLoading;
        _client.ReachFileLoaded -= OnLoaded;
        _client.ReachFileEnd -= OnEnded;
    }

    private void OnLoading(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            if (_closed || !_acceptEvents)
            {
                return;
            }

            _loadedEntry = null;
            // The first load belongs to the already prepared API hint. Later loads
            // (or file end) invalidate it, just like an application source switch.
            if (_awaitingLoad)
            {
                _awaitingLoad = false;
                return;
            }

            var generation = ++_generation;
            _queue.TryEnqueue(() =>
            {
                lock (_gate)
                {
                    if (!_closed && generation == _generation)
                    {
                        ResetWindowGeneration();
                    }
                }
            });
        }
    }

    private void OnEnded(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            _awaitingLoad = false;
            OnLoading(sender, args);
        }
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            if (!_closed && _acceptEvents && !_client.IsDisposed)
            {
                var entry = ReadPlayingEntry();
                _loadedEntry = entry != _previousEntry ? entry : null;
            }
        }
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        lock (_gate)
        {
            if (_closed || !_acceptEvents || _client.IsDisposed)
            {
                return;
            }

            ResetWindowGeneration();
            if (_loadedEntry is not long entry || ReadPlayingEntry() != entry)
            {
                return;
            }

            // Output dw/dh include sample aspect ratio; rotate is the remaining VO rotation.
            // Read twice to avoid publishing a torn tuple during output reconfiguration.
            var first = ReadOutput();
            var second = ReadOutput();
            if (first != second || ReadPlayingEntry() != entry)
            {
                return;
            }

            var dimensions = MediaDisplayDimensions.FromOutput(first.Width ?? 0, first.Height ?? 0, first.Rotation ?? double.NaN);
            if (dimensions.Width > 0 && dimensions.Height > 0)
            {
                _hasRuntimeDimensions = true;
                _publish(_windowGeneration, dimensions.Width, dimensions.Height);
            }
        }
    }

    private void ResetWindowGeneration()
    {
        if (_observedGeneration != _generation)
        {
            _hasRuntimeDimensions = false;
            _observedGeneration = _generation;
            _windowGeneration = _beginMedia();
        }
    }

    private (long? Width, long? Height, long? Rotation) ReadOutput()
        => (ReadInteger("video-out-params/dw"), ReadInteger("video-out-params/dh"), ReadInteger("video-out-params/rotate"));

    private long? ReadPlayingEntry()
    {
        // playlist-playing-pos is a mutable index, not the lifetime-unique entry ID.
        // A negative position means no playing entry, including during transitions.
        var position = ReadInteger("playlist-playing-pos");
        if (position is not >= 0)
        {
            return null;
        }

        var property = FormattableString.Invariant($"playlist/{position}/id");
        var entry = ReadInteger(property);
        // The playlist may move or replace this slot between native property reads.
        return entry is >= 0
            && ReadInteger("playlist-playing-pos") == position
            && ReadInteger(property) == entry
            ? entry
            : null;
    }

    private long? ReadInteger(string property)
    {
        return MpvNative.GetProperty(_client.Handle, property, MpvFormat.Int64, out var node) == MpvError.Success
            ? node.IntegerValue
            : null;
    }
}
