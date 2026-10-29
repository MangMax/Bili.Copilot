// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Coalesces redraws for one panel on its UI dispatcher.
/// </summary>
internal sealed class DanmakuRedrawSession(Func<Action, bool> enqueue)
{
    private Action? _pending;
    private long _generation;

    /// <summary>
    /// Invalidates queued work before clearing, replacing or disposing a renderer.
    /// </summary>
    public void Invalidate()
    {
        _generation++;
        _pending = null;
    }

    /// <summary>
    /// Requests one redraw using the latest callback in this dispatcher batch.
    /// </summary>
    /// <param name="redraw">Callback that reads and draws the current panel state.</param>
    public void Request(Action redraw)
    {
        var queued = _pending is not null;
        _pending = redraw;
        var generation = _generation;
        if (!queued && !enqueue(() =>
        {
            if (generation != _generation)
            {
                return;
            }

            var current = _pending;
            _pending = null;
            current?.Invoke();
        }))
        {
            _pending = null;
        }
    }
}
