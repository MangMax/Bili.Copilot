// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>Bounds automatic live recovery using monotonic elapsed time.</summary>
internal sealed class LivePlaybackWatchdog
{
    private TimeSpan? _stalledSince;
    private TimeSpan? _lastRecovery;
    private double _position;
    private int _failures;

    public bool Observe(TimeSpan now, double position, double cacheSpeed, bool paused, bool terminal, bool refreshing, bool ended)
    {
        if (paused || terminal || refreshing)
        {
            _stalledSince = null;
            _position = position;
            return false;
        }

        if (ended)
        {
            return true;
        }

        if (cacheSpeed >= 50 * 1024)
        {
            _stalledSince = null;
            _position = position;
            return false;
        }

        if (_stalledSince is null || Math.Abs(position - _position) >= 0.5)
        {
            _stalledSince = now;
            _position = position;
        }

        return now - _stalledSince.Value >= TimeSpan.FromSeconds(8);
    }

    public bool TryBeginRecovery(TimeSpan now)
    {
        if (_failures >= 3 || (_lastRecovery is { } last && now - last < TimeSpan.FromSeconds(60)))
        {
            return false;
        }

        _lastRecovery = now;
        _stalledSince = null;
        return true;
    }

    public bool RecordFailure() => ++_failures < 3;

    public void RecordSuccess()
    {
        _failures = 0;
        _stalledSince = null;
    }

    public void ResetFailures() => _failures = 0;

    public void Reset()
    {
        _stalledSince = null;
        _lastRecovery = null;
        _position = 0;
        _failures = 0;
    }
}
