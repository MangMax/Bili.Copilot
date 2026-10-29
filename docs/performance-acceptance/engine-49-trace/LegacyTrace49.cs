// Copyright (c) Richasy. All rights reserved.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Richasy.Danmaku.Legacy;

// Diagnostic-only per-thread single-writer rings. No engine logger or file I/O.
internal static class LegacyTrace49
{
    private const int Capacity = 32768;
    private static readonly ConcurrentBag<Ring> Rings = new();
    private static readonly ThreadLocal<Ring> Local = new(() =>
    {
        var ring = new Ring();
        Rings.Add(ring);
        return ring;
    });
    private static long _trace49Sequence;

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    internal static void Add(object data)
    {
        var ring = Local.Value!;
        var position = ring.Written;
        var entry = new
        {
            sequence = Interlocked.Increment(ref _trace49Sequence),
            ticks = Stopwatch.GetTimestamp(),
            threadId = Environment.CurrentManagedThreadId,
            nativeThreadId = GetCurrentThreadId(),
            data,
        };
        Volatile.Write(ref ring.Entries[position % Capacity], entry);
        Volatile.Write(ref ring.Written, position + 1);
    }

    // Asynchronous collector calls this; never waits for a render callback.
    public static object Snapshot() => new
    {
        frequency = Stopwatch.Frequency,
        rings = Rings.Select(ring => new
        {
            written = Volatile.Read(ref ring.Written),
            capacity = Capacity,
            entries = ring.Entries.Select((_, index) => Volatile.Read(ref ring.Entries[index])).Where(entry => entry != null).ToArray(),
        }).ToArray(),
    };

    private sealed class Ring
    {
        internal readonly object?[] Entries = new object?[Capacity];
        internal long Written;
    }
}
