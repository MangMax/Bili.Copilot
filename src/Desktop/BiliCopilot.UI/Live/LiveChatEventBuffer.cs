// Copyright (c) Bili Copilot. All rights reserved.

using System.Threading.Channels;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>Bounded chat ingress; ordinary traffic cannot evict SC or room control events.</summary>
public sealed class LiveChatEventBuffer
{
    private readonly Channel<LiveDanmakuEvent> _messages;
    private readonly Channel<LiveDanmakuEvent> _control;
    private long _dropped;

    /// <summary>Creates separate bounded lanes, preserving order within each lane.</summary>
    public LiveChatEventBuffer(int capacity = 1024, int controlCapacity = 256)
    {
        _messages = Create(capacity);
        _control = Create(controlCapacity);
    }

    /// <summary>Gets the number of evicted events.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Accepts only events that affect chat.</summary>
    public void Enqueue(LiveDanmakuEvent value)
    {
        var writer = value switch
        {
            LiveDanmakuReceivedEvent or LiveUserEnteredEvent => _messages.Writer,
            LiveSuperChatEvent or LiveSuperChatDeletedEvent or LiveConnectionStateChangedEvent
                or LiveRoomNotStartedEvent or LiveStartedEvent or LiveRoomCutOffEvent => _control.Writer,
            _ => null,
        };
        writer?.TryWrite(value);
    }

    /// <summary>Reads control events first, then ordinary messages.</summary>
    public bool TryDequeue(out LiveDanmakuEvent value)
        => _control.Reader.TryRead(out value!) || _messages.Reader.TryRead(out value!);

    /// <summary>Discards events when leaving a viewing generation.</summary>
    public void Clear()
    {
        while (TryDequeue(out _)) { }
    }

    private Channel<LiveDanmakuEvent> Create(int capacity)
        => Channel.CreateBounded<LiveDanmakuEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        }, _ => Interlocked.Increment(ref _dropped));
}
